using Terminal.Gui;

namespace Esp32EmuConsole.Tui;


public class TUI
{
    private readonly ILogger<TUI> _logger;
    private readonly IServiceProvider _services;

    public TUI(IServiceProvider serviceProvider)
    {
        _services = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = _services.GetRequiredService<ILogger<TUI>>();
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
            Height = Dim.Fill()
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


        var label = new Label("Press ANY key... (Ctrl+C to quit)")
        {
            X = Pos.Center(),
            Y = Pos.Center()
        };

        win.Add(label);

        top.KeyPress += (e) =>
        {
            label.Text = $"Key detected on top: {e.KeyEvent.Key}";
            Application.Refresh();
            if (e.KeyEvent.Key == (Key.CtrlMask | Key.C))
            {
                Application.RequestStop();
            }
        };

        win.KeyPress += (e) =>
        {
            label.Text = $"Key detected on win: {e.KeyEvent.Key}";
            Application.Refresh();
            if (e.KeyEvent.Key == (Key.CtrlMask | Key.C))
            {
                Application.RequestStop();
            }
        };

        menu.KeyPress += (e) =>
        {
            label.Text = $"Key detected on menu: {e.KeyEvent.Key}";
            Application.Refresh();
            if (e.KeyEvent.Key == (Key.CtrlMask | Key.C))
            {
                Application.RequestStop();
            }
        };

        Application.Run();
        Application.Shutdown();

        _logger.LogInformation("TUI exited.");
    }
}