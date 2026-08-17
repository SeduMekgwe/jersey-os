using System.Text.Json;
using JerseyOs.Application;
using StackExchange.Redis;

namespace JerseyOs.Infrastructure;

public sealed class RedisOpsStatusPublisher(IConnectionMultiplexer redis) : IOpsStatusPublisher
{
    public const string Channel = "jerseyos:ops:status";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task PublishAsync(OpsStatusEvent status, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(status);
        try
        {
            if (!redis.IsConnected)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(status, Json);
            await redis.GetSubscriber()
                .PublishAsync(RedisChannel.Literal(Channel), payload)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RedisException)
        {
        }
        catch (RedisCommandException)
        {
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
        }
    }
}
