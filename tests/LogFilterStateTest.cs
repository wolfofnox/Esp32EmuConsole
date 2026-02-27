using Microsoft.Extensions.Logging;
using Esp32EmuConsole.Tui;

namespace Esp32EmuConsole.Tests;

public class LogFilterStateTest
{
    // ── LogFilterState.Empty ───────────────────────────────────────────────

    [Fact]
    public void Empty_IsNotActive()
    {
        Assert.False(LogFilterState.Empty.IsActive);
    }

    [Theory]
    [InlineData("[Information] SomeCategory: some message")]
    [InlineData("[Warning]: a message without category")]
    [InlineData("anything at all")]
    public void Empty_MatchesEveryLine(string line)
    {
        Assert.True(LogFilterState.Empty.Matches(line));
    }

    // ── Text-search filter ─────────────────────────────────────────────────

    [Theory]
    [InlineData("hello", "[Information] Cat: hello world", true)]
    [InlineData("HELLO", "[Information] Cat: hello world", true)]   // case-insensitive
    [InlineData("world", "[Information] Cat: hello world", true)]
    [InlineData("xyz",   "[Information] Cat: hello world", false)]
    public void TextSearch_MatchesCaseInsensitive(string searchText, string line, bool expected)
    {
        var filter = new LogFilterState(searchText, null);
        Assert.Equal(expected, filter.Matches(line));
    }

    [Fact]
    public void TextSearch_IsActive()
    {
        var filter = new LogFilterState("foo", null);
        Assert.True(filter.IsActive);
    }

    // ── Level filter ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(LogLevel.Trace,       "[Trace] Cat: msg",       true)]
    [InlineData(LogLevel.Debug,       "[Debug] Cat: msg",       true)]
    [InlineData(LogLevel.Information, "[Information] Cat: msg", true)]
    [InlineData(LogLevel.Warning,     "[Warning] Cat: msg",     true)]
    [InlineData(LogLevel.Error,       "[Error] Cat: msg",       true)]
    [InlineData(LogLevel.Critical,    "[Critical] Cat: msg",    true)]
    public void LevelFilter_MatchesSelf(LogLevel level, string line, bool expected)
    {
        var filter = new LogFilterState(string.Empty, level);
        Assert.Equal(expected, filter.Matches(line));
    }

    [Theory]
    [InlineData(LogLevel.Information, "[Trace] Cat: msg",   false)]   // below min → filtered out
    [InlineData(LogLevel.Information, "[Debug] Cat: msg",   false)]   // below min → filtered out
    [InlineData(LogLevel.Information, "[Warning] Cat: msg", true)]    // above min → passes
    [InlineData(LogLevel.Information, "[Error] Cat: msg",   true)]    // above min → passes
    [InlineData(LogLevel.Information, "[Critical] Cat: msg",true)]    // above min → passes
    public void LevelFilter_FiltersLowerLevels(LogLevel minLevel, string line, bool expected)
    {
        var filter = new LogFilterState(string.Empty, minLevel);
        Assert.Equal(expected, filter.Matches(line));
    }

    [Fact]
    public void LevelFilter_IsActive()
    {
        var filter = new LogFilterState(string.Empty, LogLevel.Warning);
        Assert.True(filter.IsActive);
    }

    [Fact]
    public void LevelFilter_UnrecognisedLine_IsFilteredOut()
    {
        // A line that does not start with a known [Level] token should not pass a level filter.
        var filter = new LogFilterState(string.Empty, LogLevel.Information);
        Assert.False(filter.Matches("this line has no level prefix"));
    }

    // ── Combined filter ────────────────────────────────────────────────────

    [Fact]
    public void Combined_BothConditionsMustPass()
    {
        var filter = new LogFilterState("error code", LogLevel.Warning);

        // Level OK, text matches → passes
        Assert.True(filter.Matches("[Warning] Cat: error code 42"));

        // Level OK, text does NOT match → filtered out
        Assert.False(filter.Matches("[Warning] Cat: all good"));

        // Level too low, text matches → filtered out
        Assert.False(filter.Matches("[Debug] Cat: error code 42"));

        // Both fail → filtered out
        Assert.False(filter.Matches("[Debug] Cat: all good"));
    }
}
