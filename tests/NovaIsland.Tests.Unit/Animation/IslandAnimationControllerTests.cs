using FluentAssertions;
using NovaIsland.UI.Animation;
using Xunit;

namespace NovaIsland.Tests.Unit.Animation;

/// <summary>
/// Tests for <see cref="IslandAnimationController"/> — the multi-dimensional
/// spring animation controller that drives island state transitions.
/// </summary>
public sealed class IslandAnimationControllerTests
{
    private static IslandAnimationController CreateController(
        IslandState initial = IslandState.Compact,
        float stiffness = 300f)
    {
        return new IslandAnimationController(initial, SpringConfig.CriticallyDamped(stiffness));
    }

    [Fact]
    public void InitialState_MatchesDescriptor_AndIsSettled()
    {
        // Arrange & Act
        var controller = CreateController(IslandState.Compact);
        controller.GetCurrentValues(out float w, out float h, out float cr, out float o, out float oy);

        ref readonly var desc = ref IslandStateDescriptors.GetDescriptor(IslandState.Compact);

        // Assert
        controller.CurrentTarget.Should().Be(IslandState.Compact);
        controller.IsSettled.Should().BeTrue();
        w.Should().Be(desc.Width);
        h.Should().Be(desc.Height);
        cr.Should().Be(desc.CornerRadius);
        o.Should().Be(desc.Opacity);
        oy.Should().Be(desc.OffsetY);
    }

    [Theory]
    [InlineData(IslandState.Compact, IslandState.Expanded)]
    [InlineData(IslandState.Expanded, IslandState.Minimal)]
    [InlineData(IslandState.Minimal, IslandState.Alert)]
    [InlineData(IslandState.Alert, IslandState.Compact)]
    public void TransitionTo_Converges_ToTargetState(IslandState from, IslandState to)
    {
        // Arrange
        var controller = CreateController(from);
        ref readonly var targetDesc = ref IslandStateDescriptors.GetDescriptor(to);

        // Act
        controller.TransitionTo(to);
        controller.CurrentTarget.Should().Be(to);
        controller.IsSettled.Should().BeFalse("animation should start");

        // Simulate 3 seconds at 120 Hz
        for (int i = 0; i < 360; i++)
        {
            controller.Update(1f / 120f);
        }

        // Assert
        controller.GetCurrentValues(out float w, out float h, out float cr, out float o, out float oy);
        w.Should().BeApproximately(targetDesc.Width, 0.1f);
        h.Should().BeApproximately(targetDesc.Height, 0.1f);
        cr.Should().BeApproximately(targetDesc.CornerRadius, 0.1f);
        o.Should().BeApproximately(targetDesc.Opacity, 0.01f);
        oy.Should().BeApproximately(targetDesc.OffsetY, 0.1f);
        controller.IsSettled.Should().BeTrue();
    }

    [Fact]
    public void Interruptible_MidTransition_SmoothlyRedirects()
    {
        // Arrange
        var controller = CreateController(IslandState.Compact);

        // Start transition to Expanded
        controller.TransitionTo(IslandState.Expanded);
        for (int i = 0; i < 30; i++) // ~250ms at 120Hz
        {
            controller.Update(1f / 120f);
        }

        // Capture mid-animation state
        controller.GetCurrentValues(out float midW, out _, out _, out _, out _);
        ref readonly var compactDesc = ref IslandStateDescriptors.GetDescriptor(IslandState.Compact);
        ref readonly var expandedDesc = ref IslandStateDescriptors.GetDescriptor(IslandState.Expanded);

        midW.Should().BeGreaterThan(compactDesc.Width, "should be between compact and expanded");
        midW.Should().BeLessThan(expandedDesc.Width, "should not have reached expanded yet");

        // Interrupt with Alert transition
        controller.TransitionTo(IslandState.Alert);

        // The controller should NOT restart from the original state.
        controller.GetCurrentValues(out float afterInterruptW, out _, out _, out _, out _);
        afterInterruptW.Should().BeApproximately(midW, 0.1f,
            "position should not jump on retarget — velocity preserved");

        // Complete animation
        ref readonly var alertDesc = ref IslandStateDescriptors.GetDescriptor(IslandState.Alert);
        for (int i = 0; i < 360; i++)
        {
            controller.Update(1f / 120f);
        }

        controller.GetCurrentValues(out float finalW, out _, out _, out _, out _);
        finalW.Should().BeApproximately(alertDesc.Width, 0.1f,
            "should converge to alert state after interrupt");
        controller.IsSettled.Should().BeTrue();
    }

    [Fact]
    public void Update_WithZeroDelta_DoesNotMove()
    {
        // Arrange
        var controller = CreateController(IslandState.Compact);
        controller.TransitionTo(IslandState.Expanded);
        controller.GetCurrentValues(out float beforeW, out _, out _, out _, out _);

        // Act
        controller.Update(0f);

        // Assert
        controller.GetCurrentValues(out float afterW, out _, out _, out _, out _);
        afterW.Should().Be(beforeW);
    }

    [Fact]
    public void ConfigChange_AppliesNewSpringBehavior()
    {
        // Arrange
        var controller = CreateController(IslandState.Compact, stiffness: 100f);
        controller.TransitionTo(IslandState.Expanded);

        // Simulate a few frames with low stiffness
        for (int i = 0; i < 30; i++)
        {
            controller.Update(1f / 120f);
        }
        controller.GetCurrentValues(out float slowW, out _, out _, out _, out _);

        // Create another controller with higher stiffness
        var fastController = CreateController(IslandState.Compact, stiffness: 1000f);
        fastController.TransitionTo(IslandState.Expanded);

        for (int i = 0; i < 30; i++)
        {
            fastController.Update(1f / 120f);
        }
        fastController.GetCurrentValues(out float fastW, out _, out _, out _, out _);

        // Assert — higher stiffness should make faster progress
        fastW.Should().BeGreaterThan(slowW,
            "higher stiffness should produce faster convergence");
    }
}
