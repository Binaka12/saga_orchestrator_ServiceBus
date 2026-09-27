using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagaOrchestrator.Services;

public interface IMessagePublisher
{
    Task SendMessageAsync<T>(string queueOrTopic, T payload, string? correlationId = null, IDictionary<string, object>? applicationProperties = null);
}