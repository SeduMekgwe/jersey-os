using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Hangfire.SqlServer;
using JerseyOs.Application;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using JerseyOs.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace JerseyOs.Infrastructure;

public sealed class DatabaseOptions
{
    public const string Section = "Database";
    public string ConnectionString { get; set; } = string.Empty;
    public Guid DefaultOrganizationId { get; set; }
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "JerseyOs";
    public string Audience { get; set; } = "JerseyOs";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessMinutes { get; set; } = 10;
    public int RefreshDays { get; set; } = 14;
}

public static class Permissions
{
    public const string PlatformRead = "platform.read";
    public const string PlatformAdmin = "platform.admin";
    public const string SystemHealthRead = "system.health.read";
    public static readonly string[] All = [PlatformRead, PlatformAdmin, SystemHealthRead];
}

public sealed class JerseyOsDbContext(
    DbContextOptions<JerseyOsDbContext> options,
    ICurrentRequest currentRequest,
    IOptions<DatabaseOptions> databaseOptions,
    IPublisher publisher,
    IIntegrationEventPublisher integrationEvents)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IApplicationDbContext
{
    private Guid EffectiveOrganizationId =>
        currentRequest.OrganizationId ?? databaseOptions.Value.DefaultOrganizationId;

    public DbSet<Organization> OrganizationsSet => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<PermissionDefinition> PermissionsSet => Set<PermissionDefinition>();
    public DbSet<RolePermissionGrant> RolePermissions => Set<RolePermissionGrant>();
    public DbSet<MembershipRole> MembershipRoles => Set<MembershipRole>();
    public DbSet<RefreshTokenSession> RefreshTokenSessionsSet => Set<RefreshTokenSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessagesSet => Set<OutboxMessage>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    IQueryable<Organization> IApplicationDbContext.Organizations => OrganizationsSet;
    IQueryable<RefreshTokenSession> IApplicationDbContext.RefreshTokenSessions => RefreshTokenSessionsSet;
    IQueryable<OutboxMessage> IApplicationDbContext.OutboxMessages => OutboxMessagesSet;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("jersey");

        builder.Entity<Organization>(b =>
        {
            b.ToTable("Organizations");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.Slug).IsUnique();
            b.HasQueryFilter(x => !x.IsDeleted && x.Id == EffectiveOrganizationId);
        });
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("Users");
            b.Property(x => x.RowVersion).IsRowVersion();
        });
        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("Roles");
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.OrganizationId, x.NormalizedName }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == EffectiveOrganizationId);
        });
        builder.Entity<OrganizationMembership>(b =>
        {
            b.ToTable("OrganizationMemberships");
            b.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
            b.HasQueryFilter(x => !x.IsDeleted && x.OrganizationId == EffectiveOrganizationId);
        });
        builder.Entity<PermissionDefinition>(b =>
        {
            b.ToTable("Permissions");
            b.Property(x => x.Key).HasMaxLength(150).IsRequired();
            b.HasIndex(x => x.Key).IsUnique();
        });
        builder.Entity<RolePermissionGrant>(b =>
        {
            b.ToTable("RolePermissions");
            b.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == EffectiveOrganizationId);
        });
        builder.Entity<MembershipRole>(b =>
        {
            b.ToTable("MembershipRoles");
            b.HasIndex(x => new { x.MembershipId, x.RoleId }).IsUnique();
            b.HasQueryFilter(x => x.OrganizationId == EffectiveOrganizationId);
        });
        builder.Entity<RefreshTokenSession>(b =>
        {
            b.ToTable("RefreshTokenSessions");
            b.Property(x => x.TokenHash).HasMaxLength(32).IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => x.FamilyId);
            b.HasQueryFilter(x => x.OrganizationId == EffectiveOrganizationId);
        });
        builder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLogs");
            b.Property(x => x.DataJson).HasColumnType("nvarchar(max)");
            b.HasQueryFilter(x => x.OrganizationId == EffectiveOrganizationId);
        });
        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages");
            b.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            b.HasIndex(x => new { x.ProcessedAtUtc, x.CreatedAtUtc });
            b.HasQueryFilter(x => x.OrganizationId == EffectiveOrganizationId);
        });
        builder.Entity<FeatureFlag>(b =>
        {
            b.ToTable("FeatureFlags");
            b.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
            b.HasQueryFilter(x => !x.IsDeleted && x.OrganizationId == EffectiveOrganizationId);
        });

        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(IAuditableEntity).IsAssignableFrom(t.ClrType)))
        {
            builder.Entity(entityType.ClrType).Property(nameof(IAuditableEntity.CreatedBy)).HasMaxLength(256);
            builder.Entity(entityType.ClrType).Property(nameof(IAuditableEntity.ModifiedBy)).HasMaxLength(256);
            builder.Entity(entityType.ClrType).Property(nameof(IAuditableEntity.RowVersion)).IsRowVersion();
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = TimeProvider.System.GetUtcNow();
        var actor = currentRequest.Actor;
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = actor;
            }
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.ModifiedAtUtc = now;
                entry.Entity.ModifiedBy = actor;
            }
        }
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>()
                     .Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAtUtc = now;
            entry.Entity.DeletedBy = actor;
        }

        var domainEvents = ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .SelectMany(e =>
            {
                var events = e.DomainEvents.ToArray();
                e.ClearDomainEvents();
                return events;
            })
            .ToArray();

        foreach (var notification in DomainEventMapper.ToNotifications(
                     domainEvents, EffectiveOrganizationId, currentRequest.CorrelationId))
        {
            await publisher.Publish(notification, cancellationToken).ConfigureAwait(false);
        }

        FlushIntegrationEvents();
        return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void FlushIntegrationEvents()
    {
        if (integrationEvents is not OutboxIntegrationEventPublisher outbox)
        {
            return;
        }

        foreach (var envelope in outbox.Dequeue())
        {
            OutboxMessagesSet.Add(new OutboxMessage
            {
                OrganizationId = envelope.OrganizationId,
                Type = envelope.EventType,
                CorrelationId = envelope.CorrelationId,
                PayloadJson = JsonSerializer.Serialize(envelope)
            });
        }
    }
}

public sealed class OutboxIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly List<IntegrationEventEnvelope> _pending = [];

    public void Enqueue(IntegrationEventEnvelope envelope) => _pending.Add(envelope);

    public IReadOnlyList<IntegrationEventEnvelope> Dequeue()
    {
        var batch = _pending.ToArray();
        _pending.Clear();
        return batch;
    }
}

public sealed class IdentityService(
    JerseyOsDbContext db,
    UserManager<ApplicationUser> users,
    ICurrentRequest current,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IIdentityService
{
    public async Task<IssuedAuth?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeEmail(email);
        var user = await db.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalized && x.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (user is null || !await users.CheckPasswordAsync(user, password).ConfigureAwait(false))
        {
            return null;
        }
        var membership = await db.OrganizationMemberships.IgnoreQueryFilters()
            .Where(x => x.UserId == user.Id && !x.IsDeleted)
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return membership is null ? null : await IssueAsync(user, membership.OrganizationId, Guid.NewGuid(), cancellationToken);
    }

    public async Task<IssuedAuth?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        var candidates = await db.RefreshTokenSessionsSet.IgnoreQueryFilters()
            .Where(x => x.TokenHash == hash)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var session = candidates.SingleOrDefault(x => CryptographicOperations.FixedTimeEquals(x.TokenHash, hash));
        if (session is null) return null;

        var now = timeProvider.GetUtcNow();
        if (!session.IsUsable(now))
        {
            await RevokeFamilyAsync(session.FamilyId, now, "refresh-token-replay", cancellationToken);
            return null;
        }
        var user = await db.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == session.UserId && x.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (user is null) return null;
        var issued = await IssueAsync(user, session.OrganizationId, session.FamilyId, cancellationToken);
        var replacementHash = SHA256.HashData(Encoding.UTF8.GetBytes(issued.RefreshToken));
        var replacement = await db.RefreshTokenSessionsSet.IgnoreQueryFilters()
            .SingleAsync(x => x.TokenHash == replacementHash, cancellationToken).ConfigureAwait(false);
        session.Rotate(replacement.Id, now);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return issued;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        var session = await db.RefreshTokenSessionsSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken).ConfigureAwait(false);
        if (session is not null)
        {
            await RevokeFamilyAsync(session.FamilyId, timeProvider.GetUtcNow(), "logout", cancellationToken);
        }
    }

    public async Task<UserResponse?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        if (current.UserId is not { } userId || current.OrganizationId is not { } organizationId) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return null;
        var permissions = await PermissionKeysAsync(userId, organizationId, cancellationToken);
        return new UserResponse(user.Id, DisplayNameFor(user), user.Email ?? string.Empty, organizationId, permissions);
    }

    private async Task<IssuedAuth> IssueAsync(
        ApplicationUser user, Guid organizationId, Guid familyId, CancellationToken cancellationToken)
    {
        var options = jwtOptions.Value;
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(options.AccessMinutes);
        var permissionKeys = await PermissionKeysAsync(user.Id, organizationId, cancellationToken);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("org", organizationId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(permissionKeys.Select(p => new Claim("permission", p)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims,
            now.UtcDateTime, expires.UtcDateTime, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokenSessionsSet.Add(new RefreshTokenSession
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            FamilyId = familyId,
            TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)),
            ExpiresAtUtc = now.AddDays(options.RefreshDays)
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var dto = new UserResponse(user.Id, DisplayNameFor(user), user.Email ?? string.Empty, organizationId, permissionKeys);
        return new IssuedAuth(
            new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, dto),
            refreshToken, now.AddDays(options.RefreshDays));
    }

    private static string DisplayNameFor(ApplicationUser user) =>
        string.IsNullOrWhiteSpace(user.UserName) ? (user.Email ?? user.Id.ToString()) : user.UserName;

    private async Task<string[]> PermissionKeysAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken) =>
        await (from membership in db.OrganizationMemberships.IgnoreQueryFilters()
               join membershipRole in db.MembershipRoles.IgnoreQueryFilters() on membership.Id equals membershipRole.MembershipId
               join rolePermission in db.RolePermissions.IgnoreQueryFilters() on membershipRole.RoleId equals rolePermission.RoleId
               join permission in db.PermissionsSet on rolePermission.PermissionId equals permission.Id
               where membership.UserId == userId && membership.OrganizationId == organizationId && !membership.IsDeleted
               select permission.Key).Distinct().ToArrayAsync(cancellationToken).ConfigureAwait(false);

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var family = await db.RefreshTokenSessionsSet.IgnoreQueryFilters()
            .Where(x => x.FamilyId == familyId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var session in family) session.Revoke(now, reason);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class OutboxDispatcher(JerseyOsDbContext db, TimeProvider timeProvider)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task DispatchAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessagesSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == messageId, cancellationToken).ConfigureAwait(false);
        if (message is null || message.ProcessedAtUtc is not null) return;
        message.Attempts++;
        // Domain-specific transports subscribe here; completion is idempotently persisted.
        message.ProcessedAtUtc = timeProvider.GetUtcNow();
        message.Error = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class OutboxPump(JerseyOsDbContext db, IBackgroundJobClient jobs)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task EnqueuePendingAsync(CancellationToken cancellationToken)
    {
        var ids = await db.OutboxMessagesSet.IgnoreQueryFilters()
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.Id)
            .Take(100)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        foreach (var id in ids)
            jobs.Enqueue<OutboxDispatcher>(dispatcher => dispatcher.DispatchAsync(id, CancellationToken.None));
    }
}

public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"Redis latency {latency.TotalMilliseconds:F0}ms");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis unavailable", exception);
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.Section));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        var connectionString = configuration[$"{DatabaseOptions.Section}:ConnectionString"]
            ?? throw new InvalidOperationException("Database connection string is required.");
        var redisConnection = configuration["Redis:ConnectionString"] ?? "localhost:6379,abortConnect=false";
        services.AddScoped<OutboxIntegrationEventPublisher>();
        services.AddScoped<IIntegrationEventPublisher>(sp => sp.GetRequiredService<OutboxIntegrationEventPublisher>());
        services.AddDbContext<JerseyOsDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<JerseyOsDbContext>());
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.Password.RequiredLength = 12;
            o.Password.RequireNonAlphanumeric = true;
            o.User.RequireUniqueEmail = true;
        }).AddRoles<ApplicationRole>().AddEntityFrameworkStores<JerseyOsDbContext>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<IExternalIdentityProvider, UnsupportedExternalIdentityProvider>();
        services.AddSingleton<IPasswordlessIdentityProvider, UnsupportedPasswordlessIdentityProvider>();
        services.AddScoped<OutboxDispatcher>();
        services.AddScoped<OutboxPump>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddHealthChecks()
            .AddDbContextCheck<JerseyOsDbContext>("sql", tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
        services.AddHangfire(c => c.UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            PrepareSchemaIfNecessary = true,
            QueuePollInterval = TimeSpan.FromSeconds(15)
        }));

        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (!string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
                throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub
                };
            });
            services.AddAuthorizationBuilder()
                .AddPolicy(Permissions.PlatformRead, p => p.RequireClaim("permission", Permissions.PlatformRead))
                .AddPolicy(Permissions.PlatformAdmin, p => p.RequireClaim("permission", Permissions.PlatformAdmin))
                .AddPolicy(Permissions.SystemHealthRead, p => p.RequireClaim("permission", Permissions.SystemHealthRead));
        }
        return services;
    }
}
