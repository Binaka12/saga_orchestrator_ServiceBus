using System.Collections.Concurrent;

namespace OrderService.Services;

public interface IMessageDeduplicator
{
    bool TryMarkProcessed(string messageId);
}

public class InMemoryMessageDeduplicator : IMessageDeduplicator
{
    private readonly ConcurrentDictionary<string, byte> _processed = new();

    // Returns false if already processed
    public bool TryMarkProcessed(string messageId)
    {
        return _processed.TryAdd(messageId, 0);
    }
}