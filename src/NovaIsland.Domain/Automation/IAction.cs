namespace NovaIsland.Domain.Automation;

public interface IAction
{
    Task ExecuteAsync(RuleEvaluationContext context, CancellationToken cancellationToken = default);
}
