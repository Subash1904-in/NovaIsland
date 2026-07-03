using Serilog.Core;
using Serilog.Events;

namespace NovaIsland.Infrastructure.Logging;

/// <summary>
/// Serilog enricher that attaches the current correlation ID to every log event.
/// Reads from <see cref="CorrelationContext"/> which is backed by <see cref="AsyncLocal{T}"/>.
/// </summary>
public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    private const string PropertyName = "CorrelationId";
    private const string DefaultValue = "--------";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var correlationId = CorrelationContext.CurrentIdRaw ?? DefaultValue;
        var property = propertyFactory.CreateProperty(PropertyName, correlationId);
        logEvent.AddPropertyIfAbsent(property);
    }
}
