using System.Runtime.CompilerServices;
using NovaIsland.Domain.Ai;

namespace NovaIsland.Infrastructure.Ai;

public class DummyCloudAiProvider : IAiProvider
{
    public async IAsyncEnumerable<string> GetResponseStreamAsync(
        IEnumerable<AiMessage> messages, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var responseTokens = new[]
        {
            "Hello! ", "I ", "am ", "the ", "Nova ", "Island ", "AI ", "Assistant. ",
            "I ", "am ", "currently ", "running ", "in ", "a ", "simulated ", "environment ",
            "to ", "test ", "streaming ", "UI ", "rendering."
        };

        foreach (var token in responseTokens)
        {
            // Simulate network latency
            await Task.Delay(Random.Shared.Next(10, 100), cancellationToken);
            yield return token;
        }
    }
}
