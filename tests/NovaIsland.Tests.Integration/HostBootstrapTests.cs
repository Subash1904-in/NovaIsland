using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NovaIsland.Infrastructure;
using NovaIsland.Infrastructure.Logging;
using Xunit;

namespace NovaIsland.Tests.Integration;

/// <summary>
/// Integration tests verifying the Generic Host bootstraps correctly
/// with all infrastructure services registered.
/// </summary>
public sealed class HostBootstrapTests
{
    [Fact]
    public void Host_Builds_WithInfrastructureServices()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddInfrastructure();

        // Act
        using var host = builder.Build();

        // Assert — CorrelationContext should be resolvable
        var context = host.Services.GetService<CorrelationContext>();
        context.Should().NotBeNull();
    }

    [Fact]
    public void CorrelationContext_BeginScope_SetsAndRestoresId()
    {
        // Arrange
        var context = new CorrelationContext();
        context.CorrelationId.Should().BeNull();

        // Act
        using (var scope = context.BeginScope("test-id"))
        {
            // Assert — inside scope
            context.CorrelationId.Should().Be("test-id");
        }

        // Assert — after scope disposal, ID is restored
        context.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void CorrelationContext_NestedScopes_RestoreCorrectly()
    {
        // Arrange
        var context = new CorrelationContext();

        // Act & Assert
        using (var outer = context.BeginScope("outer"))
        {
            context.CorrelationId.Should().Be("outer");

            using (var inner = context.BeginScope("inner"))
            {
                context.CorrelationId.Should().Be("inner");
            }

            context.CorrelationId.Should().Be("outer");
        }

        context.CorrelationId.Should().BeNull();
    }
}
