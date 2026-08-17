using System.Text.Json;
using JerseyOs.Application;
using JerseyOs.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace JerseyOs.Api;

[Authorize]
public sealed class OpsStatusHub : Hub
{
    public const string Path = "/hubs/ops";
    public const string ClientMethod = "opsStatus";

    public override async Task OnConnectedAsync()
    {
        var org = Context.User?.FindFirst("org")?.Value;
        if (!string.IsNullOrWhiteSpace(org))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, org).ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}

public sealed class RedisOpsStatusRelay(
    IConnectionMultiplexer redis,
    IHubContext<OpsStatusHub> hubs) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ChannelMessageQueue queue;
        try
        {
            queue = await redis.GetSubscriber()
                .SubscribeAsync(RedisChannel.Literal(RedisOpsStatusPublisher.Channel))
                .WaitAsync(stoppingToken)
                .ConfigureAwait(false);
        }
        catch (RedisException)
        {
            return;
        }
        catch (RedisCommandException)
        {
            return;
        }
        catch (TimeoutException)
        {
            return;
        }

        queue.OnMessage(async message =>
        {
            var json = (string?)message.Message;
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            OpsStatusEvent? status;
            try
            {
                status = JsonSerializer.Deserialize<OpsStatusEvent>(json, Json);
            }
            catch (JsonException)
            {
                return;
            }

            if (status is null)
            {
                return;
            }

            await hubs.Clients.Group(status.OrganizationId.ToString())
                .SendAsync(OpsStatusHub.ClientMethod, status, CancellationToken.None)
                .ConfigureAwait(false);
        });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
