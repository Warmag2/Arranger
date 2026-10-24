using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Faemiyah.Arranger.Contracts.Accounts;
using Faemiyah.Arranger.Data.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Faemiyah.Repository.Interfaces;
using Faemiyah.Repository.Models;

namespace Faemiyah.Arranger.Server.Authentication;

/// <summary>
/// Registration, password verification and password changes against the user repository.
/// </summary>
/// <remarks>
/// There is deliberately no per-account lockout, since anyone could then lock a friend out. Failed logins are instead
/// logged in a fixed format with the client IP, so that fail2ban can ban the address.
/// </remarks>
public sealed class AccountService
{
    /// <summary>
    /// The fixed start of the failed-login log line, to be matched by a fail2ban filter.
    /// </summary>
    public const string FailedLoginLogPrefix = "Arranger failed login";

    private readonly ILogger<AccountService> _logger;
    private readonly IEntityRepositoryWithUuid<long, User> _users;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly Lazy<string> _dummyPasswordHash;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="users">The user repository.</param>
    /// <param name="passwordHasher">The password hasher.</param>
    public AccountService(ILogger<AccountService> logger, IEntityRepositoryWithUuid<long, User> users, IPasswordHasher<User> passwordHasher)
    {
        _logger = logger;
        _users = users;
        _passwordHasher = passwordHasher;
        _dummyPasswordHash = new Lazy<string>(() => passwordHasher.HashPassword(new User(), Guid.NewGuid().ToString()));
    }

    /// <summary>
    /// Create a new user.
    /// </summary>
    /// <param name="userName">The user name, in the casing the user wants to see.</param>
    /// <param name="password">The password.</param>
    /// <param name="timeZoneId">The browser's IANA time zone, or <b>null</b> for UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, with the created user on success.</returns>
    public async Task<AccountResult> RegisterAsync(string userName, string password, string? timeZoneId, CancellationToken cancellationToken = default)
    {
        if (!AccountRules.IsValidUserName(userName))
        {
            return new AccountResult(AccountStatus.InvalidUserName, null);
        }

        if (!AccountRules.IsValidPassword(password))
        {
            return new AccountResult(AccountStatus.InvalidPassword, null);
        }

        var normalizedUserName = AccountRules.NormalizeUserName(userName);
        var user = new User
        {
            UserName = userName,
            TimeZoneId = AccountRules.IsKnownTimeZone(timeZoneId) ? timeZoneId! : "UTC",
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        var created = await _users.AddAsync(user, u => u.NormalizedUserName == normalizedUserName, cancellationToken);
        if (!created.IsSuccess)
        {
            return new AccountResult(AccountStatus.UserNameTaken, null);
        }

        _logger.LogInformation("Registered user {UserId}.", created.Value.UniqueId);
        return new AccountResult(AccountStatus.Success, created.Value);
    }

    /// <summary>
    /// Verify a user name and password.
    /// </summary>
    /// <param name="userName">The user name, in any casing.</param>
    /// <param name="password">The password.</param>
    /// <param name="clientAddress">The client IP address, logged on failure.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user, or <b>null</b> when the user name or password is wrong.</returns>
    public async Task<User?> LoginAsync(string userName, string password, IPAddress? clientAddress, CancellationToken cancellationToken = default)
    {
        var isValidUserName = AccountRules.IsValidUserName(userName);
        User? user = null;
        if (isValidUserName)
        {
            var normalizedUserName = AccountRules.NormalizeUserName(userName);
            user = await _users.GetAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken: cancellationToken);
        }

        // Hash even for unknown users, so that the response time doesn't reveal which names exist.
        var verification = _passwordHasher.VerifyHashedPassword(user ?? new User(), user?.PasswordHash ?? _dummyPasswordHash.Value, password ?? string.Empty);

        if (user == null || verification == PasswordVerificationResult.Failed)
        {
            // Only names with safe characters are logged, so the line cannot be forged.
            _logger.LogWarning(
                FailedLoginLogPrefix + " for user {UserName} from {ClientAddress}",
                isValidUserName ? userName : "-",
                clientAddress?.ToString() ?? "unknown");
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await TryRehashAsync(user, password!, cancellationToken);
        }

        return user;
    }

    /// <summary>
    /// Change a user's password. This also changes the security stamp, signing out every other session.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome, with the updated user on success.</returns>
    public async Task<AccountResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!AccountRules.IsValidPassword(newPassword))
        {
            return new AccountResult(AccountStatus.InvalidPassword, null);
        }

        var user = await _users.GetAsync(u => u.UniqueId == userId, cancellationToken: cancellationToken);
        if (user == null)
        {
            return new AccountResult(AccountStatus.UserNotFound, null);
        }

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            return new AccountResult(AccountStatus.WrongPassword, null);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid();

        var updated = await _users.UpdateAsync(user, cancellationToken);
        if (!updated.IsSuccess)
        {
            return new AccountResult(updated.Status == OperationStatus.NotFound ? AccountStatus.UserNotFound : AccountStatus.Conflict, null);
        }

        _logger.LogInformation("User {UserId} changed their password.", userId);
        return new AccountResult(AccountStatus.Success, updated.Value);
    }

    /// <summary>
    /// Whether the user still exists and the stamp from the cookie is still current.
    /// </summary>
    /// <param name="userId">The public user ID.</param>
    /// <param name="securityStamp">The security stamp from the cookie.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><b>true</b> when the cookie may still be used.</returns>
    public async Task<bool> IsSecurityStampValidAsync(Guid userId, Guid securityStamp, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetAsync(u => u.UniqueId == userId, cancellationToken: cancellationToken);
        return user != null && user.SecurityStamp == securityStamp;
    }

    /// <summary>
    /// Build the cookie principal for a user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The principal.</returns>
    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.UniqueId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ArrangerClaimTypes.SecurityStamp, user.SecurityStamp.ToString()),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Read the public user ID from a principal.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The user ID, or <b>null</b> when missing or malformed.</returns>
    public static Guid? GetUserId(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
    }

    private async Task TryRehashAsync(User user, string password, CancellationToken cancellationToken)
    {
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        var result = await _users.UpdateAsync(user, cancellationToken);
        if (!result.IsSuccess)
        {
            _logger.LogInformation("Skipped rehashing the password of user {UserId}: {Status}.", user.UniqueId, result.Status);
        }
    }
}
