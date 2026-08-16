using JerseyOs.Domain;

namespace JerseyOs.Domain.Tests;

public sealed class NotificationTests
{
    [Fact]
    public void TemplateRendersSubjectAndBody()
    {
        var template = new NotificationTemplate(
            Guid.NewGuid(),
            NotificationKinds.ImportReady,
            "Import ready",
            "Import {{fileName}} is ready",
            "Batch {{batchId}} has {{itemCount}} items.");

        var values = new Dictionary<string, string?>
        {
            ["fileName"] = "kits.csv",
            ["batchId"] = "abc",
            ["itemCount"] = "3"
        };

        Assert.Equal("Import kits.csv is ready", template.RenderSubject(values));
        Assert.Equal("Batch abc has 3 items.", template.RenderBody(values));
    }

    [Fact]
    public void MessageRequiresPendingBeforeSent()
    {
        var message = new NotificationMessage(
            Guid.NewGuid(),
            NotificationKinds.PublishFailed,
            "Publish failed",
            "Channel error",
            "{}",
            null,
            "corr");
        var now = DateTimeOffset.UtcNow;
        message.MarkSent(now);
        Assert.Equal(NotificationMessageStatus.Sent, message.Status);
        Assert.Throws<InvalidOperationException>(() => message.MarkFailed(now));
    }
}
