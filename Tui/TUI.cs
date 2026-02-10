using Terminal.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Esp32EmuConsole.Utilities;

namespace Esp32EmuConsole.Tui;


public class TUI
{
    private readonly ILogger<TUI> _logger;
    private readonly IServiceProvider _services;
    private readonly LogBuffer _logBuffer;

    public TUI(IServiceProvider serviceProvider)
    {
        _services = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = _services.GetRequiredService<ILogger<TUI>>();
        _logBuffer = _services.GetService<LogBuffer>();
    }

    public void Run()
    {
        Application.Init();

        var top = Application.Top;

        var win = new Window("Esp32EmuConsole TUI")
        {
            X = 0,
            Y = 2,

            Width = Dim.Fill(),
            Height = Dim.Fill()-1
        };

        top.Add(win);

        var menu = new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("(use Alt)", new MenuItem[0]),
            new MenuBarItem("_File", new MenuItem[]
            {
                new MenuItem("_Quit", "", () => { Application.RequestStop(); })
            }),
            new MenuBarItem("_Edit", new MenuItem[]
            {
                new MenuItem("_Settings", "", () =>
                {
                    MessageBox.Query(50, 7, "Settings", "Settings dialog not implemented yet.", "Ok");
                }),
                new MenuItem("Static _Responses", "", () =>
                {
                    MessageBox.Query(50, 7, "Static Responses", "Static Responses dialog not implemented yet.", "Ok");
                })
            }),
            new MenuBarItem("_View", new MenuItem[]
            {
                new MenuItem("[x]_App logs", "", () => {}),
                new MenuItem("[]_Http traffic", "", () => {}),
                new MenuItem("[]_WebSocket traffic", "", () => {})
            }),
            new MenuBarItem("_Help", new MenuItem[]
            {
                new MenuItem("_About", "", () =>
                {
                    MessageBox.Query(50, 7, "About Esp32EmuConsole", "Esp32EmuConsole TUI\nVersion 1.0.0", "Ok");
                })
            })
        })
        {
            Y = 1
        };

        top.Add(menu);

        var topLabel = new Label("Terminal.Gui Application - Press Alt for Menu")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            ColorScheme = new ColorScheme()
            {
                Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray)
            }
        };
        top.Add(topLabel);


        var textView = new TextView()
        {
            ReadOnly = true,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        if (_logBuffer is null)
        {
            textView.Text = "No LogBuffer registered in services.";
            return;
        }

        // Populate initial snapshot
        var snapshot = _logBuffer.Snapshot();
        if (snapshot.Length > 0)
            textView.Text = string.Join(Environment.NewLine, snapshot);

        // Event handler to append new log lines on the GUI thread
        Action<string> handler = (line) =>
        {
            Application.MainLoop.Invoke(() =>
            {
                var cur = textView.Text?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(cur))
                    textView.Text = line;
                else
                    textView.Text = cur + Environment.NewLine + line;
                textView.MoveEnd();
                Application.Refresh();
            });
        };

        _logBuffer.NewLog += handler;

        win.Add(textView);

        var label = new Label("Keypress: ")
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            ColorScheme = new ColorScheme()
            {
                Normal = Application.Driver.MakeAttribute(Color.Black, Color.Gray)
            }
        };

        top.Add(label);

        // textView.SetFocus();

        top.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == (Key.CtrlMask | Key.C))
            {
                e.Handled = true;
                Application.RequestStop();
            }
            label.Text = $"Keypress (top): {e.KeyEvent.Key}";
            Application.Refresh();
        };

        textView.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.G)
            {
                e.Handled = true;
                MessageBox.Query(50, 7, "Key Pressed", "You pressed 'G' in the TextView!", "Ok");
            }
            label.Text = $"Keypress (textView): {e.KeyEvent.Key}";
        };

        Application.Run();
        Application.Shutdown();

        _logger.LogInformation("TUI exited.");
    }

}