using System;
using System.IO;

namespace Esp32EmuConsole;

public class ConfigInitializer
{
    private readonly string _workingDirectory;
    private readonly string _templateDirectory;
    private readonly string[] _files = new[] { "package.json", "vite.config.js", "rules.json" };

    public ConfigInitializer(string workingDirectory, string templateBaseDirectory)
    {
        _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        _templateDirectory = templateBaseDirectory ?? throw new ArgumentNullException(nameof(templateBaseDirectory));
    }

    public void EnsureFiles()
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
}
