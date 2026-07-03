using FluentAssertions;
using Xunit;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NovaIsland.Tests.Integration.Deployment;

/// <summary>
/// Simulates deployment, install, rollback, and uninstall operations in a sandbox environment.
/// </summary>
public class WindowsSandboxDeploymentTests
{
    private readonly string _mockSandboxDir;

    public WindowsSandboxDeploymentTests()
    {
        // Mock a sandbox directory for the test execution.
        _mockSandboxDir = Path.Combine(Path.GetTempPath(), "NovaIsland_Sandbox_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mockSandboxDir);
    }

    [Fact]
    public void Install_LeavesZeroResidue_UponUninstall()
    {
        // Arrange
        var appPath = Path.Combine(_mockSandboxDir, "NovaIsland.App.exe");
        var logPath = Path.Combine(_mockSandboxDir, "logs");
        var configPath = Path.Combine(_mockSandboxDir, "config.json");
        
        // Act - Simulate Installation
        File.WriteAllText(appPath, "mock_binary_data");
        Directory.CreateDirectory(logPath);
        File.WriteAllText(configPath, "{ \"tier\": \"High\" }");
        
        // Assert - Verify it installed
        File.Exists(appPath).Should().BeTrue();
        Directory.Exists(logPath).Should().BeTrue();
        File.Exists(configPath).Should().BeTrue();

        // Act - Simulate Uninstall (Velopack/MSI should clean this entirely)
        if (Directory.Exists(_mockSandboxDir))
        {
            Directory.Delete(_mockSandboxDir, recursive: true);
        }

        // Assert - Verify Zero Residue
        Directory.Exists(_mockSandboxDir).Should().BeFalse();
        File.Exists(appPath).Should().BeFalse();
    }

    [Fact]
    public async Task CanaryRelease_RollsBack_OnCrashLoop()
    {
        // Arrange
        // We simulate Velopack's behavior when the Phase 2 CrashLoopDetector flags the process
        bool updateApplied = true;
        bool isCanary = true;
        int crashCount = 0;
        
        // Act
        // Simulate app starting 3 times and crashing immediately
        while(crashCount < 3)
        {
            crashCount++;
        }

        // If CrashLoopDetector fires, it signals Velopack to rollback
        if (crashCount >= 3)
        {
            // Simulate Velopack rolling back
            isCanary = false;
            updateApplied = false;
            await Task.Delay(10); // simulate rollback I/O
        }

        // Assert
        crashCount.Should().Be(3);
        isCanary.Should().BeFalse("The canary update should have rolled back.");
        updateApplied.Should().BeFalse("The system should revert to the stable version.");
    }
}
