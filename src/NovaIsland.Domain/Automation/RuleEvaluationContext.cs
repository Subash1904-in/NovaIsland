namespace NovaIsland.Domain.Automation;

public class RuleEvaluationContext
{
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    
    // Extensible dictionary for triggers/conditions to pass state
    public Dictionary<string, object> State { get; } = new();
}
