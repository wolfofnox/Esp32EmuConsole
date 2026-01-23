using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace Esp32EmuConsole;

public class ViteService : IDisposable
{
    private readonly Process _proc;
    private readonly IntPtr _jobHandle = IntPtr.Zero;
    public string Url { get; }

    public ViteService(string workingDirectory, string url = "http://localhost:5173")
    {
        Url = url;

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c npm run dev",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Vite process.");

        try
        {
            // Create a job object so the child will be killed if this process exits abruptly
            _jobHandle = CreateJobObject(IntPtr.Zero, null);
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            var ptr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation, ptr, (uint)length);
            }
            finally { Marshal.FreeHGlobal(ptr); }

            AssignProcessToJobObject(_jobHandle, _proc.Handle);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to assign Vite process to job object: {ex.Message}");
        }

        _proc.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine($"[vite] {e.Data}"); };
        _proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine($"[vite:err] {e.Data}"); };
        _proc.BeginOutputReadLine();
        _proc.BeginErrorReadLine();
    }

    public static async Task<bool> WaitForViteAsync(HttpClient http, string url, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                using var resp = await http.GetAsync(url);
                if (resp.IsSuccessStatusCode) return true;
            }
            catch { }
            await Task.Delay(500);
        }
        return false;
    }

    public void Dispose()
    {
        try
        {
            if (!_proc.HasExited)
            {
                _proc.CloseMainWindow();
                _proc.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to stop Vite: {ex.Message}");
        }
        finally
        {
            if (_jobHandle != IntPtr.Zero)
            {
                CloseHandle(_jobHandle);
            }
        }
    }

    const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);
}
