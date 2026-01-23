
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Esp32EmuConsole;

public class Initializer
{
    private readonly string _workingDirectory;
    private readonly string _templateDirectory;
    private readonly string[] _files = new[] { "package.json", "vite.config.js", "rules.json" };

    public Initializer(string workingDirectory, string templateBaseDirectory)
    {
        _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        _templateDirectory = templateBaseDirectory ?? throw new ArgumentNullException(nameof(templateBaseDirectory));
    }

    public void EnsureConfigFiles()
    {
        foreach (var f in _files)
        {
            var dest = Path.Combine(_workingDirectory, f);
            if (File.Exists(dest)) continue;

            var src = Path.Combine(_templateDirectory, f);
            if (!File.Exists(src))
            {
                Console.WriteLine($"Template file not found: {src}. Skipping copy for {f}.");
                continue;
            }

            try
            {
                File.Copy(src, dest);
                Console.WriteLine($"Copied {f} to working directory.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to copy {f} from template: {ex.Message}");
            }
        }
    }
    public void KillProcessUsingPort(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.Contains($":{port} ")) continue;
                var tokens = Regex.Split(line.Trim(), "\\s+");
                if (tokens.Length < 5) continue;
                if (!int.TryParse(tokens[^1], out var pid)) continue;
                if (pid == Process.GetCurrentProcess().Id) continue;
                if (pid == 0) continue;
                try
                {
                    var victim = Process.GetProcessById(pid);
                    victim.Kill(entireProcessTree: true);
                    Console.WriteLine($"Killed process {pid} listening on port {port}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to kill process {pid} on port {port}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Port cleanup failed: {ex.Message}");
        }
    }
}
