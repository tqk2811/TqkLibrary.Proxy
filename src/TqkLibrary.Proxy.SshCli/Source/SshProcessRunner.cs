using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TqkLibrary.Proxy.SshCli.Exceptions;

namespace TqkLibrary.Proxy.SshCli
{
    internal sealed class SshProcessRunner : IDisposable
    {
        private enum PasswordMode { None, SshPass, AskPass }

        private readonly OpenSshConnectionOptions _options;
        private readonly string _sshPath;
        private readonly string? _sshPassPath;
        private readonly string? _passwordFile;
        private readonly string? _askPassHelperPath;
        private readonly PasswordMode _passwordMode;

        public bool IsWindows { get; }
        public bool ControlMasterEnabled { get; }
        public string? ControlSocketPath { get; }

        public SshProcessRunner(OpenSshConnectionOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            _sshPath = ResolveExecutable(options.SshExecutablePath, "ssh", IsWindows)
                ?? throw new SshProcessException("ssh executable not found. Install OpenSSH client or set SshExecutablePath.");

            if (!string.IsNullOrEmpty(options.Password))
            {
                _passwordFile = CreatePasswordFile(options.Password!, IsWindows);
                if (IsWindows)
                {
                    _passwordMode = PasswordMode.AskPass;
                    _askPassHelperPath = CreateAskPassHelper(_passwordFile);
                }
                else
                {
                    _passwordMode = PasswordMode.SshPass;
                    _sshPassPath = ResolveExecutable(options.SshPassExecutablePath, "sshpass", IsWindows)
                        ?? throw new SshProcessException("sshpass not found. Install sshpass (e.g. 'apt install sshpass') or remove Password.");
                }
            }

            // ControlMaster requires unix-domain sockets; Windows OpenSSH does not support it.
            ControlMasterEnabled = options.UseControlMaster && !IsWindows;
            if (ControlMasterEnabled)
            {
                ControlSocketPath = Path.Combine(Path.GetTempPath(), $"tqk-sshcli-{Guid.NewGuid():N}.sock");
            }
        }

        public Process StartMaster()
        {
            if (!ControlMasterEnabled) throw new InvalidOperationException("ControlMaster disabled.");
            var args = new List<string>
            {
                "-M",
                "-S", ControlSocketPath!,
                "-N",
                "-o", $"ControlPersist={_options.ControlPersistSeconds}",
            };
            AppendCommonArgs(args);
            args.Add($"{_options.User}@{_options.Host}");
            return Start(args, usePasswordAuth: _passwordMode != PasswordMode.None);
        }

        public Process StartForwardW(string targetHost, int targetPort)
        {
            var args = new List<string>();
            if (ControlMasterEnabled)
            {
                args.Add("-S");
                args.Add(ControlSocketPath!);
            }
            args.Add("-W");
            args.Add($"{targetHost}:{targetPort}");
            AppendCommonArgs(args);
            args.Add($"{_options.User}@{_options.Host}");

            // When ControlMaster is up, slave connection multiplexes on the master — no auth needed.
            bool usePasswordAuth = _passwordMode != PasswordMode.None && !ControlMasterEnabled;
            return Start(args, usePasswordAuth);
        }

        public void StopMaster()
        {
            if (!ControlMasterEnabled) return;
            try
            {
                var args = new List<string>
                {
                    "-S", ControlSocketPath!,
                    "-O", "exit",
                    $"{_options.User}@{_options.Host}",
                };
                var psi = BuildPsi(_sshPath, args, redirect: true);
                using var p = Process.Start(psi);
                if (p != null)
                {
                    if (!p.WaitForExit(2000))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
            }
            catch { }
        }

        private void AppendCommonArgs(List<string> args)
        {
            args.Add("-p"); args.Add(_options.Port.ToString());
            // BatchMode disables password prompts AND askpass — skip it when password auth is active.
            if (_passwordMode == PasswordMode.None)
            {
                args.Add("-o"); args.Add("BatchMode=yes");
            }
            else
            {
                args.Add("-o"); args.Add("PreferredAuthentications=password,keyboard-interactive");
                args.Add("-o"); args.Add("PubkeyAuthentication=no");
            }
            args.Add("-o"); args.Add($"StrictHostKeyChecking={_options.StrictHostKeyChecking}");
            if (_options.ServerAliveInterval > 0)
            {
                args.Add("-o"); args.Add($"ServerAliveInterval={_options.ServerAliveInterval}");
            }
            if (!string.IsNullOrEmpty(_options.UserKnownHostsFile))
            {
                args.Add("-o"); args.Add($"UserKnownHostsFile={_options.UserKnownHostsFile}");
            }
            if (!string.IsNullOrEmpty(_options.IdentityFile))
            {
                args.Add("-i"); args.Add(_options.IdentityFile!);
                args.Add("-o"); args.Add("IdentitiesOnly=yes");
            }
            foreach (var extra in _options.ExtraArgs)
            {
                args.Add(extra);
            }
        }

        private Process Start(List<string> sshArgs, bool usePasswordAuth)
        {
            ProcessStartInfo psi;
            if (usePasswordAuth && _passwordMode == PasswordMode.SshPass && _sshPassPath != null && _passwordFile != null)
            {
                var wrapped = new List<string> { "-f", _passwordFile, _sshPath };
                wrapped.AddRange(sshArgs);
                psi = BuildPsi(_sshPassPath, wrapped, redirect: true);
            }
            else
            {
                psi = BuildPsi(_sshPath, sshArgs, redirect: true);
                if (usePasswordAuth && _passwordMode == PasswordMode.AskPass && _askPassHelperPath != null)
                {
                    psi.Environment["SSH_ASKPASS"] = _askPassHelperPath;
                    psi.Environment["SSH_ASKPASS_REQUIRE"] = "force";
                    // Some ssh builds still require DISPLAY to be set to invoke askpass.
                    if (!psi.Environment.ContainsKey("DISPLAY"))
                        psi.Environment["DISPLAY"] = "dummy:0";
                }
            }
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            if (!process.Start())
                throw new SshProcessException("Failed to start ssh process.");
            return process;
        }

        private static ProcessStartInfo BuildPsi(string fileName, IList<string> args, bool redirect)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = redirect,
                RedirectStandardOutput = redirect,
                RedirectStandardError = redirect,
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

        private static string? ResolveExecutable(string? explicitPath, string command, bool isWindows)
        {
            if (!string.IsNullOrEmpty(explicitPath))
            {
                if (File.Exists(explicitPath)) return explicitPath;
                throw new SshProcessException($"Executable not found: {explicitPath}");
            }

            // Prefer the built-in OpenSSH client on Windows.
            if (isWindows && command.Equals("ssh", StringComparison.OrdinalIgnoreCase))
            {
                var builtin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
                if (File.Exists(builtin)) return builtin;
            }

            var locator = isWindows ? "where" : "which";
            var probe = isWindows ? command + ".exe" : command;
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

        private static string CreatePasswordFile(string password, bool isWindows)
        {
            var path = Path.Combine(Path.GetTempPath(), $"tqk-sshcli-pw-{Guid.NewGuid():N}");
            // sshpass expects the password followed by a newline; askpass reads until newline.
            File.WriteAllText(path, password + "\n");
            try
            {
                if (!isWindows)
                {
                    var chmod = Process.Start(new ProcessStartInfo("chmod", $"600 {path}") { UseShellExecute = false, CreateNoWindow = true });
                    chmod?.WaitForExit(2000);
                }
            }
            catch { }
            return path;
        }

        private static string CreateAskPassHelper(string passwordFile)
        {
            // A minimal .cmd that prints the password file contents to stdout.
            // ssh invokes SSH_ASKPASS and reads the first line as the password.
            var path = Path.Combine(Path.GetTempPath(), $"tqk-sshcli-askpass-{Guid.NewGuid():N}.cmd");
            var content = "@echo off\r\ntype \"" + passwordFile + "\"\r\n";
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (_passwordFile != null)
            {
                try { File.Delete(_passwordFile); } catch { }
            }
            if (_askPassHelperPath != null)
            {
                try { File.Delete(_askPassHelperPath); } catch { }
            }
            if (ControlSocketPath != null)
            {
                try { File.Delete(ControlSocketPath); } catch { }
            }
        }
    }
}
