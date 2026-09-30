using EntityFramework.Exceptions.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ResolveOps.Domain;
using ResolveOps.Domain.Ai;
using ResolveOps.Domain.Claims;
using ResolveOps.Domain.Documents;
using ResolveOps.Domain.Exceptions;
using ResolveOps.Domain.Identity;
using ResolveOps.Domain.Messaging;
using ResolveOps.Domain.Notifications;
using ResolveOps.Domain.Partners;
using ResolveOps.Domain.Reporting;
using ResolveOps.Domain.Shipments;
using ResolveOps.Domain.Tenancy;
using ResolveOps.Domain.Tracking;
using ResolveOps.Domain.Workflow;
using ResolveOps.Persistence.Conventions;

namespace ResolveOps.Persistence;

/// <summary>
/// Root EF Core DbContext for ResolveOps.
///
/// Design decisions (see docs/assumptions.md — Phase 1 and Phase 2):
/// - Extends IdentityDbContext so ASP.NET Core Identity tables are managed here.
/// - Single shared context for the modular monolith; module EF configurations
///   are discovered via <see cref="AddConfigurationAssembly"/> at module startup.
/// - Snake_case naming applied after all configurations have run.
/// - SaveChangesAsync is overridden as a hook point for outbox/audit interceptors (Phase 5).
///
/// Tenant-aware query filters (spec §19.4):
/// - Entities with a TenantId get a global query filter using the current request's tenantId.
/// - When tenantId is null (migrations, admin jobs, seeding), no filter is applied.
///
/// Dependency pattern (see docs/assumptions.md — A-012):
/// - Persistence references module projects to declare DbSet&lt;T&gt; properties and for
///   the EF design-time factory (dotnet ef migrations add). This is the accepted
///   trade-off for a modular monolith with one shared DbContext.
/// - Module projects reference EF Core directly, NOT Persistence — preventing circular deps.
/// - Module DI registrations call AddConfigurationAssembly() to register their EF configs.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    // ── Module assembly registry ──────────────────────────────────────────────
    private static readonly List<System.Reflection.Assembly> _configurationAssemblies = [];

    /// <summary>
    /// Called by module DI registrations (e.g. IdentityModule.Register, TenancyModule.Register)
    /// to register their EF configuration assemblies before the first DbContext is built.
    /// </summary>
    public static void AddConfigurationAssembly(System.Reflection.Assembly assembly)
    {
        lock (_configurationAssemblies)
        {
            if (!_configurationAssemblies.Contains(assembly))
            {
                _configurationAssemblies.Add(assembly);
            }
        }
    }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<UserTenantMembership> UserTenantMemberships => Set<UserTenantMembership>();
    public DbSet<RefreshTokenSession> RefreshTokenSessions => Set<RefreshTokenSession>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    // ── Partners ─────────────────────────────────────────────────────────────
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();

    // ── Shipments (Phase 4) ───────────────────────────────────────────────────
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentLeg> ShipmentLegs => Set<ShipmentLeg>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<ShipmentTrackingAlias> ShipmentTrackingAliases => Set<ShipmentTrackingAlias>();

    // ── Tracking (Phase 6) ────────────────────────────────────────────────────
    public DbSet<InboundEventReceipt> InboundEventReceipts => Set<InboundEventReceipt>();
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();
    public DbSet<QuarantinedEvent> QuarantinedEvents => Set<QuarantinedEvent>();

    // ── Exceptions (Phase 7) ──────────────────────────────────────────────────
    public DbSet<ExceptionPolicy> ExceptionPolicies => Set<ExceptionPolicy>();
    public DbSet<ExceptionCase> ExceptionCases => Set<ExceptionCase>();
    public DbSet<ExceptionOccurrence> ExceptionOccurrences => Set<ExceptionOccurrence>();
    public DbSet<CaseTimelineEntry> CaseTimelineEntries => Set<CaseTimelineEntry>();

    // ── Workflow (Phase 8) ────────────────────────────────────────────────────
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<SlaPolicyVersion> SlaPolicyVersions => Set<SlaPolicyVersion>();
    public DbSet<SlaClock> SlaClocks => Set<SlaClock>();
    public DbSet<SlaClockPause> SlaClockPauses => Set<SlaClockPause>();

    // ── Documents (Phase 9) ───────────────────────────────────────────────────
    public DbSet<EvidenceDocument> EvidenceDocuments => Set<EvidenceDocument>();
    public DbSet<EvidenceRequirement> EvidenceRequirements => Set<EvidenceRequirement>();

    // ── Claims (Phase 10, 11 & 12) ───────────────────────────────────────────
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimLossComponent> ClaimLossComponents => Set<ClaimLossComponent>();
    public DbSet<ClaimApproval> ClaimApprovals => Set<ClaimApproval>();
    public DbSet<CarrierClaimResponse> CarrierClaimResponses => Set<CarrierClaimResponse>();
    public DbSet<RecoveryTransaction> RecoveryTransactions => Set<RecoveryTransaction>();

    // ── Notifications (Phase 13) ──────────────────────────────────────────────
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    // ── Reporting (Phase 14) ──────────────────────────────────────────────────
    public DbSet<ExportRequest> ExportRequests => Set<ExportRequest>();
    public DbSet<CarrierPerformanceSnapshot> CarrierPerformanceSnapshots => Set<CarrierPerformanceSnapshot>();

    // ── AI & RAG (Phase 18) ───────────────────────────────────────────────────
    public DbSet<AiTask> AiTasks => Set<AiTask>();
    public DbSet<AiFeedback> AiFeedbacks => Set<AiFeedback>();
    public DbSet<AiKnowledgeEmbedding> AiKnowledgeEmbeddings => Set<AiKnowledgeEmbedding>();

    // ── Messaging (Phase 5) ───────────────────────────────────────────────────
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    // ── System ───────────────────────────────────────────────────────────────
    public DbSet<ErrorTemplate> ErrorTemplates => Set<ErrorTemplate>();

    // ── Tenant filter & Infrastructure ────────────────────────────────────────
    private readonly Guid? _currentTenantId;
    private readonly IEnumerable<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor> _interceptors;
    private readonly TimeProvider _timeProvider;
    private readonly ResolveOps.Security.ITenantContext? _tenantContext;

    /// <summary>Primary constructor — no tenant filter (design-time factory, migrations, seeding).</summary>
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        TimeProvider? timeProvider = null,
        ResolveOps.Security.ITenantContext? tenantContext = null)
        : base(options)
    {
        _interceptors = [];
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tenantContext = tenantContext;
        try
        {
            _currentTenantId = tenantContext?.TenantId.Value;
        }
        catch (InvalidOperationException)
        {
            _currentTenantId = null;
        }
    }

    /// <summary>
    /// Request-scoped constructor — tenant filter active.
    /// Registered via factory in DI so each HTTP request gets the correct tenant ID
    /// from the resolved ITenantContext.
    /// </summary>
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        Guid? tenantId,
        IEnumerable<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor> interceptors,
        TimeProvider? timeProvider = null,
        ResolveOps.Security.ITenantContext? tenantContext = null)
        : base(options)
    {
        _currentTenantId = tenantId;
        _interceptors = interceptors;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tenantContext = tenantContext;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseExceptionProcessor();
        optionsBuilder.AddInterceptors(_interceptors);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Add(static _ => new SnakeCaseNamingConvention());
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // IdentityDbContext must run first to configure its own tables.
        base.OnModelCreating(builder);

        // Scan this assembly (in case any configurations live here directly).
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Apply configurations from each module assembly registered at startup.
        List<System.Reflection.Assembly> snapshot;
        lock (_configurationAssemblies)
        {
            snapshot = [.. _configurationAssemblies];
        }

        foreach (var asm in snapshot)
        {
            builder.ApplyConfigurationsFromAssembly(asm);
        }

        // ── Tenant query filters (spec §19.4 — defense in depth) ──────────────────
        builder.Entity<UserTenantMembership>()
            .HasQueryFilter(m => _currentTenantId == null || m.TenantId == _currentTenantId);

        builder.Entity<RefreshTokenSession>()
            .HasQueryFilter(s => _currentTenantId == null || s.TenantId == _currentTenantId);

        // Tenant is the isolation root — no tenant filter on it.

        // Partners — all entities are tenant-scoped
        builder.Entity<Carrier>()
            .HasQueryFilter(c => _currentTenantId == null || c.TenantId == _currentTenantId);

        builder.Entity<Customer>()
            .HasQueryFilter(c => _currentTenantId == null || c.TenantId == _currentTenantId);

        builder.Entity<Location>()
            .HasQueryFilter(l => _currentTenantId == null || l.TenantId == _currentTenantId);

        builder.Entity<BusinessCalendar>()
            .HasQueryFilter(bc => _currentTenantId == null || bc.TenantId == _currentTenantId);

        // Shipments — all entities are tenant-scoped (Phase 4)
        builder.Entity<Shipment>()
            .HasQueryFilter(s => _currentTenantId == null || s.TenantId == _currentTenantId);

        builder.Entity<ShipmentLeg>()
            .HasQueryFilter(l => _currentTenantId == null || l.TenantId == _currentTenantId);

        builder.Entity<ShipmentItem>()
            .HasQueryFilter(i => _currentTenantId == null || i.TenantId == _currentTenantId);

        builder.Entity<ShipmentTrackingAlias>()
            .HasQueryFilter(a => _currentTenantId == null || a.TenantId == _currentTenantId);

        // Tracking — all entities are tenant-scoped (Phase 6)
        builder.Entity<InboundEventReceipt>()
            .HasQueryFilter(r => _currentTenantId == null || r.TenantId == _currentTenantId);

        builder.Entity<TrackingEvent>()
            .HasQueryFilter(e => _currentTenantId == null || e.TenantId == _currentTenantId);

        builder.Entity<QuarantinedEvent>()
            .HasQueryFilter(q => _currentTenantId == null || q.TenantId == _currentTenantId);

        // Exceptions — all entities are tenant-scoped (Phase 7)
        builder.Entity<ExceptionPolicy>()
            .HasQueryFilter(p => _currentTenantId == null || p.TenantId == _currentTenantId);

        builder.Entity<ExceptionCase>()
            .HasQueryFilter(c => _currentTenantId == null || c.TenantId == _currentTenantId);

        builder.Entity<ExceptionOccurrence>()
            .HasQueryFilter(o => _currentTenantId == null || o.TenantId == _currentTenantId);

        builder.Entity<CaseTimelineEntry>()
            .HasQueryFilter(t => _currentTenantId == null || t.TenantId == _currentTenantId);

        // Workflow — all entities are tenant-scoped (Phase 8)
        builder.Entity<WorkflowTask>()
            .HasQueryFilter(t => _currentTenantId == null || t.TenantId == _currentTenantId);

        builder.Entity<SlaPolicy>()
            .HasQueryFilter(p => _currentTenantId == null || p.TenantId == _currentTenantId);

        builder.Entity<SlaPolicyVersion>()
            .HasQueryFilter(v => _currentTenantId == null || v.TenantId == _currentTenantId);

        builder.Entity<SlaClock>()
            .HasQueryFilter(c => _currentTenantId == null || c.TenantId == _currentTenantId);

        builder.Entity<SlaClockPause>()
            .HasQueryFilter(p => _currentTenantId == null || p.TenantId == _currentTenantId);

        // Documents — all entities are tenant-scoped (Phase 9)
        builder.Entity<EvidenceDocument>()
            .HasQueryFilter(d => _currentTenantId == null || d.TenantId == _currentTenantId);

        builder.Entity<EvidenceRequirement>()
            .HasQueryFilter(r => _currentTenantId == null || r.TenantId == _currentTenantId);

        // Claims — all entities are tenant-scoped (Phase 10 & 11)
        builder.Entity<Claim>()
            .HasQueryFilter(c => _currentTenantId == null || c.TenantId == _currentTenantId);

        builder.Entity<ClaimApproval>()
            .HasQueryFilter(a => _currentTenantId == null || a.TenantId == _currentTenantId);

        builder.Entity<CarrierClaimResponse>()
            .HasQueryFilter(r => _currentTenantId == null || r.TenantId == _currentTenantId);

        builder.Entity<RecoveryTransaction>()
            .HasQueryFilter(t => _currentTenantId == null || t.TenantId == _currentTenantId);

        // Notifications — tenant-scoped (Phase 13)
        builder.Entity<NotificationTemplate>()
            .HasQueryFilter(t => _currentTenantId == null || t.TenantId == null || t.TenantId == _currentTenantId);

        builder.Entity<Notification>()
            .HasQueryFilter(n => _currentTenantId == null || n.TenantId == _currentTenantId);

        builder.Entity<NotificationPreference>()
            .HasQueryFilter(p => _currentTenantId == null || p.TenantId == _currentTenantId);

        builder.Entity<NotificationDelivery>()
            .HasQueryFilter(d => _currentTenantId == null || d.TenantId == _currentTenantId);

        // Reporting — tenant-scoped (Phase 14)
        builder.Entity<ExportRequest>()
            .HasQueryFilter(e => _currentTenantId == null || e.TenantId == _currentTenantId);

        builder.Entity<CarrierPerformanceSnapshot>()
            .HasQueryFilter(c => _currentTenantId == null || c.TenantId == _currentTenantId);

        // AI & RAG — tenant-scoped (Phase 18)
        builder.Entity<AiTask>()
            .HasQueryFilter(t => _currentTenantId == null || t.TenantId == _currentTenantId);

        builder.Entity<AiFeedback>()
            .HasQueryFilter(f => _currentTenantId == null || f.TenantId == _currentTenantId);

        builder.Entity<AiKnowledgeEmbedding>()
            .HasQueryFilter(k => _currentTenantId == null || k.TenantId == _currentTenantId);



        // Messaging — OutboxMessage has nullable TenantId (system events have no tenant).
        // No global filter on OutboxMessage/InboxMessage/IdempotencyRecord — the publisher
        // and admin tooling need unfiltered access regardless of request tenant context.

        // ── Snake_case column names ────────────────────────────────────────────
        // Applied last so configurations' explicit column names are preserved.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrEmpty(columnName))
                {
                    property.SetColumnName(SnakeCaseNamingConvention.ToSnakeCase(columnName));
                }
            }
        }
    }

    /// <summary>
    /// Centralized SaveChangesAsync enforcing:
    /// 1. IAuditableEntity audit trail timestamps & current user resolution.
    /// 2. IHasConcurrencyStamp optimistic concurrency checks (ABP Framework pattern).
    /// </summary>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditAndConcurrency();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditAndConcurrency();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ApplyAuditAndConcurrency()
    {
        var now = _timeProvider.GetUtcNow();
        var currentUserId = ResolveCurrentUserId();

        // 1. Audit fields (IAuditableEntity)
        var auditableEntries = ChangeTracker.Entries<IAuditableEntity>();
        foreach (var entry in auditableEntries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = currentUserId;
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = currentUserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = currentUserId;
            }
        }

        // 2. Concurrency stamp (IHasConcurrencyStamp - ABP Framework pattern)
        var concurrencyEntries = ChangeTracker.Entries<IHasConcurrencyStamp>();
        foreach (var entry in concurrencyEntries)
        {
            if (entry.State == EntityState.Added)
            {
                if (string.IsNullOrWhiteSpace(entry.Entity.ConcurrencyStamp))
                {
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var clientStamp = entry.Entity.ConcurrencyStamp;
                entry.Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp)).OriginalValue = clientStamp;

                var newStamp = Guid.NewGuid().ToString("N");
                entry.Entity.ConcurrencyStamp = newStamp;
                entry.Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp)).CurrentValue = newStamp;
            }
        }
    }

    private string ResolveCurrentUserId()
    {
        try
        {
            if (_tenantContext != null)
            {
                return _tenantContext.UserId.Value.ToString();
            }
        }
        catch (InvalidOperationException)
        {
            // Unauthenticated or background worker context
        }

        return "System";
    }
}
