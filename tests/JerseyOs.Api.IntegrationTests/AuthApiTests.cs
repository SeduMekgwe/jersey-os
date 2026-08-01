using System.Net;
using System.Net.Http.Json;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using JerseyOs.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace JerseyOs.Api.IntegrationTests;

public sealed class AuthApiTests : IAsyncLifetime
{
    private MsSqlContainer? _sql;
    private RedisContainer? _redis;
    private WebApplicationFactory<Program>? _factory;
    private bool _ready;

    public async Task InitializeAsync()
    {
        try
        {
            _sql = new MsSqlBuilder().Build();
            _redis = new RedisBuilder().Build();
            await _sql.StartAsync();
            await _redis.StartAsync();
            var sqlCs = _sql.GetConnectionString();
            var redisCs = $"{_redis.GetConnectionString()},abortConnect=false";
            var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
            var settings = new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = sqlCs,
                ["Database:DefaultOrganizationId"] = orgId.ToString(),
                ["Redis:ConnectionString"] = redisCs,
                ["Jwt:Issuer"] = "JerseyOs",
                ["Jwt:Audience"] = "JerseyOs",
                ["Jwt:SigningKey"] = "integration-test-signing-key-that-is-long-enough-0123456789",
                ["Bootstrap:Enabled"] = "false",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
            };

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            });

            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<JerseyOsDbContext>();
            await db.Database.MigrateAsync();
            await Bootstrapper.RunAsync(
                scope.ServiceProvider,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Bootstrap:Enabled"] = "true",
                    ["Database:DefaultOrganizationId"] = orgId.ToString(),
                    ["Bootstrap:OrganizationName"] = "Jersey No.10 Collective",
                    ["Bootstrap:OrganizationSlug"] = "jersey-no10-collective",
                    ["Bootstrap:AdminEmail"] = "admin@example.invalid",
                    ["Bootstrap:AdminPassword"] = "Str0ng!Passw0rd#1"
                }).Build(),
                CancellationToken.None);
            _ready = true;
        }
        catch
        {
            _ready = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_redis is not null)
        {
            await _redis.DisposeAsync();
        }

        if (_sql is not null)
        {
            await _sql.DisposeAsync();
        }
    }

    [Fact]
    public async Task LoginRefreshLogoutAndRbacWork()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin@example.invalid", "Str0ng!Passw0rd#1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Contains("system.health.read", auth.User.Permissions);
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, c => c.Contains("jersey-refresh=", StringComparison.Ordinal));

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/system/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/v1/auth/admin-probe")).StatusCode);

        var originalRefresh = ExtractCookie(login, "jersey-refresh");
        Assert.False(string.IsNullOrWhiteSpace(originalRefresh));

        var refresh = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(auth.AccessToken, refreshed.AccessToken);

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replay.Headers.Add("Cookie", $"jersey-refresh={originalRefresh}");
        var replayResponse = await _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false })
            .SendAsync(replay);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/auth/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task RbacDeniesViewerWithoutAdminPermission()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JerseyOsDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var orgId = Guid.Parse("0190f2f2-67a1-7b11-a9e8-5f4921816f53");
        var viewer = new ApplicationUser
        {
            UserName = "viewer@example.invalid",
            Email = "viewer@example.invalid",
            EmailConfirmed = true,
            IsActive = true
        };
        Assert.True((await users.CreateAsync(viewer, "Str0ng!Passw0rd#1")).Succeeded);
        var membership = new OrganizationMembership { OrganizationId = orgId, UserId = viewer.Id };
        db.OrganizationMemberships.Add(membership);
        var role = new ApplicationRole { OrganizationId = orgId, Name = "Viewer", NormalizedName = "VIEWER" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.MembershipRoles.Add(new MembershipRole
        {
            OrganizationId = orgId,
            MembershipId = membership.Id,
            RoleId = role.Id
        });
        var readPermission = await db.PermissionsSet.SingleAsync(x => x.Key == Permissions.PlatformRead);
        db.RolePermissions.Add(new RolePermissionGrant
        {
            OrganizationId = orgId,
            RoleId = role.Id,
            PermissionId = readPermission.Id
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("viewer@example.invalid", "Str0ng!Passw0rd#1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/auth/admin-probe")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/system/health")).StatusCode);
    }

    [Fact]
    public async Task UnauthorizedWithoutToken()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var client = _factory!.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentIsAvailable()
    {
        if (!EnsureDockerOrSkip())
        {
            return;
        }

        var client = _factory!.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/auth/login", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/system/health", document, StringComparison.Ordinal);
    }

    private bool EnsureDockerOrSkip()
    {
        if (_ready)
        {
            return true;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("JERSEYOS_ALLOW_SKIP_DOCKER"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Assert.Fail("Docker is required for API integration tests. Install Docker or set JERSEYOS_ALLOW_SKIP_DOCKER=true for local machines without Docker.");
        return false;
    }

    private static string? ExtractCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            var segment = value.Split(';', 2)[0];
            var parts = segment.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals(name, StringComparison.Ordinal))
            {
                return parts[1];
            }
        }

        return null;
    }
}
