using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    /// <summary>
    /// A Windows job object that kills every process assigned to it once its handle closes, so a
    /// subprocess cannot outlive the process that spawned it.
    /// </summary>
    /// <remarks>
    /// Killing the child from <c>Dispose</c> only covers the orderly exit. End Task, an unhandled
    /// exception, a debugger stop — none of them run finally blocks, and the child keeps running
    /// with its tunnel up and its port held for as long as the machine is. The kernel does close
    /// every handle of a dying process though, and <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c> turns
    /// that into the guarantee managed code cannot make for itself.
    ///
    /// Off Windows, and whenever the kernel refuses us a job, this is inert: the caller keeps its
    /// own kill-on-dispose path, which is what it had before, so nothing needs a platform branch.
    /// </remarks>
    internal sealed class KillOnCloseJobObject : IDisposable
    {
        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        private IntPtr _handle;

        public KillOnCloseJobObject()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            IntPtr handle;
            try { handle = CreateJobObject(IntPtr.Zero, null); }
            catch (DllNotFoundException) { return; }
            catch (EntryPointNotFoundException) { return; }
            if (handle == IntPtr.Zero) return;

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, buffer, false);
                if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, buffer, (uint)length))
                {
                    CloseHandle(handle);
                    return;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            _handle = handle;
        }

        /// <summary>
        /// Puts <paramref name="process"/> under the job. Best effort: a failure leaves the child
        /// exactly as unmanaged as it was before, which the caller already handles.
        /// </summary>
        public bool Assign(Process process)
        {
            IntPtr job = _handle;
            if (job == IntPtr.Zero) return false;
            try { return AssignProcessToJobObject(job, process.Handle); }
            catch { return false; }
        }

        public void Dispose()
        {
            IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
            if (handle != IntPtr.Zero)
            {
                try { CloseHandle(handle); } catch { }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            IntPtr hJob, int jobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
