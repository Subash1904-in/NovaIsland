namespace NovaIsland.Domain.Automation;

public class Rule
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public ITrigger? Trigger { get; init; }
    public ICondition? Condition { get; init; }
    public IAction? Action { get; init; }
}
