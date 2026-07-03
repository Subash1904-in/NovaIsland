using System.Threading.Tasks;

namespace NovaIsland.Domain.Marketplace;

public interface ITelemetryService
{
    bool IsOptedIn { get; set; }
    
    Task RecordEventAsync(string eventName, string data);
}
