using System.Text.Json;
using Microsoft.Extensions.Logging;
using NovaIsland.Domain.Automation;

namespace NovaIsland.Infrastructure.Automation;

public class FileRuleStore : IRuleStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly ILogger<FileRuleStore> _logger;

    public FileRuleStore(ILogger<FileRuleStore> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NovaIsland", "rules.json");
    }

    public async Task<IEnumerable<Rule>> LoadRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                // Return default rule
                return Enumerable.Empty<Rule>();
            }

            // Note: Real implementation would need a polymorphic JSON converter for ITrigger/ICondition/IAction
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            var rules = JsonSerializer.Deserialize<List<Rule>>(json) ?? new List<Rule>();
            return rules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rules from {Path}", _filePath);
            return Enumerable.Empty<Rule>();
        }
    }

    public async Task SaveRulesAsync(IEnumerable<Rule> rules, CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(rules, s_jsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save rules to {Path}", _filePath);
        }
    }
}
