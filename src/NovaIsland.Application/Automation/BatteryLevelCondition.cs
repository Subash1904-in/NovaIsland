using NovaIsland.Domain.Automation;

namespace NovaIsland.Application.Automation;

public class BatteryLevelCondition : ICondition
{
    public Task<bool> EvaluateAsync(RuleEvaluationContext context, CancellationToken cancellationToken = default)
    {
        // Mock checking battery level < 20%
        // We'll hardcode to false for now so it doesn't run continuously, 
        // but in tests we can mock the condition or use state.
        
        if (context.State.TryGetValue("BatteryLevel", out var levelObj) && levelObj is int level)
        {
            return Task.FromResult(level < 20);
        }

        return Task.FromResult(false);
    }
}
