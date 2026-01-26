using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Services;

public class Vite : IDisposable
{
    private Process? _proc;
    private IntPtr _jobHandle = IntPtr.Zero;
    private readonly ILogger<Vite> _logger;
    private readonly ILogger _loggerVite;
    private readonly string _working_directory;
    private readonly string _url;
    private bool _started;
    public string Url => _url;

    public Vite(string workingDirectory, ILogger<Vite> logger, ILoggerFactory loggerFactory, string url = "http://localhost:5173")
    {
        _working_directory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        _url = url;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));
        _loggerVite = loggerFactory.CreateLogger("vite");
        _started = false;
    }

    public void Start()
    {
        if (_started) return;

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c npm run dev",
            WorkingDirectory = _working_directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Starting Vite process in {WorkingDirectory}", _working_directory);

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
            _logger.LogError(ex, "Failed to assign Vite process to job object");
        }

        _proc.OutputDataReceived += (_, e) => { if (e.Data is not null) _loggerVite.LogInformation(e.Data); };
        _proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) _loggerVite.LogError(e.Data); };
        _proc.BeginOutputReadLine();
        _proc.BeginErrorReadLine();

        _started = true;
    }

    public async Task<bool> WaitForViteAsync(HttpClient http, string url, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                using var resp = await http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Vite is ready at {Url}", url);
                    return true;
                }
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
            if (_proc is not null && !_proc.HasExited)
            {
                _proc.CloseMainWindow();
                _proc.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Vite process");
        }
        finally
        {
            if (_jobHandle != IntPtr.Zero)
            {
                CloseHandle(_jobHandle);
            }
            GC.SuppressFinalize(this);
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
