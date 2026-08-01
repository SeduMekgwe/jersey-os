using System.Security.Claims;
using Asp.Versioning;
using FluentValidation;
using JerseyOs.Application;
using JerseyOs.Contracts;
using JerseyOs.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JerseyOs.Api;

public sealed class HttpCurrentRequest(IHttpContextAccessor accessor) : ICurrentRequest
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;
    public Guid? UserId => Guid.TryParse(User?.FindFirstValue("sub"), out var value) ? value : null;
    public Guid? OrganizationId => Guid.TryParse(User?.FindFirstValue("org"), out var value) ? value : null;
    public string Actor => UserId?.ToString() ?? "system";
    public string CorrelationId => accessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
}

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                string.Join("; ", validation.Errors.Select(x => x.ErrorMessage))),
            InvalidOperationException invalid => (
                StatusCodes.Status400BadRequest,
                "Request rejected",
                invalid.Message),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                "The resource was modified by another request."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", (string?)null)
        };
        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = $"https://www.rfc-editor.org/rfc/rfc9110#name-{status}",
                Title = title,
                Status = status,
                Detail = detail
            },
            Exception = exception
        });
    }
}

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    private const string HostRefreshCookie = "__Host-jersey-refresh";
    private const string DevRefreshCookie = "jersey-refresh";

    private string RefreshCookieName => Request.IsHttps ? HostRefreshCookie : DevRefreshCookie;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var issued = await sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        if (issued is null) return Unauthorized();
        SetRefreshCookie(issued);
        return Ok(issued.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        if (!TryReadRefreshCookie(out var token)) return Unauthorized();
        var issued = await sender.Send(new RefreshCommand(token), cancellationToken);
        if (issued is null)
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }
        SetRefreshCookie(issued);
        return Ok(issued.Response);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        TryReadRefreshCookie(out var token);
        await sender.Send(new LogoutCommand(token), cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await sender.Send(new GetMeQuery(), cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpGet("admin-probe")]
    [Authorize(Policy = Permissions.PlatformAdmin)]
    public IActionResult AdminProbe() => NoContent();

    private bool TryReadRefreshCookie(out string token) =>
        Request.Cookies.TryGetValue(RefreshCookieName, out token!)
        || Request.Cookies.TryGetValue(HostRefreshCookie, out token!)
        || Request.Cookies.TryGetValue(DevRefreshCookie, out token!);

    private void SetRefreshCookie(IssuedAuth issued) =>
        Response.Cookies.Append(RefreshCookieName, issued.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = issued.RefreshExpiresAtUtc,
            IsEssential = true
        });

    private void DeleteRefreshCookie()
    {
        foreach (var name in new[] { RefreshCookieName, HostRefreshCookie, DevRefreshCookie }.Distinct())
        {
            Response.Cookies.Delete(name, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }
    }
}

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = Permissions.SystemHealthRead)]
[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController(HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType<SystemHealthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemHealthResponse>> Health(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var checks = report.Entries
            .Select(entry => new HealthCheckResponse(
                entry.Key,
                entry.Value.Status.ToString(),
                entry.Value.Description ?? entry.Value.Exception?.Message))
            .ToArray();
        return Ok(new SystemHealthResponse(report.Status.ToString(), DateTimeOffset.UtcNow, checks));
    }
}
