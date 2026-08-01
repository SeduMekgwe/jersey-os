using JerseyOs.Application;
using JerseyOs.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public sealed class DesignTimeCurrentRequest : ICurrentRequest
{
    public Guid? UserId => null;
    public Guid? OrganizationId => Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
    public string Actor => "design-time";
    public string CorrelationId => "design-time";
}

public sealed class NoOpPublisher : IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification => Task.CompletedTask;
}

public sealed class NoOpIntegrationEventPublisher : IIntegrationEventPublisher
{
    public void Enqueue(IntegrationEventEnvelope envelope)
    {
    }
}

public sealed class JerseyOsDbContextFactory : IDesignTimeDbContextFactory<JerseyOsDbContext>
{
    public JerseyOsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "JerseyOs.Api"))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["Database:ConnectionString"]
            ?? "Server=localhost,1433;Database=JerseyOs;User Id=sa;Password=Your_password123;Encrypt=True;TrustServerCertificate=True";
        var organizationId = configuration.GetValue<Guid?>("Database:DefaultOrganizationId")
            ?? Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");

        var options = new DbContextOptionsBuilder<JerseyOsDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new JerseyOsDbContext(
            options,
            new DesignTimeCurrentRequest(),
            Options.Create(new DatabaseOptions
            {
                ConnectionString = connectionString,
                DefaultOrganizationId = organizationId
            }),
            new NoOpPublisher(),
            new NoOpIntegrationEventPublisher());
    }
}
