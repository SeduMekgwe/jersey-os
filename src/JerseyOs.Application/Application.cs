using FluentValidation;
using JerseyOs.Contracts;
using JerseyOs.Domain;
using MediatR;

namespace JerseyOs.Application;

public interface ICurrentRequest
{
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    string Actor { get; }
    string CorrelationId { get; }
}

public interface IApplicationDbContext
{
    IQueryable<Organization> Organizations { get; }
    IQueryable<RefreshTokenSession> RefreshTokenSessions { get; }
    IQueryable<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record IssuedAuth(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAtUtc);

public interface IIdentityService
{
    Task<IssuedAuth?> LoginAsync(string email, string password, CancellationToken cancellationToken);
    Task<IssuedAuth?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentAsync(CancellationToken cancellationToken);
}

public sealed record LoginCommand(string Email, string Password) : IRequest<IssuedAuth?>;
public sealed record RefreshCommand(string RefreshToken) : IRequest<IssuedAuth?>;
public sealed record LogoutCommand(string? RefreshToken) : IRequest;
public sealed record GetMeQuery : IRequest<UserResponse?>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}

public sealed class RefreshValidator : AbstractValidator<RefreshCommand>
{
    public RefreshValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

public sealed class LoginHandler(IIdentityService identity) : IRequestHandler<LoginCommand, IssuedAuth?>
{
    public Task<IssuedAuth?> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        identity.LoginAsync(request.Email, request.Password, cancellationToken);
}

public sealed class RefreshHandler(IIdentityService identity) : IRequestHandler<RefreshCommand, IssuedAuth?>
{
    public Task<IssuedAuth?> Handle(RefreshCommand request, CancellationToken cancellationToken) =>
        identity.RefreshAsync(request.RefreshToken, cancellationToken);
}

public sealed class LogoutHandler(IIdentityService identity) : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken cancellationToken) =>
        identity.LogoutAsync(request.RefreshToken, cancellationToken);
}

public sealed class GetMeHandler(IIdentityService identity) : IRequestHandler<GetMeQuery, UserResponse?>
{
    public Task<UserResponse?> Handle(GetMeQuery request, CancellationToken cancellationToken) =>
        identity.GetCurrentAsync(cancellationToken);
}

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = validators
            .Select(v => v.Validate(request))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();
        if (failures.Length != 0)
        {
            throw new ValidationException(failures);
        }

        return await next().ConfigureAwait(false);
    }
}
