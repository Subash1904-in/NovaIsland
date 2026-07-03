namespace NovaIsland.Infrastructure.Logging;

/// <summary>
/// Holds the current correlation ID for the active async flow.
/// Uses <see cref="AsyncLocal{T}"/> to propagate across async continuations
/// without requiring explicit parameter passing.
/// </summary>
public sealed class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentId = new();

    /// <summary>
    /// Gets the raw correlation ID for the current async scope.
    /// Used by <see cref="CorrelationIdEnricher"/> which cannot take constructor-injected dependencies.
    /// </summary>
    internal static string? CurrentIdRaw => CurrentId.Value;

    /// <summary>
    /// Gets or sets the correlation ID for the current async scope.
    /// </summary>
    /// <remarks>
    /// Intentionally an instance member despite accessing static storage, to support
    /// DI injection and scoped usage patterns via <see cref="CorrelationScope"/>.
    /// </remarks>
#pragma warning disable CA1822 // Member does not access instance data — by design for DI pattern
    public string? CorrelationId
    {
        get => CurrentId.Value;
        set => CurrentId.Value = value;
    }
#pragma warning restore CA1822

    /// <summary>
    /// Creates a new correlation scope with a freshly generated ID.
    /// Disposes back to the previous ID when the scope ends.
    /// </summary>
    /// <returns>A disposable that restores the previous correlation ID on disposal.</returns>
    public CorrelationScope BeginScope()
    {
        return new CorrelationScope(this, Guid.NewGuid().ToString("N")[..8]);
    }

    /// <summary>
    /// Creates a correlation scope with an explicit ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID to use.</param>
    /// <returns>A disposable that restores the previous correlation ID on disposal.</returns>
    public CorrelationScope BeginScope(string correlationId)
    {
        return new CorrelationScope(this, correlationId);
    }
}

/// <summary>
/// Disposable scope that sets and restores a correlation ID on the <see cref="CorrelationContext"/>.
/// </summary>
public readonly struct CorrelationScope : IDisposable
{
    private readonly CorrelationContext _context;
    private readonly string? _previousId;

    internal CorrelationScope(CorrelationContext context, string correlationId)
    {
        _context = context;
        _previousId = context.CorrelationId;
        context.CorrelationId = correlationId;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _context.CorrelationId = _previousId;
    }
}
