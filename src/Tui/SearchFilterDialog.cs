using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Esp32EmuConsole.Tui;

/// <summary>Immutable state representing the active search/filter criteria for log views.</summary>
public record LogFilterState(string SearchText, LogLevel? MinLevel)
{
    public static readonly LogFilterState Empty = new(string.Empty, null);

    public bool IsActive => !string.IsNullOrEmpty(SearchText) || MinLevel.HasValue;

    /// <summary>Returns true if the given log line passes this filter.</summary>
    public bool Matches(string line)
    {
        if (MinLevel.HasValue && !LineMatchesLevel(line, MinLevel.Value))
            return false;

        if (!string.IsNullOrEmpty(SearchText) &&
            !line.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool LineMatchesLevel(string line, LogLevel minLevel)
    {
        // Log lines are formatted as "[LogLevel] ..." by InMemoryLogger.
        foreach (LogLevel lvl in Enum.GetValues<LogLevel>())
        {
            if (lvl == LogLevel.None) continue;
            if (lvl >= minLevel && line.StartsWith($"[{lvl}]", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Presents a modal dialog that lets the user set a text search filter and a minimum log level
/// filter for the log views. Returns the new <see cref="LogFilterState"/>, or <c>null</c> if the
/// user cancelled.
/// </summary>
internal static class SearchFilterDialog
{
    private static readonly string[] s_levelLabels =
    {
        "All levels",
        "Trace+",
        "Debug+",
        "Information+",
        "Warning+",
        "Error+",
        "Critical only",
    };

    private static readonly LogLevel?[] s_levelValues =
    {
        null,
        LogLevel.Trace,
        LogLevel.Debug,
        LogLevel.Information,
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Critical,
    };

    /// <summary>
    /// Shows the search/filter dialog.
    /// </summary>
    /// <param name="app">Current <see cref="IApplication"/> instance (required to run the dialog modally).</param>
    /// <param name="current">The currently active filter to pre-populate the dialog.</param>
    /// <returns>
    /// The new <see cref="LogFilterState"/> when the user clicks Apply, or <c>null</c> when cancelled.
    /// </returns>
    public static LogFilterState? Show(IApplication app, LogFilterState current)
    {
        var dialog = new Dialog
        {
            Title = "Search / Filter Logs",
            Width = 62,
            Height = 13,
        };

        // -- Search text row --
        var searchLabel = new Label
        {
            Text = "Search text:",
            X = 1,
            Y = 1,
        };
        var searchField = new TextField
        {
            Text = current.SearchText,
            X = 14,
            Y = 1,
            Width = Dim.Fill(2),
        };

        // -- Minimum log level row --
        var levelLabel = new Label
        {
            Text = "Min log level:",
            X = 1,
            Y = 3,
        };

        // Determine which level index is currently selected.
        int currentLevelIndex = 0;
        if (current.MinLevel.HasValue)
        {
            var idx = Array.IndexOf(s_levelValues, current.MinLevel.Value);
            if (idx >= 0)
                currentLevelIndex = idx;
        }

        // Build radio-style CheckBoxes, one per level label.
        var levelCheckBoxes = new CheckBox[s_levelLabels.Length];
        for (int i = 0; i < s_levelLabels.Length; i++)
        {
            var cb = new CheckBox
            {
                Text = s_levelLabels[i],
                RadioStyle = true,
                Value = i == currentLevelIndex ? CheckState.Checked : CheckState.UnChecked,
                X = 14,
                Y = 3 + i,
            };

            // Enforce mutual exclusivity: deselect all others when one is checked.
            cb.ValueChanging += (_, args) =>
            {
                if (args.NewValue == CheckState.Checked)
                {
                    foreach (var other in levelCheckBoxes)
                    {
                        if (other != null && other != cb && other.Value == CheckState.Checked)
                            other.Value = CheckState.UnChecked;
                    }
                }
            };

            levelCheckBoxes[i] = cb;
        }

        dialog.Add(searchLabel, searchField, levelLabel);
        foreach (var cb in levelCheckBoxes)
            dialog.Add(cb);

        // -- Buttons --
        var cancelBtn = new Button { Title = "_Cancel" };
        var applyBtn = new Button { Title = "_Apply" };

        dialog.AddButton(cancelBtn);
        dialog.AddButton(applyBtn);

        app.Run(dialog);

        // Result is null when ESC/Cancel is pressed; 1 when Apply (second button) is pressed.
        if (dialog.Result != 1)
            return null;

        // Determine selected level.
        int selectedLevelIndex = 0;
        for (int i = 0; i < levelCheckBoxes.Length; i++)
        {
            if (levelCheckBoxes[i].Value == CheckState.Checked)
            {
                selectedLevelIndex = i;
                break;
            }
        }

        LogLevel? selectedLevel = s_levelValues[selectedLevelIndex];
        return new LogFilterState(searchField.Text ?? string.Empty, selectedLevel);
    }
}
