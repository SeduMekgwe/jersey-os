using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using Hangfire;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class NotificationOptions
{
    public const string Section = "Notifications";
    public string Provider { get; set; } = "Fixture";
    public NotificationWebhookOptions Webhook { get; set; } = new();
    public NotificationEmailOptions Email { get; set; } = new();
}

public sealed class NotificationWebhookOptions
{
    public string Url { get; set; } = string.Empty;
}

public sealed class NotificationEmailOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public static class NotificationModelBuilderExtensions
{
    public static void ConfigureNotifications(this ModelBuilder builder, Guid effectiveOrganizationId)
    {
        builder.Entity<AuditLog>(b =>
        {
            b.Property(x => x.Action).HasMaxLength(128).IsRequired();
            b.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
            b.Property(x => x.EntityId).HasMaxLength(256).IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.OrganizationId, x.Action });
        });
        builder.Entity<NotificationTemplate>(b =>
        {
            b.ToTable("notification_templates");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Kind).HasMaxLength(64).IsRequired();
            b.Property(x => x.SubjectTemplate).HasMaxLength(200).IsRequired();
            b.Property(x => x.BodyTemplate).HasMaxLength(4000).IsRequired();
            b.HasIndex(x => new { x.OrganizationId, x.Kind, x.Name }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<NotificationMessage>(b =>
        {
            b.ToTable("notification_messages");
            b.Property(x => x.Kind).HasMaxLength(64).IsRequired();
            b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            b.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
            b.HasOne<NotificationTemplate>().WithMany().HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
        builder.Entity<NotificationDelivery>(b =>
        {
            b.ToTable("notification_deliveries");
            b.Property(x => x.Channel).HasMaxLength(32).IsRequired();
            b.Property(x => x.Destination).HasMaxLength(500).IsRequired();
            b.Property(x => x.Error).HasMaxLength(1000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.HasIndex(x => x.MessageId);
            b.HasOne<NotificationMessage>().WithMany().HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
    }
}

public sealed record NotificationSendRequest(string Subject, string Body, string PayloadJson);

public interface INotificationChannel
{
    string Channel { get; }
    string Destination { get; }
    Task SendAsync(NotificationSendRequest request, CancellationToken cancellationToken);
}

public sealed class FixtureNotificationChannel : INotificationChannel
{
    public string Channel => "fixture";
    public string Destination => "fixture";

    public Task SendAsync(NotificationSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class WebhookNotificationChannel(IHttpClientFactory httpFactory, IOptions<NotificationOptions> options)
    : INotificationChannel
{
    public string Channel => "webhook";
    public string Destination =>
        string.IsNullOrWhiteSpace(options.Value.Webhook.Url) ? "webhook" : options.Value.Webhook.Url.Trim();

    public async Task SendAsync(NotificationSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var url = options.Value.Webhook.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Notifications:Webhook:Url is required when Notifications:Provider=Webhook.");
        }

        var client = httpFactory.CreateClient("notifications");
        using var response = await client.PostAsJsonAsync(
                url.Trim(),
                new { subject = request.Subject, body = request.Body, payloadJson = request.PayloadJson },
                cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Notification webhook failed ({(int)response.StatusCode}).");
        }
    }
}

public sealed class EmailNotificationChannel(IOptions<NotificationOptions> options) : INotificationChannel
{
    public string Channel => "email";
    public string Destination =>
        string.IsNullOrWhiteSpace(options.Value.Email.To) ? "email" : options.Value.Email.To.Trim();

    public async Task SendAsync(NotificationSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = options.Value.Email;
        if (string.IsNullOrWhiteSpace(email.SmtpHost) || string.IsNullOrWhiteSpace(email.From) || string.IsNullOrWhiteSpace(email.To))
        {
            throw new InvalidOperationException("Notifications:Email SMTP host, From, and To are required when Notifications:Provider=Email.");
        }

        cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(email.SmtpHost.Trim(), email.SmtpPort)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(email.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(email.Username, email.Password)
        };
#pragma warning restore SYSLIB0014
        using var message = new MailMessage(email.From.Trim(), email.To.Trim(), request.Subject, request.Body);
        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class HangfireNotificationPublisher(
    JerseyOsDbContext db,
    IBackgroundJobClient jobs,
    ICurrentRequest current) : INotificationPublisher
{
    public async Task PublishAsync(
        Guid organizationId, string kind, object payload, CancellationToken cancellationToken)
    {
        var normalized = kind.Trim().ToLowerInvariant();
        var payloadJson = JsonSerializer.Serialize(payload);
        var values = ParseValues(payloadJson);
        Guid? templateId = null;
        string subject;
        string body;
        var template = await db.NotificationTemplatesSet.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId && x.Kind == normalized && x.IsEnabled)
            .OrderBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (template is not null)
        {
            templateId = template.Id;
            subject = template.RenderSubject(values);
            body = template.RenderBody(values);
        }
        else
        {
            var fallback = NotificationTemplate.DefaultFor(normalized);
            subject = NotificationTemplate.Render(fallback.Subject, values);
            body = NotificationTemplate.Render(fallback.Body, values);
        }

        var message = new NotificationMessage(
            organizationId,
            normalized,
            subject,
            body,
            payloadJson,
            templateId,
            current.CorrelationId);
        db.NotificationMessagesSet.Add(message);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobs.Enqueue<DeliverNotificationJob>(job => job.ExecuteAsync(message.Id, CancellationToken.None));
    }

    private static Dictionary<string, string?> ParseValues(string json)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Null => null,
                _ => property.Value.ToString()
            };
        }

        return values;
    }
}

public sealed class DeliverNotificationJob(
    JerseyOsDbContext db,
    INotificationChannel channel,
    TimeProvider time)
{
    [Queue("notifications")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await db.NotificationMessagesSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            .ConfigureAwait(false);
        if (message is null || message.Status != NotificationMessageStatus.Pending)
        {
            return;
        }

        var delivery = new NotificationDelivery(
            message.OrganizationId,
            message.Id,
            channel.Channel,
            channel.Destination);
        db.NotificationDeliveriesSet.Add(delivery);
        try
        {
            await channel.SendAsync(new NotificationSendRequest(message.Subject, message.Body, message.PayloadJson), cancellationToken)
                .ConfigureAwait(false);
            delivery.MarkSent(time.GetUtcNow());
            message.MarkSent(time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            delivery.MarkFailed(exception.Message, time.GetUtcNow());
            message.MarkFailed(time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

public static class NotificationInfrastructureExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.Section));
        services.AddHttpClient("notifications", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IAuditRecorder, AuditRecorder>();
        services.AddScoped<INotificationPublisher, HangfireNotificationPublisher>();
        services.AddScoped<DeliverNotificationJob>();
        services.AddSingleton<INotificationChannel>(sp =>
        {
            var provider = sp.GetRequiredService<IOptions<NotificationOptions>>().Value.Provider;
            if (string.Equals(provider, "Webhook", StringComparison.OrdinalIgnoreCase))
            {
                return ActivatorUtilities.CreateInstance<WebhookNotificationChannel>(sp);
            }

            if (string.Equals(provider, "Email", StringComparison.OrdinalIgnoreCase))
            {
                return ActivatorUtilities.CreateInstance<EmailNotificationChannel>(sp);
            }

            return new FixtureNotificationChannel();
        });
        return services;
    }
}
