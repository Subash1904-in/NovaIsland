using System;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Threading.Tasks;
using FluentAssertions;
using NovaIsland.Application.Marketplace;
using Xunit;

namespace NovaIsland.Tests.Integration.Marketplace;

public class MarketplaceTests : IDisposable
{
    private readonly string _testPluginsDir;
    private readonly string _testPackagesDir;

    public MarketplaceTests()
    {
        _testPluginsDir = Path.Combine(Path.GetTempPath(), $"NovaIslandTest_Plugins_{Guid.NewGuid()}");
        _testPackagesDir = Path.Combine(Path.GetTempPath(), $"NovaIslandTest_Packages_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPluginsDir);
        Directory.CreateDirectory(_testPackagesDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testPluginsDir))
            Directory.Delete(_testPluginsDir, true);
        if (Directory.Exists(_testPackagesDir))
            Directory.Delete(_testPackagesDir, true);
        GC.SuppressFinalize(this);
    }

    private string CreateTestPackage(string id, string signature)
    {
        string packagePath = Path.Combine(_testPackagesDir, $"{id}.zip");
        string tempDir = Path.Combine(_testPackagesDir, id);
        Directory.CreateDirectory(tempDir);

        string manifestJson = $$"""
        {
            "Id": "{{id}}",
            "Name": "Test Plugin",
            "Version": "1.0.0",
            "Author": "Test",
            "EntryPoint": "Test.dll",
            "Capabilities": 0,
            "Signature": "{{signature}}"
        }
        """;
        File.WriteAllText(Path.Combine(tempDir, "manifest.json"), manifestJson);

        ZipFile.CreateFromDirectory(tempDir, packagePath);
        Directory.Delete(tempDir, true);

        return packagePath;
    }

    [Fact]
    public async Task InstallAsync_RejectsTamperedPackage_AndCleansUpTemp()
    {
        // Arrange
        var validator = new PluginPackageValidator();
        var installer = new PluginInstaller(validator, _testPluginsDir);
        string packagePath = CreateTestPackage("plugin.tampered", "tampered");

        // Act
        Func<Task> act = async () => await installer.InstallAsync(packagePath);

        // Assert
        await act.Should().ThrowAsync<SecurityException>()
            .WithMessage("Package signature is invalid or tampered.");

        // Verify cleanup: no directories in plugins dir
        Directory.GetDirectories(_testPluginsDir).Should().BeEmpty();
    }

    [Fact]
    public async Task InstallAsync_RollsBack_OnInterruptedInstall()
    {
        // Arrange
        // We'll create an invalid package (no manifest.json) to simulate a failure during validation/extraction
        string packagePath = Path.Combine(_testPackagesDir, "invalid.zip");
        string tempDir = Path.Combine(_testPackagesDir, "invalid");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "dummy.txt"), "no manifest");
        ZipFile.CreateFromDirectory(tempDir, packagePath);
        
        var validator = new PluginPackageValidator();
        var installer = new PluginInstaller(validator, _testPluginsDir);

        // Act
        Func<Task> act = async () => await installer.InstallAsync(packagePath);

        // Assert
        await act.Should().ThrowAsync<SecurityException>();

        // Verify cleanup of .tmp folders
        Directory.GetDirectories(_testPluginsDir).Should().BeEmpty();
    }

    [Fact]
    public async Task UninstallAsync_RemovesPluginDirectory_WithZeroResidue()
    {
        // Arrange
        var validator = new PluginPackageValidator();
        var installer = new PluginInstaller(validator, _testPluginsDir);
        string packagePath = CreateTestPackage("plugin.valid", "valid_signature");

        // Act - Install first
        await installer.InstallAsync(packagePath);
        
        string expectedDir = Path.Combine(_testPluginsDir, "plugin.valid");
        Directory.Exists(expectedDir).Should().BeTrue();

        // Act - Uninstall
        await installer.UninstallAsync("plugin.valid");

        // Assert
        Directory.Exists(expectedDir).Should().BeFalse();
        Directory.GetDirectories(_testPluginsDir).Should().BeEmpty();
    }
}
