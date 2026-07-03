using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NovaIsland.Application.Modules;
using NovaIsland.Domain.Notifications;
using Xunit;

namespace NovaIsland.Tests.Unit.Notifications;

public class NotificationModuleTests
{
    [Fact]
    public void NotificationReceived_FocusAssistActive_SuppressesAlert()
    {
        // Arrange
        var notificationServiceMock = new Mock<INotificationService>();
        var focusAssistMock = new Mock<IFocusAssistProvider>();
        
        focusAssistMock.Setup(f => f.IsFocusAssistActive()).Returns(true);

        var module = new NotificationModule(
            notificationServiceMock.Object,
            focusAssistMock.Object,
            NullLogger<NotificationModule>.Instance
        );

        bool alertTriggered = false;
        module.OnAlertTriggered += (s, e) => alertTriggered = true;

        var runTask = module.ExecuteAsync(CancellationToken.None);

        // Act
        var msg = new NotificationMessage("1", "App", "Title", "Body", DateTimeOffset.UtcNow);
        notificationServiceMock.Raise(n => n.NotificationReceived += null, notificationServiceMock.Object, msg);

        // Assert
        alertTriggered.Should().BeFalse();
    }

    [Fact]
    public void NotificationReceived_FocusAssistInactive_TriggersAlert()
    {
        // Arrange
        var notificationServiceMock = new Mock<INotificationService>();
        var focusAssistMock = new Mock<IFocusAssistProvider>();
        
        focusAssistMock.Setup(f => f.IsFocusAssistActive()).Returns(false);

        var module = new NotificationModule(
            notificationServiceMock.Object,
            focusAssistMock.Object,
            NullLogger<NotificationModule>.Instance
        );

        bool alertTriggered = false;
        module.OnAlertTriggered += (s, e) => alertTriggered = true;

        var runTask = module.ExecuteAsync(CancellationToken.None);

        // Act
        var msg = new NotificationMessage("1", "App", "Title", "Body", DateTimeOffset.UtcNow);
        notificationServiceMock.Raise(n => n.NotificationReceived += null, notificationServiceMock.Object, msg);

        // Assert
        alertTriggered.Should().BeTrue();
    }
}
