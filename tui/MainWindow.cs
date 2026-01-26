using System.Collections.ObjectModel;
using Terminal.Gui;

namespace Esp32EmuConsole.Tui;

// Minimal TUI skeleton: top status line, middle tabs, bottom status bar.
// Logs tab shows two list panes (HTTP and WS). You can switch to tabs-only
// or add more panes later. The timer shows where to drain your LogBuffer.
internal sealed class MainWindow : Window
{
    private readonly ILogger _logger;

    // UI elements we’ll update
    private readonly Label _topStatus;
    private readonly ListView _logList;

    // In-memory buffers for display (ring buffer pattern recommended later)
    private readonly ObservableCollection<string> _logItems = new();

    public MainWindow(/*ILogger logger*/)
    {
        //_logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // _app = App;

        Title = "Esp32EmuConsole";

    }
}