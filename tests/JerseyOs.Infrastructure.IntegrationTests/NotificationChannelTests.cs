using JerseyOs.Infrastructure;

namespace JerseyOs.Infrastructure.IntegrationTests;

public sealed class NotificationChannelTests
{
    [Fact]
    public async Task FixtureChannel_CompletesWithoutExternalCalls()
    {
        await new FixtureNotificationChannel().SendAsync(
            new NotificationSendRequest("Ready", "Import is ready.", "{}"),
            CancellationToken.None);
    }
}
