using System.Security.Claims;
using System.Text.Encodings.Web;
using JerseyOs.Application;
using JerseyOs.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JerseyOs.Infrastructure;

public static class IntegrationModelBuilderExtensions
{
    public static void ConfigureIntegrations(this ModelBuilder builder, Guid effectiveOrganizationId)
    {
        builder.Entity<ApiKeyCredential>(b =>
        {
            b.ToTable("integration_api_keys");
            b.Property(x => x.Name).HasMaxLength(ApiKeyCredential.NameMaxLength).IsRequired();
            b.Property(x => x.Prefix).HasMaxLength(ApiKeyCredential.DisplayPrefixMaxLength).IsRequired();
            b.Property(x => x.SecretHash).HasMaxLength(32).IsRequired();
            b.Property(x => x.Scopes).HasMaxLength(ApiKeyCredential.ScopesMaxLength).IsRequired();
            b.HasIndex(x => x.SecretHash).IsUnique();
            b.HasIndex(x => new { x.OrganizationId, x.Prefix });
            b.HasQueryFilter(x => x.OrganizationId == effectiveOrganizationId);
        });
    }
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    JerseyOsDbContext db,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public static bool HasPresentedKey(HttpRequest request) => TryReadKey(request, out _);

    public static bool TryReadKey(HttpRequest request, out string key)
    {
        if (request.Headers.TryGetValue(HeaderName, out var header) &&
            !string.IsNullOrWhiteSpace(header))
        {
            key = header.ToString().Trim();
            return key.StartsWith(ApiKeyCredential.PlaintextPrefix, StringComparison.Ordinal);
        }

        var authorization = request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization[bearer.Length..].Trim();
            if (token.StartsWith(ApiKeyCredential.PlaintextPrefix, StringComparison.Ordinal))
            {
                key = token;
                return true;
            }
        }

        key = string.Empty;
        return false;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryReadKey(Request, out var presented))
        {
            return AuthenticateResult.NoResult();
        }

        var hash = ApiKeySecrets.Hash(presented);
        var credential = await db.ApiKeysSet.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.SecretHash == hash)
            .ConfigureAwait(false);
        if (credential is null || !credential.IsUsable)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        credential.TouchLastUsed(time.GetUtcNow());
        await db.SaveChangesAsync().ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, credential.Id.ToString()),
            new("org", credential.OrganizationId.ToString()),
            new("api_key", credential.Id.ToString("N")),
            new("api_key_prefix", credential.Prefix)
        };
        claims.AddRange(credential.ScopeKeys.Select(scope => new Claim("permission", scope)));
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
