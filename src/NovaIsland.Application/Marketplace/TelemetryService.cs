using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NovaIsland.Domain.Marketplace;

namespace NovaIsland.Application.Marketplace;

public class TelemetryService : ITelemetryService
{
    private readonly string _telemetryFile;

    public bool IsOptedIn { get; set; }

    public TelemetryService(string? telemetryFile = null)
    {
        _telemetryFile = telemetryFile ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaIsland", "telemetry.json");
    }

    public async Task RecordEventAsync(string eventName, string data)
    {
        if (!IsOptedIn) return;

        var payload = new
        {
            Timestamp = DateTime.UtcNow,
            Event = eventName,
            Data = data
        };

        var json = JsonSerializer.Serialize(payload);
        
        string? directory = Path.GetDirectoryName(_telemetryFile);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(_telemetryFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync(json);
    }
}
