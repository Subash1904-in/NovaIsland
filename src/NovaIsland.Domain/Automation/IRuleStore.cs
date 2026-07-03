namespace NovaIsland.Domain.Automation;

public interface IRuleStore
{
    Task<IEnumerable<Rule>> LoadRulesAsync(CancellationToken cancellationToken = default);
    Task SaveRulesAsync(IEnumerable<Rule> rules, CancellationToken cancellationToken = default);
}
