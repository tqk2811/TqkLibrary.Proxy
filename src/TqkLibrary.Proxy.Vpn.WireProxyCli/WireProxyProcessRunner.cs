using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using TqkLibrary.Proxy.Vpn.WireProxyCli.Exceptions;

namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    internal sealed class WireProxyProcessRunner : IDisposable
    {
        private const string GeneratedConfigPrefix = "tqk-wg-";
        private const string GeneratedConfigSuffix = ".conf";

        /// <summary>
        /// Age past which a leftover generated config belongs to nobody: wireproxy reads the file
        /// within seconds of it being written, so anything this old is from a run that died.
        /// </summary>
        private static readonly TimeSpan OrphanedConfigAge = TimeSpan.FromHours(1);

        /// <summary>Budget for one liveness probe, retried until the startup deadline.</summary>
        private const int ProbeTimeoutMs = 500;

        /// <summary>How many of wireproxy's most recent output lines are kept for diagnostics.</summary>
        private const int StderrLineCapacity = 200;

        private readonly WireGuardOptions _options;
        private readonly string _binaryPath;
        private readonly bool _isWindows;

        /// <summary>
        /// Ties the subprocess to this process's lifetime, so an abrupt end here is an end there
        /// too. <see cref="Dispose"/> still kills explicitly; this only covers the paths where
        /// Dispose never runs.
        /// </summary>
        private readonly KillOnCloseJobObject _job = new KillOnCloseJobObject();

        /// <summary>
        /// Cancelled by <see cref="Dispose"/>. The shared startup attempt runs on this rather than
        /// on any one caller's token, so the caller that happened to be first walking away does
        /// not cancel the tunnel out from under everybody waiting behind it.
        /// </summary>
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        private readonly object _lock = new object();
        private Process? _process;

        /// <summary>
        /// The one startup in flight, shared by every caller in a burst. Null until the first
        /// <see cref="EnsureStartedAsync"/>; faulted or cancelled means the next caller retries.
        /// </summary>
        private Task? _startTask;

        private string? _generatedConfigPath;
        /// <summary>
        /// The last <see cref="StderrLineCapacity"/> lines wireproxy wrote, newest last.
        /// </summary>
        /// <remarks>
        /// Keeping the first N bytes instead kept the banner and threw away the reason: whatever
        /// killed the process is by definition the last thing it said, and that is the line a
        /// supervisor puts in front of the user.
        /// </remarks>
        private readonly Queue<string> _stderrLines = new Queue<string>();
        private int _disposed;

        /// <summary>
        /// Where the tunnel's SOCKS5 listener is. For a generated config with no bind address of
        /// its own this moves on every restart, so read it rather than caching it.
        /// </summary>
        public IPEndPoint Socks5Endpoint { get; private set; }

        /// <summary>
        /// Raised when the subprocess exits on its own. Not raised by <see cref="Dispose"/>: a
        /// supervisor listening for this wants to know about failures, not about being shut down.
        /// </summary>
        public event EventHandler<WireProxyExitedEventArgs>? Exited;

        /// <summary>True while the subprocess is running. Says nothing about the tunnel's health.</summary>
        public bool IsAlive
        {
            get
            {
                Process? p = _process;
                if (p is null) return false;
                try { return !p.HasExited; }
                catch { return false; }
            }
        }

        public WireProxyProcessRunner(WireGuardOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            bool inlineConfig = options.Config != null;
            bool fileConfig = !string.IsNullOrEmpty(options.ConfigFilePath);
            if (inlineConfig == fileConfig)
                throw new WireGuardException("Exactly one of WireGuardOptions.Config or ConfigFilePath must be set.");

            _binaryPath = ResolveBinary(options.BinaryPath, _isWindows)
                ?? throw new WireGuardException("wireproxy executable not found. Set WireGuardOptions.BinaryPath or place wireproxy on PATH.");

            if (inlineConfig)
            {
                Socks5Endpoint = options.Socks5BindAddress ?? new IPEndPoint(IPAddress.Loopback, GetFreeTcpPort());
                DeleteOrphanedConfigs();
            }
            else
            {
                Socks5Endpoint = options.ExternalSocks5Endpoint
                    ?? throw new WireGuardException("WireGuardOptions.ExternalSocks5Endpoint is required when ConfigFilePath is set.");
            }
        }

        /// <summary>
        /// Brings wireproxy up and returns once its SOCKS5 listener answers. Concurrent callers
        /// share one attempt and all return together.
        /// </summary>
        /// <remarks>
        /// The shape matters more than it looks. A browser opening six sockets at once through a
        /// freshly selected VPN outbound calls this six times within a millisecond; a cheaper
        /// "is the process alive" check would let five of them straight through to a port nothing
        /// is listening on yet, and they would come back refused. Waiting on one shared task is
        /// what makes the burst behave like the single connection it logically is.
        /// </remarks>
        public async Task EnsureStartedAsync(CancellationToken cancellationToken)
        {
            Task start;
            lock (_lock)
            {
                if (_disposed != 0) throw new ObjectDisposedException(nameof(WireProxyProcessRunner));

                Task? pending = _startTask;
                if (pending != null && !pending.IsCompleted)
                {
                    start = pending;
                }
                else if (pending != null && pending.Status == TaskStatus.RanToCompletion && IsAlive)
                {
                    return;
                }
                else
                {
                    Spawn();
                    start = _startTask = WaitForListenerOrTearDownAsync(_lifetimeCts.Token);
                    // Every caller may walk away on its own token; without this the shared
                    // failure would come back as an unobserved task exception.
                    _ = start.ContinueWith(
                        static t => _ = t.Exception, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }

            await AwaitSharedStartAsync(start, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Waits on a start attempt somebody else may have begun, honouring this caller's token
        /// without cancelling the shared attempt.
        /// </summary>
        private static async Task AwaitSharedStartAsync(Task start, CancellationToken cancellationToken)
        {
            if (start.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                await start.ConfigureAwait(false);
                return;
            }

            var abandoned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(static s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), abandoned))
            {
                if (await Task.WhenAny(start, abandoned.Task).ConfigureAwait(false) != start)
                    cancellationToken.ThrowIfCancellationRequested();
            }
            await start.ConfigureAwait(false);
        }

        private async Task WaitForListenerOrTearDownAsync(CancellationToken cancellationToken)
        {
            try
            {
                await WaitForListenerAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // A listener that never came up leaves a process that will never serve anyone.
                // Left running it would keep answering "alive" while the tunnel stays dead, and
                // nothing short of disposing the whole source would clear it.
                KillCurrentProcess();
                throw;
            }
        }

        /// <summary>Launches wireproxy. The caller holds <see cref="_lock"/>.</summary>
        private void Spawn()
        {
            bool isRestart = _process != null;
            if (isRestart && !_options.AutoRestart)
            {
                string err;
                err = ReadStderr();
                int code;
                try { code = _process!.ExitCode; } catch { code = -1; }
                // Nobody is going to restart it, so let the corpse go rather than re-reading it
                // on every later call.
                DiscardProcess();
                throw new WireGuardException(
                    $"wireproxy has exited (code={code}) and AutoRestart is disabled: {err.Trim()}", code, err);
            }

            DiscardProcess();
            if (isRestart)
            {
                DeleteGeneratedConfig();
                // A fresh port every time. While wireproxy was down anything could have taken the
                // old one, and a probe that lands on a stranger's listener reads as a healthy
                // tunnel — after which every request through it goes somewhere nobody chose.
                if (_options.Config != null && _options.Socks5BindAddress is null)
                    Socks5Endpoint = new IPEndPoint(IPAddress.Loopback, GetFreeTcpPort());
            }
            lock (_stderrLines) _stderrLines.Clear();

            string configPath;
            if (_options.Config != null)
            {
                var content = WireGuardConfigWriter.Build(
                    _options.Config, Socks5Endpoint, _options.Socks5Username, _options.Socks5Password,
                    _options.DefaultPersistentKeepalive);
                configPath = Path.Combine(
                    Path.GetTempPath(), $"{GeneratedConfigPrefix}{Guid.NewGuid():N}{GeneratedConfigSuffix}");
                WritePrivateFile(configPath, content, _isWindows);
                _generatedConfigPath = configPath;
            }
            else
            {
                configPath = _options.ConfigFilePath!;
            }

            var args = new List<string> { "-c", configPath };
            foreach (var extra in _options.ExtraArgs) args.Add(extra);

            var psi = BuildPsi(_binaryPath, args);
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            try
            {
                process.ErrorDataReceived += OnStdErr;
                process.OutputDataReceived += OnStdErr;
                process.Exited += OnProcessExited;
                if (!process.Start())
                    throw new WireGuardException("Failed to start wireproxy process.");
                _job.Assign(process);
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
            }
            catch
            {
                try { process.Exited -= OnProcessExited; } catch { }
                try { if (!process.HasExited) process.Kill(); } catch { }
                try { process.Dispose(); } catch { }
                throw;
            }
            _process = process;

            if (_disposed != 0)
            {
                // Dispose was called while we held the lock spawning. It waits on the lock, so it
                // would find this process anyway — but saying so here keeps the invariant local:
                // nothing survives Spawn once disposal has begun.
                KillCurrentProcessLocked();
                throw new ObjectDisposedException(nameof(WireProxyProcessRunner));
            }
        }

        /// <summary>Lets go of the current process handle without killing it. Caller holds the lock.</summary>
        private void DiscardProcess()
        {
            var p = _process;
            _process = null;
            if (p == null) return;
            try { p.Exited -= OnProcessExited; } catch { }
            try { p.Dispose(); } catch { }
        }

        private void DeleteGeneratedConfig()
        {
            if (_generatedConfigPath == null) return;
            try { File.Delete(_generatedConfigPath); } catch { }
            _generatedConfigPath = null;
        }

        private void KillCurrentProcess()
        {
            lock (_lock) KillCurrentProcessLocked();
        }

        /// <summary>Kills and forgets the current process. Caller holds <see cref="_lock"/>.</summary>
        private void KillCurrentProcessLocked()
        {
            var p = _process;
            _process = null;
            if (p != null)
            {
                // Unsubscribe first: killing it would otherwise tell every supervisor the tunnel
                // failed, when in fact we are the ones taking it down.
                try { p.Exited -= OnProcessExited; } catch { }
                try { if (!p.HasExited) p.Kill(); } catch { }
                try { p.Dispose(); } catch { }
            }
            DeleteGeneratedConfig();
        }

        private async Task WaitForListenerAsync(CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(_options.StartupTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var p = _process;
                if (p == null || p.HasExited)
                {
                    string err;
                    err = ReadStderr();
                    throw new WireGuardException(
                        $"wireproxy exited early (code={p?.ExitCode}): {err.Trim()}",
                        p?.ExitCode ?? -1, err);
                }

                if (await TryProbeAsync(cancellationToken).ConfigureAwait(false))
                    return;

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            throw new WireGuardException(
                $"Timed out after {_options.StartupTimeoutMs}ms waiting for wireproxy SOCKS5 listener at {Socks5Endpoint}.");
        }

        private async Task<bool> TryProbeAsync(CancellationToken cancellationToken)
        {
            IPEndPoint endpoint = Socks5Endpoint;
            try
            {
                using var client = new TcpClient(endpoint.AddressFamily);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked.CancelAfter(ProbeTimeoutMs);
                // Closing the socket is what actually unblocks a connect or a read in flight;
                // on netstandard2.0 the token alone does nothing to either.
                using var abort = linked.Token.Register(
                    static s => { try { ((TcpClient)s!).Close(); } catch { } }, client);

                await client.ConnectAsync(endpoint.Address, endpoint.Port).ConfigureAwait(false);
                if (!client.Connected) return false;

                return await SpeaksSocks5Async(client.GetStream(), linked.Token).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Opens a SOCKS5 method negotiation and checks the answer carries the protocol version.
        /// </summary>
        /// <remarks>
        /// A bare TCP connect only proves that something is listening. Since the port is reused
        /// across restarts, that something may be a program that grabbed it while wireproxy was
        /// down — and to a connect-only probe a stranger looks exactly like a healthy tunnel.
        /// Two bytes of protocol tell them apart.
        /// </remarks>
        private static async Task<bool> SpeaksSocks5Async(NetworkStream stream, CancellationToken cancellationToken)
        {
            // VER=5, offering both no-auth and username/password. Whether the listener accepts one
            // or rejects both, a SOCKS5 server answers with its version in the first byte.
            byte[] greeting = { 0x05, 0x02, 0x00, 0x02 };
            await stream.WriteAsync(greeting, 0, greeting.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            var reply = new byte[2];
            int read = 0;
            while (read < reply.Length)
            {
                int n = await stream.ReadAsync(reply, read, reply.Length - read, cancellationToken).ConfigureAwait(false);
                if (n <= 0) return false;
                read += n;
            }
            return reply[0] == 0x05;
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            // Ignore the process we have already replaced or shut down: only the current one
            // exiting is news. Reading Exited off a disposed runner would also be a lie.
            if (_disposed != 0 || !ReferenceEquals(sender, _process)) return;

            var p = (Process)sender!;
            string err;
            err = ReadStderr();

            int exitCode;
            try { exitCode = p.ExitCode; } catch { exitCode = -1; }

            try { Exited?.Invoke(this, new WireProxyExitedEventArgs(exitCode, err.Trim())); }
            catch { /* a broken subscriber must not take down the process-exit callback */ }
        }

        private void OnStdErr(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            lock (_stderrLines)
            {
                _stderrLines.Enqueue(e.Data);
                while (_stderrLines.Count > StderrLineCapacity) _stderrLines.Dequeue();
            }
        }

        /// <summary>Everything currently retained from wireproxy's output, oldest line first.</summary>
        private string ReadStderr()
        {
            lock (_stderrLines)
            {
                if (_stderrLines.Count == 0) return string.Empty;
                var sb = new StringBuilder();
                foreach (var line in _stderrLines) sb.AppendLine(line);
                return sb.ToString();
            }
        }

        private static ProcessStartInfo BuildPsi(string fileName, IList<string> args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
#if NET6_0_OR_GREATER
            foreach (var a in args) psi.ArgumentList.Add(a);
#else
            psi.Arguments = BuildArgumentString(args);
#endif
            return psi;
        }

#if !NET6_0_OR_GREATER
        private static string BuildArgumentString(IList<string> args)
        {
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
                {
                    sb.Append(a);
                }
                else
                {
                    sb.Append('"');
                    sb.Append(a.Replace("\\", "\\\\").Replace("\"", "\\\""));
                    sb.Append('"');
                }
            }
            return sb.ToString();
        }
#endif

#if NET6_0_OR_GREATER
        /// <summary>
        /// Creates the file with an explicit owner-only ACL already on it. False if the platform
        /// or the filesystem will not take one, leaving the caller to fall back.
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool TryWriteOwnerOnlyFile(string path, string content)
        {
            try
            {
                var security = new System.Security.AccessControl.FileSecurity();
                // Inheritance off: the account running us ends up the only entry on the file.
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    System.Security.Principal.WindowsIdentity.GetCurrent().User!,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));

                using var stream = System.IO.FileSystemAclExtensions.Create(
                    new FileInfo(path), FileMode.CreateNew, System.Security.AccessControl.FileSystemRights.Write,
                    FileShare.None, 4096, FileOptions.None, security);
                using var writer = new StreamWriter(stream);
                writer.Write(content);
                return true;
            }
            catch (PlatformNotSupportedException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (NotSupportedException) { return false; }
        }
#endif

        /// <summary>
        /// Writes <paramref name="content"/> to a new file readable only by the current user.
        /// </summary>
        /// <remarks>
        /// The file carries the WireGuard PrivateKey, so it should never be created wide open and
        /// narrowed afterwards: between the two there is a window in which anyone can read it.
        /// On Windows the permissions go on before a byte is written; elsewhere the file is created
        /// with 0600 and only then filled in.
        /// </remarks>
        private static void WritePrivateFile(string path, string content, bool isWindows)
        {
            if (isWindows)
            {
#if NET6_0_OR_GREATER
                if (OperatingSystem.IsWindows() && TryWriteOwnerOnlyFile(path, content)) return;
#endif
                // Falling back leaves the file on the temp directory's own permissions, which on
                // Windows already exclude other users. Worth having, not worth failing over.
                File.WriteAllText(path, content);
                return;
            }

            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream);
                writer.Write(content);
            }
            try
            {
                using var chmod = Process.Start(new ProcessStartInfo("chmod", $"600 {path}")
                { UseShellExecute = false, CreateNoWindow = true });
                chmod?.WaitForExit(2000);
            }
            catch { }
        }

        /// <summary>
        /// Removes generated configs left behind by runs that never got to clean up after
        /// themselves. They carry a PrivateKey, so leaving them in the temp directory forever is
        /// the part that matters; the disk space is incidental.
        /// </summary>
        private static void DeleteOrphanedConfigs()
        {
            try
            {
                var cutoff = DateTime.UtcNow - OrphanedConfigAge;
                foreach (var path in Directory.EnumerateFiles(
                    Path.GetTempPath(), $"{GeneratedConfigPrefix}*{GeneratedConfigSuffix}"))
                {
                    try
                    {
                        // Anything younger may belong to a runner that is starting right now,
                        // here or in another process.
                        if (File.GetLastWriteTimeUtc(path) > cutoff) continue;
                        File.Delete(path);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
            finally { listener.Stop(); }
        }

        private static string? ResolveBinary(string? explicitPath, bool isWindows)
        {
            if (!string.IsNullOrEmpty(explicitPath))
            {
                if (File.Exists(explicitPath)) return explicitPath;
                throw new WireGuardException($"wireproxy executable not found at: {explicitPath}");
            }

            // Whatever PATH says stays true for the life of the process, so pay for the lookup
            // once instead of on every runner: it spawns `where`, and it does it in a constructor
            // a UI thread may well be standing on.
            return _binaryOnPath.Value;
        }

        private static readonly Lazy<string?> _binaryOnPath = new Lazy<string?>(
            () => ProbeForBinary(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)),
            LazyThreadSafetyMode.ExecutionAndPublication);

        private static string? ProbeForBinary(bool isWindows)
        {
            string fileName = isWindows ? "wireproxy.exe" : "wireproxy";

            // Probe alongside the host app.
            string localProbe = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(localProbe)) return localProbe;

            var locator = isWindows ? "where" : "which";
            var probe = fileName;
            try
            {
                var psi = new ProcessStartInfo(locator, probe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                if (p.ExitCode != 0) return null;
                var first = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return string.IsNullOrWhiteSpace(first) ? null : first.Trim();
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            // Cancelled, never disposed: a startup attempt may still be sitting on this token,
            // and disposing it under that attempt only trades a clean cancel for an
            // ObjectDisposedException nothing is there to catch.
            try { _lifetimeCts.Cancel(); } catch { }

            // Under the lock: a spawn in flight holds it, and disposing around one would read
            // _process before that spawn assigns it and leave the child running forever.
            lock (_lock) KillCurrentProcessLocked();

            _job.Dispose();
        }
    }
}
