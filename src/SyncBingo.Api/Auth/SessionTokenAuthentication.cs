using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SyncBingo.Application;
using SyncBingo.Application.Ports;
using SyncBingo.Domain.Sessions;
using Claim = System.Security.Claims.Claim;

namespace SyncBingo.Api.Auth;

/// <summary>
/// Signs callers in with their session token (ADR 0005): <c>Authorization: Bearer {token}</c>
/// on a <c>/sessions/{code}/…</c> route. Tokens are looked up by hash within that game only,
/// so a token is useless for any other game. What the caller may do is decided by the
/// application layer from the role found here, never by anything the request claims.
/// </summary>
public sealed class SessionTokenAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionStore store,
    ITokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SessionToken";

    private const string RoleClaim = "syncbingo:role";
    private const string CodeClaim = "syncbingo:code";
    private const string ParticipantClaim = "syncbingo:participant";

    /// <summary>The caller an authenticated request signed in as.</summary>
    public static Caller CallerOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var role = Enum.Parse<CallerRole>(user.FindFirstValue(RoleClaim) ?? throw new InvalidOperationException("The request is not signed in."));
        return new Caller(role, user.FindFirstValue(ParticipantClaim));
    }

    /// <summary>The game the caller signed in to.</summary>
    public static GameCode CodeOf(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return GameCode.Parse(user.FindFirstValue(CodeClaim) ?? throw new InvalidOperationException("The request is not signed in."));
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Browsers can't set headers on a WebSocket, so the hub alone also takes ?access_token= (and ?code=).
        var isHub = Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);
        var token = AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                ? header.Parameter
                : isHub ? Request.Query["access_token"].ToString() : null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var raw = Context.GetRouteValue("code") as string ?? (isHub ? Request.Query["code"].ToString() : null);
        if (raw is null || !GameCode.TryParse(raw, out var code))
        {
            return AuthenticateResult.Fail("Session tokens are only valid on a game's routes.");
        }

        var caller = await store.FindTokenAsync(code, tokens.Hash(token), Context.RequestAborted).ConfigureAwait(false);
        if (caller is null)
        {
            return AuthenticateResult.Fail("Unknown or revoked token.");
        }

        List<Claim> claims = [new(RoleClaim, caller.Role.ToString()), new(CodeClaim, code.Value)];
        if (caller.ParticipantId is { } participantId)
        {
            claims.Add(new Claim(ParticipantClaim, participantId));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
