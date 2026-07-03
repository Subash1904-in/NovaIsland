namespace NovaIsland.Domain.Automation;

public interface ITrigger
{
    bool ShouldEvaluate(RuleEvaluationContext context);
}
