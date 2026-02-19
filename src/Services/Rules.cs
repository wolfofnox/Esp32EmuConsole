using System.Text.Json;

namespace Esp32EmuConsole.Services;

public class Rules : IRules
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
    private readonly ReaderWriterLockSlim _lock = new();
    private List<Rule> _ruleList = new();
    private Dictionary<string, HttpResponse?> _ruleMap = new(StringComparer.OrdinalIgnoreCase);

    [Obsolete("Use GetRules() method instead for thread-safe access")]
    public List<Rule> RuleList
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return new List<Rule>(_ruleList);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    [Obsolete("Use TryGetResponse() method instead for thread-safe access")]
    public Dictionary<string, HttpResponse?> RuleMap
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return new Dictionary<string, HttpResponse?>(_ruleMap, StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }


    public Rules(string workingDirectory, ILogger<Rules> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rulesPath = Path.Combine(workingDirectory, "rules.json");
        
        _lock.EnterWriteLock();
        try
        {
            LoadRulesInternal();
            _logger.LogInformation("Loaded {Count} rules from {RulesPath}", _ruleList.Count, _rulesPath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        if (File.Exists(_rulesPath))
        {
            _watcher = new FileSystemWatcher(workingDirectory, "rules.json") { NotifyFilter = NotifyFilters.LastWrite };
            _watcher.Changed += (_, _) => 
            { 
                try 
                { 
                    ReloadRules(); 
                } 
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reload rules on file change");
                }
            };
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void LoadRulesInternal()
    {
        if (!File.Exists(_rulesPath))
        {
            _ruleList = new List<Rule>();
            _ruleMap = new Dictionary<string, HttpResponse?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var jsonText = File.ReadAllText(_rulesPath);
        List<Rule> rules;
        try
        {
            rules = JsonSerializer.Deserialize<List<Rule>>(jsonText, _jsonOptions) ?? new List<Rule>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing rules.json");
            rules = new List<Rule>();
        }

        var httpRuleMap = new Dictionary<string, HttpResponse?>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rules)
        {
            // Process HTTP rules - either has Response.Http or has no response at all
            if (r.Response?.Ws == null)
            {
                var path = r.Uri?.Trim();
                if (string.IsNullOrWhiteSpace(path)) continue;
                var method = string.IsNullOrWhiteSpace(r.Method) ? "GET" : r.Method.Trim().ToUpperInvariant();
                var key = MakeKey(method, path);
                
                // If response is null or empty, create default 501 response
                if (r.Response?.Http == null)
                {
                    httpRuleMap[key] = new HttpResponse 
                    { 
                        StatusCode = 501,
                        ContentType = "text/plain",
                        Body = "Not Implemented"
                    };
                }
                else
                {
                    httpRuleMap[key] = r.Response.Http;
                }
            }
        }

        _ruleList = rules;
        _ruleMap = httpRuleMap;
    }

    public IReadOnlyList<Rule> GetRules()
    {
        _lock.EnterReadLock();
        try
        {
            return new List<Rule>(_ruleList);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool TryGetResponse(string method, string path, out HttpResponse? response)
    {
        _lock.EnterReadLock();
        try
        {
            var key = MakeKey(method, path);
            return _ruleMap.TryGetValue(key, out response);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void ReloadRules()
    {
        _lock.EnterWriteLock();
        try
        {
            LoadRulesInternal();
            _logger.LogInformation("Reloaded {Count} rules from {RulesPath}", _ruleList.Count, _rulesPath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private static string MakeKey(string method, string path) => $"{method.Trim().ToUpperInvariant()} {(path.Trim().StartsWith("/") ? path.Trim() : "/" + path.Trim())}";

    public void Dispose()
    {
        _watcher?.Dispose();
        _lock.Dispose();
    }
}
