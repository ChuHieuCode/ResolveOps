using Quartz;
using RabbitMQ.Client;
using ResolveOps.Messaging;
using ResolveOps.Modules.Ai;
using ResolveOps.Modules.Documents;
using ResolveOps.Modules.Exceptions;
using ResolveOps.Modules.Notifications;
using ResolveOps.Modules.Reporting;
using ResolveOps.Modules.Workflow;
using ResolveOps.Persistence;
using ResolveOps.ServiceDefaults;
using ResolveOps.Worker.Jobs;

// The Worker uses WebApplication builder (not just HostBuilder) so it can expose
// /health/* endpoints for the Aspire dashboard and orchestration readiness probes.

var builder = WebApplication.CreateBuilder(args);

// ── Service defaults (Serilog, OpenTelemetry, health checks) ─────────────
builder.AddServiceDefaults();

// ── Problem Details ───────────────────────────────────────────────────────
builder.Services.AddProblemDetails();

// ── Persistence: EF Core via Aspire integration ───────────────────────────
builder.AddSqlServerDbContext<AppDbContext>("resolveops");

// ── Cache: Redis via Aspire integration ──────────────────────────────────
builder.AddRedisClient("redis");

// ── RabbitMQ connection (Phase 5) ─────────────────────────────────────────
// Connection string name "rabbitmq" must be defined in Aspire AppHost or appsettings.
var rabbitMqUri = builder.Configuration["ConnectionStrings:rabbitmq"]
    ?? "amqp://guest:guest@localhost:5672/";

builder.Services.AddSingleton<IConnectionFactory>(_ =>
    new ConnectionFactory
    {
        Uri = new Uri(rabbitMqUri),
        AutomaticRecoveryEnabled = true,
    });

builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = sp.GetRequiredService<IConnectionFactory>();
    // CreateConnectionAsync resolved synchronously here for singleton registration.
    // Phase 16 will replace with IAsyncConnectionFactory if needed.
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

// ── Options ──────────────────────────────────────────────────────────────
builder.Services.Configure<ResolveOps.Messaging.Options.OutboxOptions>(
    builder.Configuration.GetSection(ResolveOps.Messaging.Options.OutboxOptions.SectionName));
builder.Services.Configure<ResolveOps.Messaging.Options.RabbitMqConsumerOptions>(
    builder.Configuration.GetSection(ResolveOps.Messaging.Options.RabbitMqConsumerOptions.SectionName));

// ── Modules ────────────────────────────────────────────────────────────────────
builder.Services.AddExceptionsModule();
builder.Services.AddWorkflowModule();
builder.Services.AddDocumentsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddReportingModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);

// ── Messaging & Background Consumers (Phase 5, 6, 7, 13, 14 & 18) ──────────────────────────────────
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxPublisherService>();
builder.Services.AddHostedService<ResolveOps.Worker.Consumers.TrackingIngestionConsumerService>();
builder.Services.AddHostedService<ResolveOps.Worker.Consumers.ExceptionEvaluationConsumerService>();
builder.Services.AddHostedService<ResolveOps.Worker.Consumers.NotificationConsumerService>();
builder.Services.AddHostedService<ResolveOps.Worker.Consumers.ReportingProjectionConsumerService>();
builder.Services.AddHostedService<ResolveOps.Worker.Consumers.AiProcessingConsumerService>();

// ── Quartz.NET Scheduled Jobs (Phase 7, 8, 9 / spec §18.2, §24) ──────────────────────
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("MissedDeadlineScanJob");
    q.AddJob<MissedDeadlineScanJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("MissedDeadlineScanTrigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(1)
            .RepeatForever()));

    var slaJobKey = new JobKey("SlaBreachScanJob");
    q.AddJob<SlaBreachScanJob>(opts => opts.WithIdentity(slaJobKey));
    q.AddTrigger(opts => opts
        .ForJob(slaJobKey)
        .WithIdentity("SlaBreachScanTrigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(1)
            .RepeatForever()));

    // Phase 9: Batch scan and verification of pending document uploads (runs every 10 seconds)
    var docScanJobKey = new JobKey("DocumentProcessingScanJob");
    q.AddJob<DocumentProcessingScanJob>(opts => opts.WithIdentity(docScanJobKey));
    q.AddTrigger(opts => opts
        .ForJob(docScanJobKey)
        .WithIdentity("DocumentProcessingScanTrigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(10)
            .RepeatForever()));

    // Phase 9: Abandoned upload intent cleanup (runs every 6 hours)
    var cleanupJobKey = new JobKey("AbandonedUploadCleanupJob");
    q.AddJob<AbandonedUploadCleanupJob>(opts => opts.WithIdentity(cleanupJobKey));
    q.AddTrigger(opts => opts
        .ForJob(cleanupJobKey)
        .WithIdentity("AbandonedUploadCleanupTrigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInHours(6)
            .RepeatForever()));

    // Phase 11: Claim follow-up & deadline scan (runs every 15 minutes)
    var claimFollowUpJobKey = new JobKey("ClaimFollowUpScanJob");
    q.AddJob<ClaimFollowUpScanJob>(opts => opts.WithIdentity(claimFollowUpJobKey));
    q.AddTrigger(opts => opts
        .ForJob(claimFollowUpJobKey)
        .WithIdentity("ClaimFollowUpScanTrigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(15)
            .RepeatForever()));

    // Phase 13: Notification email retry scan (runs every 2 minutes)
    var emailRetryJobKey = new JobKey("NotificationEmailRetryScanJob");
    q.AddJob<NotificationEmailRetryScanJob>(opts => opts.WithIdentity(emailRetryJobKey));
    q.AddTrigger(opts => opts
        .ForJob(emailRetryJobKey)
        .WithIdentity("NotificationEmailRetryScanTrigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(2)
            .RepeatForever()));

    // Phase 14: Asynchronous CSV export processing (runs every 30 seconds)
    var exportJobKey = new JobKey("ExportProcessingJob");
    q.AddJob<ExportProcessingJob>(opts => opts.WithIdentity(exportJobKey));
    q.AddTrigger(opts => opts
        .ForJob(exportJobKey)
        .WithIdentity("ExportProcessingTrigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(30)
            .RepeatForever()));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// ── SQL Server readiness health check ────────────────────────────────────
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db", tags: ["ready"]);

// ─────────────────────────────────────────────────────────────────────────

var app = builder.Build();

app.UseServiceDefaults(); // maps /health/live and /health/ready

app.Run();
