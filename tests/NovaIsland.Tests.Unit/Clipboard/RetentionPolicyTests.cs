using FluentAssertions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NovaIsland.Application.Modules;
using NovaIsland.Domain.Clipboard;
using NovaIsland.Infrastructure.Clipboard;

namespace NovaIsland.Tests.Unit.Clipboard;

public class RetentionPolicyTests
{
    private readonly DbContextOptions<NovaIsland.Infrastructure.Clipboard.ClipboardDbContext> _dbOptions;
    
    public RetentionPolicyTests()
    {
        _dbOptions = new DbContextOptionsBuilder<NovaIsland.Infrastructure.Clipboard.ClipboardDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private sealed class TestDbContextFactory : IDbContextFactory<NovaIsland.Infrastructure.Clipboard.ClipboardDbContext>
    {
        private readonly DbContextOptions<NovaIsland.Infrastructure.Clipboard.ClipboardDbContext> _options;
        public TestDbContextFactory(DbContextOptions<NovaIsland.Infrastructure.Clipboard.ClipboardDbContext> options) => _options = options;
        public NovaIsland.Infrastructure.Clipboard.ClipboardDbContext CreateDbContext() => new NovaIsland.Infrastructure.Clipboard.ClipboardDbContext(_options);
    }

    [Fact]
    public async Task PerformRetentionCleanupAsync_EvictsOldUnpinnedEntries()
    {
        // Arrange
        var factory = new TestDbContextFactory(_dbOptions);
        using (var context = factory.CreateDbContext())
        {
            context.Entries.Add(new ClipboardEntry { Id = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow.AddDays(-10), IsPinned = false, Type = ClipboardEntryType.Text });
            context.Entries.Add(new ClipboardEntry { Id = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow.AddDays(-2), IsPinned = false, Type = ClipboardEntryType.Text });
            context.Entries.Add(new ClipboardEntry { Id = Guid.NewGuid(), Timestamp = DateTimeOffset.UtcNow.AddDays(-10), IsPinned = true, Type = ClipboardEntryType.Text });
            await context.SaveChangesAsync();
        }

        var optionsMock = new Mock<IOptionsMonitor<ClipboardOptions>>();
        optionsMock.Setup(o => o.CurrentValue).Returns(new ClipboardOptions { RetentionPeriod = TimeSpan.FromDays(7) });

        var service = new ClipboardListenerService(factory, optionsMock.Object, NullLogger<ClipboardListenerService>.Instance);
        var module = new ClipboardModule(service, optionsMock.Object, NullLogger<ClipboardModule>.Instance);

        // Act
        // Use reflection to call the private method for testing
        var method = typeof(ClipboardModule).GetMethod("PerformRetentionCleanupAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(module, new object[] { CancellationToken.None })!;

        // Assert
        using (var context = factory.CreateDbContext())
        {
            var remaining = await context.Entries.ToListAsync();
            remaining.Should().HaveCount(2);
            remaining.Should().Contain(e => e.IsPinned); // The pinned one should remain
            remaining.Should().Contain(e => e.Timestamp > DateTimeOffset.UtcNow.AddDays(-7)); // The recent one should remain
        }
    }
}
