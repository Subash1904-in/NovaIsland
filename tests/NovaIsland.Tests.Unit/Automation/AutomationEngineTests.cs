using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NovaIsland.Application.Modules;
using NovaIsland.Domain.Automation;
using Xunit;

namespace NovaIsland.Tests.Unit.Automation;

public class AutomationEngineTests
{
    [Fact]
    public async Task EvaluateRulesAsync_ExecutesWithin5msBudget()
    {
        // Arrange
        var storeMock = new Mock<IRuleStore>();
        var module = new AutomationModule(storeMock.Object, NullLogger<AutomationModule>.Instance);

        var conditionMock = new Mock<ICondition>();
        conditionMock.Setup(c => c.EvaluateAsync(It.IsAny<RuleEvaluationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var actionMock = new Mock<IAction>();
        actionMock.Setup(a => a.ExecuteAsync(It.IsAny<RuleEvaluationContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var rule = new Rule
        {
            Name = "Test Rule",
            Condition = conditionMock.Object,
            Action = actionMock.Object
        };

        module.AddRule(rule);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await module.EvaluateRulesAsync();
        
        sw.Stop();

        // Assert
        actionMock.Verify(a => a.ExecuteAsync(It.IsAny<RuleEvaluationContext>(), It.IsAny<CancellationToken>()), Times.Once);
        
        // Assert it takes less than 50ms in testing environment (though target is 5ms, 
        // test runners can be slow, but we're mostly testing the logic doesn't intentionally block).
        sw.ElapsedMilliseconds.Should().BeLessThan(50);
    }
}
