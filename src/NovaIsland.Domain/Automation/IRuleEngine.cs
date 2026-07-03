namespace NovaIsland.Domain.Automation;

public interface IRuleEngine
{
    Task EvaluateRulesAsync(CancellationToken cancellationToken = default);
    void AddRule(Rule rule);
    void RemoveRule(string ruleId);
}
