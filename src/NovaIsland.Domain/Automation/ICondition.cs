namespace NovaIsland.Domain.Automation;

public interface ICondition
{
    Task<bool> EvaluateAsync(RuleEvaluationContext context, CancellationToken cancellationToken = default);
}
