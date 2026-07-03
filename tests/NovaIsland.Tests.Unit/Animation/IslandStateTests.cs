using FluentAssertions;
using NovaIsland.UI.Animation;
using Xunit;

namespace NovaIsland.Tests.Unit.Animation;

/// <summary>
/// Tests for <see cref="IslandState"/> enum and <see cref="IslandStateDescriptors"/> lookup table.
/// </summary>
public sealed class IslandStateTests
{
    [Theory]
    [InlineData(IslandState.Compact)]
    [InlineData(IslandState.Expanded)]
    [InlineData(IslandState.Minimal)]
    [InlineData(IslandState.Alert)]
    public void GetDescriptor_ReturnsValid_Dimensions(IslandState state)
    {
        // Act
        ref readonly var desc = ref IslandStateDescriptors.GetDescriptor(state);

        // Assert — all dimensions must be positive
        desc.Width.Should().BeGreaterThan(0f, $"{state} width must be positive");
        desc.Height.Should().BeGreaterThan(0f, $"{state} height must be positive");
        desc.CornerRadius.Should().BeGreaterOrEqualTo(0f, $"{state} corner radius must be non-negative");
        desc.Opacity.Should().BeInRange(0f, 1f, $"{state} opacity must be 0–1");
        desc.OffsetY.Should().BeGreaterOrEqualTo(0f, $"{state} offsetY must be non-negative");
    }

    [Fact]
    public void Compact_Is_Smaller_Than_Expanded()
    {
        ref readonly var compact = ref IslandStateDescriptors.GetDescriptor(IslandState.Compact);
        ref readonly var expanded = ref IslandStateDescriptors.GetDescriptor(IslandState.Expanded);

        compact.Width.Should().BeLessThan(expanded.Width);
        compact.Height.Should().BeLessThan(expanded.Height);
    }

    [Fact]
    public void Minimal_Is_Smallest_State()
    {
        ref readonly var minimal = ref IslandStateDescriptors.GetDescriptor(IslandState.Minimal);
        ref readonly var compact = ref IslandStateDescriptors.GetDescriptor(IslandState.Compact);
        ref readonly var expanded = ref IslandStateDescriptors.GetDescriptor(IslandState.Expanded);
        ref readonly var alert = ref IslandStateDescriptors.GetDescriptor(IslandState.Alert);

        minimal.Width.Should().BeLessThan(compact.Width);
        minimal.Width.Should().BeLessThan(expanded.Width);
        minimal.Width.Should().BeLessThan(alert.Width);
        minimal.Height.Should().BeLessThan(compact.Height);
    }

    [Fact]
    public void SetDescriptor_OverridesValues()
    {
        // Arrange — save original
        ref readonly var original = ref IslandStateDescriptors.GetDescriptor(IslandState.Alert);
        var savedOriginal = new IslandStateDescriptor(original.Width, original.Height,
            original.CornerRadius, original.Opacity, original.OffsetY);

        try
        {
            var custom = new IslandStateDescriptor(500f, 80f, 10f, 0.9f, 12f);

            // Act
            IslandStateDescriptors.SetDescriptor(IslandState.Alert, in custom);
            ref readonly var result = ref IslandStateDescriptors.GetDescriptor(IslandState.Alert);

            // Assert
            result.Width.Should().Be(500f);
            result.Height.Should().Be(80f);
            result.CornerRadius.Should().Be(10f);
            result.Opacity.Should().Be(0.9f);
            result.OffsetY.Should().Be(12f);
        }
        finally
        {
            // Restore original to not affect other tests.
            IslandStateDescriptors.SetDescriptor(IslandState.Alert, in savedOriginal);
        }
    }

    [Fact]
    public void AllStates_Have_UniqueValues()
    {
        ref readonly var compact = ref IslandStateDescriptors.GetDescriptor(IslandState.Compact);
        ref readonly var expanded = ref IslandStateDescriptors.GetDescriptor(IslandState.Expanded);
        ref readonly var minimal = ref IslandStateDescriptors.GetDescriptor(IslandState.Minimal);
        ref readonly var alert = ref IslandStateDescriptors.GetDescriptor(IslandState.Alert);

        // Each state should have a distinct width+height combination.
        var dimensions = new[]
        {
            (compact.Width, compact.Height),
            (expanded.Width, expanded.Height),
            (minimal.Width, minimal.Height),
            (alert.Width, alert.Height),
        };

        dimensions.Should().OnlyHaveUniqueItems(
            "each state must have a unique size to be visually distinguishable");
    }

    [Fact]
    public void ReducedMotionAnimator_Snaps_Instantly()
    {
        // Arrange
        var animator = new ReducedMotionAnimator(IslandState.Compact);

        // Act
        animator.TransitionTo(IslandState.Expanded);
        animator.GetCurrentValues(out float w, out float h, out float cr, out _, out float oy);

        // Assert — dimensions should snap immediately (no spring)
        ref readonly var expanded = ref IslandStateDescriptors.GetDescriptor(IslandState.Expanded);
        w.Should().Be(expanded.Width);
        h.Should().Be(expanded.Height);
        cr.Should().Be(expanded.CornerRadius);
        oy.Should().Be(expanded.OffsetY);
    }

    [Fact]
    public void ReducedMotionAnimator_CrossFades_Opacity()
    {
        // Arrange — create animator at a state with opacity 1.0
        var animator = new ReducedMotionAnimator(IslandState.Compact);

        // Transition to Minimal (opacity 0.7) — should trigger cross-fade.
        animator.TransitionTo(IslandState.Minimal);

        // Immediately after transition, opacity should still be near 1.0 (fade just started).
        animator.GetCurrentValues(out _, out _, out _, out float opacityStart, out _);
        opacityStart.Should().BeApproximately(1.0f, 0.01f, "opacity fade should just be starting");
        animator.IsSettled.Should().BeFalse("cross-fade should be in progress");

        // After full fade duration (150ms), opacity should reach target.
        for (int i = 0; i < 20; i++) // 20 frames at 120Hz = 167ms > 150ms fade
        {
            animator.Update(1f / 120f);
        }

        animator.GetCurrentValues(out _, out _, out _, out float opacityEnd, out _);
        opacityEnd.Should().BeApproximately(0.7f, 0.01f, "opacity should reach target after fade");
        animator.IsSettled.Should().BeTrue("fade should be complete");
    }
}
