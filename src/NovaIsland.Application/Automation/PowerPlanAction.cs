using Microsoft.Extensions.Logging;
using NovaIsland.Domain.Automation;

namespace NovaIsland.Application.Automation;

public class PowerPlanAction : IAction
{
    private readonly ILogger<PowerPlanAction> _logger;

    public PowerPlanAction(ILogger<PowerPlanAction> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(RuleEvaluationContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Switching Power Plan to Balanced (mock)");
        return Task.CompletedTask;
    }
}
