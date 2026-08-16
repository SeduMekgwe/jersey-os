using System.Globalization;
using Hangfire;
using JerseyOs.Application;
using JerseyOs.Infrastructure;
using JerseyOs.Worker;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<ICurrentRequest, WorkerCurrentRequest>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.Queues = ["default", "scrape", "ai", "notifications"];
});
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("JerseyOs.Worker"))
    .WithTracing(t => t.AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddRuntimeInstrumentation().AddOtlpExporter());
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

var host = builder.Build();
using (var scope = host.Services.CreateScope())
{
    var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurring.AddOrUpdate<OutboxPump>(
        "outbox-pump",
        pump => pump.EnqueuePendingAsync(CancellationToken.None),
        "*/1 * * * *");
}
host.Services.RegisterSupplierFeedRecurringJobs();
await host.RunAsync();
