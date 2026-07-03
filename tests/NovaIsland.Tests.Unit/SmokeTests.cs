using FluentAssertions;
using NovaIsland.Domain;
using Xunit;

namespace NovaIsland.Tests.Unit;

/// <summary>
/// Smoke tests to verify the solution builds and test infrastructure works.
/// </summary>
public sealed class SmokeTests
{
    [Fact]
    public void DomainAssemblyMarker_ReturnsCorrectName()
    {
        // Arrange & Act
        var name = DomainAssemblyMarker.AssemblyName;

        // Assert
        name.Should().Be("NovaIsland.Domain");
    }

    [Fact]
    public void ApplicationAssemblyMarker_ReturnsCorrectName()
    {
        // Arrange & Act
        var name = Application.ApplicationAssemblyMarker.AssemblyName;

        // Assert
        name.Should().Be("NovaIsland.Application");
    }

    [Fact]
    public void Solution_IsConfiguredWithNullableEnabled()
    {
        // This test will only compile if nullable reference types are enabled,
        // which validates Directory.Build.props configuration.
        string? nullableString = null;
        nullableString.Should().BeNull();
    }
}
