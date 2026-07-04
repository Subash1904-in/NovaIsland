using Microsoft.Extensions.Logging;
using NovaIsland.Application.Stability;
using NovaIsland.Domain.Media;

namespace NovaIsland.Application.Modules;

public class MediaModule : INovaModule
{
    private readonly IMediaService _mediaService;
    private readonly ILogger<MediaModule> _logger;

    public string ModuleName => "Media";

    public MediaModule(IMediaService mediaService, ILogger<MediaModule> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Media Module started");

        await _mediaService.InitializeAsync();

        _mediaService.TrackChanged += OnTrackChanged;

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        finally
        {
            _mediaService.TrackChanged -= OnTrackChanged;
            _logger.LogInformation("Media Module stopped");
        }
    }

    private void OnTrackChanged(object? sender, TrackMetadata e)
    {
        _logger.LogInformation("Track changed: {Title} by {Artist}, Status: {Status}", e.Title, e.Artist, e.Status);
        
        // TODO: Interface with IslandShellService to expand the island and update progress bar
    }
}
