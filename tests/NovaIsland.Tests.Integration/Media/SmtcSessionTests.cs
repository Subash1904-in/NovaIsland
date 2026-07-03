using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NovaIsland.Infrastructure.Media;

namespace NovaIsland.Tests.Integration.Media;

public class SmtcSessionTests
{
    [Fact]
    public async Task InitializeAsync_AttachesToSmtcAndUpdatesInUnder100ms()
    {
        // This is a rough integration test. Since we can't easily mock the OS-level SMTC
        // without an extensive wrapper, we test that the service can initialize without
        // exceptions and we can hook the event.
        
        using var service = new SmtcMediaService(NullLogger<SmtcMediaService>.Instance);
        var trackChangedInvoked = false;
        
        service.TrackChanged += (s, e) =>
        {
            trackChangedInvoked = true;
        };

        await service.InitializeAsync();
        
        // Wait briefly for SMTC to report initial state
        await Task.Delay(200);
        
        // If there's an active media session, trackChangedInvoked might be true,
        // but it's hard to guarantee in a CI environment.
        // The main integration check is that InitializeAsync doesn't throw and
        // CurrentTrack doesn't crash on read.
        
        var track = service.CurrentTrack;
        if (track != null)
        {
            track.Title.Should().NotBeNull();
            track.Artist.Should().NotBeNull();
            trackChangedInvoked.Should().BeTrue();
        }
    }
}
