using Microsoft.Extensions.Logging;
using NovaIsland.Application.Stability;
using NovaIsland.Domain.Ai;

namespace NovaIsland.Application.Modules;

public class AiModule : INovaModule
{
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<AiModule> _logger;

    public string ModuleName => "AI Assistant";

    public AiModule(IAiProvider aiProvider, ILogger<AiModule> logger)
    {
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AI Module started. Provider: {Provider}", _aiProvider.GetType().Name);

        // Keep the module running. The UI handles interactions directly with the provider
        // or through events here, but for streaming it's better to expose the provider directly.
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally
        {
            _logger.LogInformation("AI Module stopped.");
        }
    }
}
