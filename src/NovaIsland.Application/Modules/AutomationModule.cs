using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NovaIsland.Application.Stability;
using NovaIsland.Domain.Automation;

namespace NovaIsland.Application.Modules;

public class AutomationModule : INovaModule, IRuleEngine
{
    private readonly IRuleStore _ruleStore;
    private readonly ILogger<AutomationModule> _logger;
    private readonly List<Rule> _rules = new();

    public string ModuleName => "Automation Engine";

    public AutomationModule(IRuleStore ruleStore, ILogger<AutomationModule> logger)
    {
        _ruleStore = ruleStore;
        _logger = logger;
    }

    public void AddRule(Rule rule)
    {
        lock (_rules)
        {
            _rules.Add(rule);
        }
    }

    public void RemoveRule(string ruleId)
    {
        lock (_rules)
        {
            _rules.RemoveAll(r => r.Id == ruleId);
        }
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Automation Engine started.");

        var loadedRules = await _ruleStore.LoadRulesAsync(cancellationToken);
        lock (_rules)
        {
            _rules.AddRange(loadedRules);
        }

        // We use a periodic timer to evaluate rules. (e.g. every 1 second).
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await EvaluateRulesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            _logger.LogInformation("Automation Engine stopped.");
        }
    }

    public async Task EvaluateRulesAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        List<Rule> rulesSnapshot;
        lock (_rules)
        {
            rulesSnapshot = _rules.Where(r => r.IsEnabled).ToList();
        }

        var context = new RuleEvaluationContext();

        foreach (var rule in rulesSnapshot)
        {
            try
            {
                if (rule.Trigger != null && !rule.Trigger.ShouldEvaluate(context))
                    continue;

                if (rule.Condition != null)
                {
                    bool conditionMet = await rule.Condition.EvaluateAsync(context, cancellationToken);
                    if (!conditionMet)
                        continue;
                }

                if (rule.Action != null)
                {
                    _logger.LogInformation("Rule {RuleName} triggered. Executing action.", rule.Name);
                    await rule.Action.ExecuteAsync(context, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating rule {RuleName}", rule.Name);
            }
        }

        sw.Stop();
        if (sw.ElapsedMilliseconds > 5)
        {
            _logger.LogWarning("Automation tick exceeded 5ms budget: {Elapsed}ms", sw.ElapsedMilliseconds);
        }
    }
}
