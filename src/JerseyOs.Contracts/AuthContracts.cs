namespace JerseyOs.Contracts;

public sealed record LoginRequest(string Email, string Password, Guid? OrganizationId = null);
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, UserResponse User);
public sealed record UserResponse(
    Guid Id,
    string DisplayName,
    string Email,
    Guid OrganizationId,
    string OrganizationName,
    IReadOnlyCollection<string> Permissions);
public sealed record HealthCheckResponse(string Name, string Status, string? Description);
public sealed record SystemHealthResponse(string Status, DateTimeOffset CheckedAt, IReadOnlyCollection<HealthCheckResponse> Checks);
