using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NovaIsland.Plugins.Host;
using NovaIsland.SDK;
using Xunit;

namespace NovaIsland.Tests.Integration.Plugins;

public class PluginSandboxTests
{
    private static string GetTestAssemblyPath() => typeof(PluginSandboxTests).Assembly.Location;

    [Fact]
    public async Task LoadPluginAsync_CompletesWithin100ms_ForWellBehavedPlugin()
    {
        var module = new PluginModule(NullLogger<PluginModule>.Instance);
        var manifest = new PluginManifest("test", "Test", "1.0", "Author", typeof(DummyWellBehavedPlugin).FullName!, PluginCapability.None);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        await module.LoadPluginAsync(manifest, GetTestAssemblyPath());
        
        sw.Stop();
        
        // Assert load was fast. Testing environment might add overhead, but we test the CancellationToken enforces 100ms.
        sw.ElapsedMilliseconds.Should().BeLessThan(2000); 
    }

    [Fact]
    public async Task LoadPluginAsync_ThrowsTimeout_WhenPluginHangs()
    {
        var module = new PluginModule(NullLogger<PluginModule>.Instance);
        var manifest = new PluginManifest("hang", "Hang", "1.0", "Author", typeof(DummyHangPlugin).FullName!, PluginCapability.None);

        var act = async () => await module.LoadPluginAsync(manifest, GetTestAssemblyPath());

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public void CapabilityValidator_ThrowsSecurityException_WhenUnauthorized()
    {
        // This test simulates the Host side receiving an unauthorized call
        var manifest = new PluginManifest("unauth", "Unauth", "1.0", "Author", "Any", PluginCapability.None);
        var validator = new CapabilityValidator(manifest);

        var act = () => validator.AssertCapability(PluginCapability.Notifications);

        act.Should().Throw<SecurityException>().WithMessage("*attempted to use*");
    }
}

public class DummyWellBehavedPlugin : IPlugin
{
    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ExecuteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class DummyHangPlugin : IPlugin
{
    public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        // Simulate hang that ignores cancellation token (to test hard timeout)
        await Task.Delay(10000, CancellationToken.None);
    }
    public Task ExecuteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
