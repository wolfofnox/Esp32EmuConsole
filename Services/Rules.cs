using System.Text.Json;

namespace Esp32EmuConsole.Services;

public class Rules : IDisposable
{
    private readonly string _rulesPath;
    private readonly FileSystemWatcher? _watcher;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private readonly ILogger<Rules> _logger;
    public List<Rule> RuleList { get; private set; } = new();
    public Dictionary<string, FixedResponse?> RuleMap { get; private set; } = new(StringComparer.OrdinalIgnoreCase);


    public Rules(string workingDirectory, ILogger<Rules> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rulesPath = Path.Combine(workingDirectory, "rules.json");
        LoadRules();
        _logger.LogInformation("Loaded {Count} rules from {RulesPath}", RuleList.Count, _rulesPath);

        if (File.Exists(_rulesPath))
        {
            _watcher = new FileSystemWatcher(workingDirectory, "rules.json") { NotifyFilter = NotifyFilters.LastWrite };
            _watcher.Changed += (_, _) => { try { LoadRules(); } catch { } };
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void LoadRules()
    {
        if (!File.Exists(_rulesPath))
        {
            RuleList = new List<Rule>();
            RuleMap = new Dictionary<string, FixedResponse?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var jsonText = File.ReadAllText(_rulesPath);
        try
        {
            RuleList = JsonSerializer.Deserialize<List<Rule>>(jsonText, _jsonOptions) ?? new List<Rule>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing rules.json");
            RuleList = new List<Rule>();
        }

        var map = new Dictionary<string, FixedResponse?>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in RuleList)
        {
            var path = r.Uri?.Trim();
            if (string.IsNullOrWhiteSpace(path)) continue;
            var method = r.Method?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(method)) method = "GET";
            var key = MakeKey(method, path);
            map[key] = r.Response;
        }

        RuleMap = map;
    }

    private static string MakeKey(string method, string path) => $"{method.Trim().ToUpperInvariant()} {(path.Trim().StartsWith("/") ? path.Trim() : "/" + path.Trim())}";

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
