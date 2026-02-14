using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace Esp32EmuConsole.Tests;

public class InMemmoryLoggerTest
{
    private readonly Utilities.InMemoryLoggerProvider _providerMatchall;
    private readonly Utilities.InMemoryLoggerProvider _providerFallback;
    private readonly Utilities.InMemoryLoggerProvider _providerLevels;
    private readonly Utilities.LogBuffer _buf1 = new();
    private readonly Utilities.LogBuffer _buf2 = new();
    private readonly Utilities.LogBuffer _buf3 = new();
    private readonly Utilities.LogBuffer _buf4 = new();
    private readonly Utilities.LogBuffer _buf5 = new();
    private readonly Utilities.LogBuffer _buf6 = new();
    private readonly StringWriter _writer = new();

    public InMemmoryLoggerTest()
    {
        _providerMatchall = new Utilities.InMemoryLoggerProvider( new[]
        {
            new Utilities.LogRoute("TestCategory1, TestCategory1.1", LogLevel.Trace, Utilities.LogFormat.Full, _buf1), // Route with exact match
            new Utilities.LogRoute("TestCategory2*", LogLevel.Trace, Utilities.LogFormat.Full, _buf2), // Route with wildcard match
            new Utilities.LogRoute("*", LogLevel.Trace, Utilities.LogFormat.Full, _buf3), // Catch all route
            new Utilities.LogRoute("fallback", LogLevel.Trace, Utilities.LogFormat.Full, _buf4), // Fallback route - nothing goes here
            new Utilities.LogRoute("TestCategoryWithWriter", LogLevel.Trace, Utilities.LogFormat.Full, null, _writer), // Route with writer
        });
        _providerFallback = new Utilities.InMemoryLoggerProvider( new[]
        {
            new Utilities.LogRoute("TestCategory1, TestCategory1.1", LogLevel.Trace, Utilities.LogFormat.Full, _buf1), // Route with exact match
            new Utilities.LogRoute("TestCategory2*", LogLevel.Trace, Utilities.LogFormat.Full, _buf2), // Route with wildcard match
            new Utilities.LogRoute("fallback", LogLevel.Trace, Utilities.LogFormat.Full, _buf3), // Fallback route - should only get unmatched categories
        });
        _providerLevels = new Utilities.InMemoryLoggerProvider( new[]
        {
            new Utilities.LogRoute("TestCategoryCritical", LogLevel.Critical, Utilities.LogFormat.Full, _buf1),
            new Utilities.LogRoute("TestCategoryError", LogLevel.Error, Utilities.LogFormat.Full, _buf2),
            new Utilities.LogRoute("TestCategoryWarning", LogLevel.Warning, Utilities.LogFormat.Full, _buf3),
            new Utilities.LogRoute("TestCategoryInformation", LogLevel.Information, Utilities.LogFormat.Full, _buf4),
            new Utilities.LogRoute("TestCategoryDebug", LogLevel.Debug, Utilities.LogFormat.Full, _buf5),
            new Utilities.LogRoute("TestCategoryTrace", LogLevel.Trace, Utilities.LogFormat.Full, _buf6),
        });
    }

    [Theory]
    [InlineData("TestCategory1", new int[] { 0, 2 })]
    [InlineData("TestCategory1.1", new int[] { 0, 2 })]
    [InlineData("TestCategory2", new int[] { 1, 2 })]
    [InlineData("TestCategory2.Sub", new int[] { 1, 2 })]
    [InlineData("UnmatchedCategory", new int[] { 2 })] // Should only go to catch-all buffer, not the specific buffers or fallback buffer
    [InlineData("fallback", new int[] { 2 })] // Even though "fallback" matches the fallback route, it also matches the catch-all route, so should go to that buffer, not the fallback buffer
    [InlineData("TestCategoryWithWriter", new int[] { 2 })] // Should only go to writer, not buffers
    public void TestCategoryMatchingMatchAll(string category, int[] expectedBufferIndex)
    {
        var logger = _providerMatchall.CreateLogger(category);
        logger.LogInformation($"Log message for {category}");

        var buffers = new[] { _buf1, _buf2, _buf3, _buf4, _buf5, _buf6 };
        for (int i = 0; i < buffers.Length; i++)
        {
            var found = buffers[i].Snapshot().Any(log => log.Contains($"Log message for {category}"));
            if (expectedBufferIndex.Contains(i))
            {
                Assert.True(found, $"Expected buffer {i} from matchall provider to contain log for category '{category}'");
            }
            else
            {
                Assert.False(found, $"Did not expect buffer {i} from matchall provider to contain log for category '{category}'");
            }
        }
    }

        [Theory]
    [InlineData("TestCategory1", 0)]
    [InlineData("TestCategory1.1", 0)]
    [InlineData("TestCategory2", 1)]
    [InlineData("TestCategory2.Sub", 1)]
    [InlineData("UnmatchedCategory", 2)]
    [InlineData("fallback", 2)]
    [InlineData("TestCategoryWithWriter", 2)]
    public void TestCategoryMatchingFallback(string category, int expectedBufferIndex)
    {
        var logger = _providerFallback.CreateLogger(category);
        logger.LogInformation($"Log message for {category}");

        var buffers = new[] { _buf1, _buf2, _buf3, _buf4, _buf5, _buf6 };
        for (int i = 0; i < buffers.Length; i++)
        {
            var found = buffers[i].Snapshot().Any(log => log.Contains($"Log message for {category}"));
            if (i == expectedBufferIndex)
            {
                Assert.True(found, $"Expected buffer {i} from fallback provider to contain log for category '{category}'");
            }
            else
            {
                Assert.False(found, $"Did not expect buffer {i} from fallback provider to contain log for category '{category}'");
            }
        }
    }

    [Theory]
    [InlineData("TestCategoryCritical", LogLevel.Critical, 0)]
    [InlineData("TestCategoryError", LogLevel.Error, 1)]
    [InlineData("TestCategoryWarning", LogLevel.Warning, 2)]
    [InlineData("TestCategoryInformation", LogLevel.Information, 3)]
    [InlineData("TestCategoryDebug", LogLevel.Debug, 4)]
    [InlineData("TestCategoryTrace", LogLevel.Trace, 5)]
    public void TestLogLevelFiltering(string category, LogLevel level, int expectedBufferIndex)
    {
        var logger = _providerLevels.CreateLogger(category);
        for (LogLevel lvl = LogLevel.Trace; lvl <= LogLevel.None; lvl++)
        {
            logger.Log(lvl, $"Log message for {category} at level {lvl}");

            var buffers = new[] { _buf1, _buf2, _buf3, _buf4, _buf5, _buf6 };
            for (int i = 0; i < buffers.Length; i++)
            {
                var found = buffers[i].Snapshot().Any(log => log.Contains($"Log message for {category} at level {lvl}"));
                if (i == expectedBufferIndex && lvl >= level)
                {
                    Assert.True(found, $"Expected buffer {i} from levels provider to contain log for category '{category}' at level '{lvl}'");
                }
                else
                {
                    Assert.False(found, $"Did not expect buffer {i} from levels provider to contain log for category '{category}' at level '{lvl}'");
                }
            }
        }
    }

    [Theory]
    [InlineData("TestCategoryCritical", LogLevel.Critical)]
    [InlineData("TestCategoryError", LogLevel.Error)]
    [InlineData("TestCategoryWarning", LogLevel.Warning)]
    [InlineData("TestCategoryInformation", LogLevel.Information)]
    [InlineData("TestCategoryDebug", LogLevel.Debug)]
    [InlineData("TestCategoryTrace", LogLevel.Trace)]
    public void TestLogLevelIsEnabled(string category, LogLevel level)
    {
        var logger = _providerLevels.CreateLogger(category);
        for (LogLevel lvl = LogLevel.Trace; lvl <= LogLevel.None; lvl++)
        {
            var isEnabled = logger.IsEnabled(lvl);
            if (lvl >= level)
            {
                Assert.True(isEnabled, $"Expected IsEnabled to return true for category '{category}' at level '{lvl}'");
            }
            else
            {
                Assert.False(isEnabled, $"Expected IsEnabled to return false for category '{category}' at level '{lvl}'");
            }
        }
    }

    [Fact]
    public void TestBufferBehavior()
    {
        _buf1.Clear();
        _buf1.Push("First log message");
        _buf1.Push("Second log message");

        var snapshot = _buf1.Snapshot();
        Assert.Equal(2, snapshot.Length);
        Assert.Contains(snapshot, log => log.Contains("First log message"));
        Assert.Contains(snapshot, log => log.Contains("Second log message"));

        _buf1.Clear();
        snapshot = _buf1.Snapshot();
        Assert.Empty(snapshot);
    }

    [Fact]
    public void TestWriterOutput()
    {
        var category = "TestCategoryWithWriter";
        var logger = _providerMatchall.CreateLogger(category);
        logger.LogInformation($"Log message for {category}");
        var output = _writer.ToString();
        var found = output.Contains($"Log message for {category}");
        Assert.True(found, $"Expected writer to contain log for category '{category}'");
    }
}
