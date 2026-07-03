using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NovaIsland.Application.Modules;
using NovaIsland.Domain.Widgets;
using Xunit;

namespace NovaIsland.Tests.Unit.Widgets;

public class WidgetModuleTests
{
    [Fact]
    public async Task WidgetModule_InitializesAllWidgets()
    {
        // Arrange
        var w1 = new Mock<IWidget>();
        var w2 = new Mock<IWidget>();

        w1.Setup(w => w.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        w2.Setup(w => w.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var module = new WidgetModule(new[] { w1.Object, w2.Object }, NullLogger<WidgetModule>.Instance);

        var cts = new CancellationTokenSource();
        var runTask = module.ExecuteAsync(cts.Token);

        // Wait a bit for initialization to complete
        await Task.Delay(50);

        // Assert
        w1.Verify(w => w.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
        w2.Verify(w => w.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);

        cts.Cancel();
        
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) { }

        w1.Verify(w => w.Dispose(), Times.Once);
        w2.Verify(w => w.Dispose(), Times.Once);
    }
}
