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
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                string.Join("; ", validation.Errors.Select(x => x.ErrorMessage))),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", (string?)null)
        };
        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
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
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    private const string RefreshCookie = "__Host-jersey-refresh";

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
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var token)) return Unauthorized();
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
        Request.Cookies.TryGetValue(RefreshCookie, out var token);
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

    private void SetRefreshCookie(IssuedAuth issued) =>
        Response.Cookies.Append(RefreshCookie, issued.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = issued.RefreshExpiresAtUtc,
            IsEssential = true
        });

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
}
