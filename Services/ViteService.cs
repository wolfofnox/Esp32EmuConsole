using System.Diagnostics;
using System.Net.Http;

namespace Esp32EmuConsole;

public class ViteService : IDisposable
{
    private readonly Process _proc;
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
    }
}
