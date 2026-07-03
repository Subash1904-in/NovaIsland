namespace NovaIsland.Domain.Ai;

public interface IAiProvider
{
    IAsyncEnumerable<string> GetResponseStreamAsync(IEnumerable<AiMessage> messages, CancellationToken cancellationToken = default);
}
