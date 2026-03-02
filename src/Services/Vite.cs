using System;
using System.Diagnostics;
using System.Net.Http;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Services;

/// <summary>
/// Manages the lifecycle of the Vite dev-server child process (<c>npm run dev</c>).
/// <list type="bullet">
///   <item>Spawns the process via Win32 <c>CreateProcess</c> with inherited pipes for stdout/stderr.</item>
///   <item>Assigns the child to a Windows Job Object so it is killed automatically when the parent exits.</item>
///   <item>Routes Vite console output to the in-memory logging system (category: <c>"vite"</c>).</item>
///   <item><see cref="WaitForViteAsync"/> polls the Vite HTTP endpoint until it is ready or times out.</item>
/// </list>
/// <b>Windows only.</b> Uses P/Invoke for <c>kernel32.dll</c> APIs.
/// </summary>
public class Vite : IDisposable
{
    private Process? _proc;
    private IntPtr _jobHandle = IntPtr.Zero;
    private readonly ILogger<Vite> _logger;
    private readonly ILogger _loggerVite;
    private readonly string _working_directory;
    private readonly string _url;
    private CancellationTokenSource? _cts;
    private bool _started;
    private Channel<(bool IsError, string Line)>? _logChannel;
    private Task? _logPumpTask;
    private static readonly HttpClient _httpClient = new();

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

        _logger.LogInformation("Starting Vite process in {WorkingDirectory}", _working_directory);

        // Create inheritable pipes for stdout and stderr and start the child with those handles
        var sa = new NativeMethods.SECURITY_ATTRIBUTES();
        sa.nLength = Marshal.SizeOf<NativeMethods.SECURITY_ATTRIBUTES>();
        sa.bInheritHandle = true;
        sa.lpSecurityDescriptor = IntPtr.Zero;

        var saPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.SECURITY_ATTRIBUTES>());
        try
        {
            Marshal.StructureToPtr(sa, saPtr, false);

            if (!NativeMethods.CreatePipe(out var parentStdOutRead, out var childStdOutWrite, saPtr, 0))
                throw new InvalidOperationException("CreatePipe stdout failed: " + Marshal.GetLastWin32Error());
            if (!NativeMethods.CreatePipe(out var parentStdErrRead, out var childStdErrWrite, saPtr, 0))
                throw new InvalidOperationException("CreatePipe stderr failed: " + Marshal.GetLastWin32Error());

            // Parent should not inherit the read handles
            const uint HANDLE_FLAG_INHERIT = 0x00000001;
            NativeMethods.SetHandleInformation(parentStdOutRead, HANDLE_FLAG_INHERIT, 0);
            NativeMethods.SetHandleInformation(parentStdErrRead, HANDLE_FLAG_INHERIT, 0);

            var si = new NativeMethods.STARTUPINFO();
            si.cb = (uint)Marshal.SizeOf<NativeMethods.STARTUPINFO>();
            si.dwFlags = NativeMethods.STARTF_USESTDHANDLES;
            si.hStdOutput = childStdOutWrite;
            si.hStdError = childStdErrWrite;
            si.hStdInput = IntPtr.Zero;

            var pi = new NativeMethods.PROCESS_INFORMATION();

            var cmdLine = new System.Text.StringBuilder("cmd.exe /c npm run dev");

            var created = NativeMethods.CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, true,
                NativeMethods.CREATE_NO_WINDOW, IntPtr.Zero, _working_directory, ref si, out pi);

            // Close child-side pipe handles in parent now
            NativeMethods.CloseHandle(childStdOutWrite);
            NativeMethods.CloseHandle(childStdErrWrite);

            if (!created)
            {
                // clean up parent handles
                NativeMethods.CloseHandle(parentStdOutRead);
                NativeMethods.CloseHandle(parentStdErrRead);
                throw new InvalidOperationException("CreateProcess failed: " + Marshal.GetLastWin32Error());
            }

            _cts = new CancellationTokenSource();

            // Create a channel and pump logs to the in-memory logger without touching the console
            _logChannel = Channel.CreateUnbounded<(bool, string)>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

            _logPumpTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in _logChannel.Reader.ReadAllAsync(_cts.Token))
                    {
                        try
                        {
                            if (item.IsError)
                                _loggerVite.LogError(item.Line);
                            else
                                _loggerVite.LogInformation(item.Line);
                        }
                        catch { }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Vite log pump failed");
                }
            });

            // Start background readers for the parent read handles
            Task.Run(() => ReadPipeAndEnqueue(parentStdOutRead, false, _cts.Token));
            Task.Run(() => ReadPipeAndEnqueue(parentStdErrRead, true, _cts.Token));

            // Wrap native process with a managed Process instance for later Kill/HasExited checks
            _proc = Process.GetProcessById(pi.dwProcessId);

            // Close thread handle from PROCESS_INFORMATION
            NativeMethods.CloseHandle(pi.hThread);
            // close the process handle returned in PROCESS_INFORMATION (Process.GetProcessById has its own handle)
            NativeMethods.CloseHandle(pi.hProcess);
        }
        finally
        {
            Marshal.FreeHGlobal(saPtr);
        }

        try
        {
            // Create a job object so the child will be killed if this process exits abruptly
            _jobHandle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            var length = Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            var ptr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                NativeMethods.SetInformationJobObject(_jobHandle, NativeMethods.JobObjectInfoType.ExtendedLimitInformation, ptr, (uint)length);
            }
            finally { Marshal.FreeHGlobal(ptr); }

            NativeMethods.AssignProcessToJobObject(_jobHandle, _proc.Handle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign Vite process to job object");
        }

        // legacy managed redirection removed - using native pipes and background readers

        _started = true;
    }

    // Native-pipe based readers are used (see ReadPipeAndEnqueue)

    private void ReadPipeAndEnqueue(IntPtr readHandle, bool isError, CancellationToken ct)
    {
        try
        {
            using var safe = new SafeFileHandle(readHandle, ownsHandle: true);
            // Pipes were opened for synchronous I/O; use a synchronous FileStream
            using var fs = new FileStream(safe, FileAccess.Read, 4096, isAsync: false);
            using var sr = new StreamReader(fs);

            while (!ct.IsCancellationRequested)
            {
                var line = sr.ReadLine();
                if (line is null) break;
                if (_logChannel is not null) _logChannel.Writer.TryWrite((isError, line));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error reading pipe");
        }
    }

    public async Task<bool> WaitForViteAsync(TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                using var resp = await _httpClient.GetAsync(_url);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Vite is ready at {Url}", _url);
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
            _cts?.Cancel();

            // shut down the log pump

            try
            {
                if (_logChannel is not null)
                {
                    _logChannel.Writer.TryComplete();
                }
                if (_logPumpTask is not null)
                {
                    // give it brief time to finish
                    _logPumpTask.Wait(2000);
                }
            }
            catch { }

            if (_proc is not null && !_proc.HasExited)
            {
                try
                {
                    _proc.CloseMainWindow();
                }
                catch { }
                try
                {
                    _proc.Kill(entireProcessTree: true);
                }
                catch { }
            }

            _proc?.Dispose();
            _cts?.Dispose();
            _logger.LogInformation("Vite process stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop Vite process");
        }
        finally
        {
            if (_jobHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_jobHandle);
            }
            GC.SuppressFinalize(this);
        }
    }

    // Native interop tucked into a nested helper for readability
    private static class NativeMethods
    {
        public const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        public const uint STARTF_USESTDHANDLES = 0x00000100;
        public const uint CREATE_NO_WINDOW = 0x08000000;

        public enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public uint cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(string lpApplicationName, System.Text.StringBuilder lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
            bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
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
        public struct IO_COUNTERS
        {
            public UInt64 ReadOperationCount;
            public UInt64 WriteOperationCount;
            public UInt64 OtherOperationCount;
            public UInt64 ReadTransferCount;
            public UInt64 WriteTransferCount;
            public UInt64 OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    }
}
