using FluentAssertions;
using NovaIsland.Domain.Ai;
using NovaIsland.Infrastructure.Ai;
using Xunit;

namespace NovaIsland.Tests.Integration.Ai;

public class AiStreamingTests
{
    [Fact]
    public async Task GetResponseStreamAsync_YieldsTokensWithLatency()
    {
        // Arrange
        var provider = new DummyCloudAiProvider();
        var messages = new List<AiMessage> { new AiMessage("user", "Hello") };
        var tokens = new List<string>();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await foreach (var token in provider.GetResponseStreamAsync(messages))
        {
            tokens.Add(token);
        }

        sw.Stop();

        // Assert
        tokens.Should().NotBeEmpty();
        string fullText = string.Join("", tokens);
        fullText.Should().Contain("Nova Island");
        
        // Since there is a 10-100ms delay per token and 21 tokens, 
        // it should take at least 210ms.
        sw.ElapsedMilliseconds.Should().BeGreaterThan(200);
    }
}
