namespace JerseyOs.Contracts;

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest;
public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, UserResponse User);
public sealed record UserResponse(Guid Id, string Email, Guid OrganizationId, IReadOnlyCollection<string> Permissions);
public sealed record ProblemResponse(string Type, string Title, int Status, string? Detail, string TraceId);
