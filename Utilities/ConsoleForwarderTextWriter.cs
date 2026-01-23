using System.Text;

namespace Esp32EmuConsole.Utilities;

public class ConsoleForwarderTextWriter : TextWriter
{
    private readonly TextWriter _original;
    private readonly Services.LogBuffer _buffer;
    private readonly string _prefix;

    public ConsoleForwarderTextWriter(TextWriter original, Services.LogBuffer buffer, string? prefix = null)
    {
        _original = original ?? throw new ArgumentNullException(nameof(original));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _prefix = prefix ?? string.Empty;
    }

    public override Encoding Encoding => _original.Encoding;

    public override void Write(char value)
    {
        _original.Write(value);
    }

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            // forward each line separately so UI gets nice chunks
            var lines = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines)
            {
                _buffer.Push(_prefix + l);
            }
        }
        _original.Write(value);
    }

    public override void WriteLine(string? value)
    {
        if (value is not null)
        {
            _buffer.Push(_prefix + value);
        }
        _original.WriteLine(value);
    }
}
