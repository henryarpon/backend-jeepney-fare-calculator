using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace JeepneyFareCalculator.Api.Authentication;

/// <summary>
/// Keeps signed-in sessions on the server, so the cookie only carries a session
/// key. Signing out removes the session here, which means a copy of the old
/// cookie stops working too. Sessions live in memory: restarting the API signs
/// the Administrator out, and this would need a shared store if the API ever ran
/// on more than one server.
/// </summary>
internal sealed class InMemoryTicketStore : ITicketStore
{
    private readonly MemoryCache _tickets = new(new MemoryCacheOptions());

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        Save(key, ticket);
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        Save(key, ticket);
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        return Task.FromResult(_tickets.Get<AuthenticationTicket>(key));
    }

    public Task RemoveAsync(string key)
    {
        _tickets.Remove(key);
        return Task.CompletedTask;
    }

    private void Save(string key, AuthenticationTicket ticket)
    {
        var entryOptions = new MemoryCacheEntryOptions();
        if (ticket.Properties.ExpiresUtc is { } expiresUtc)
        {
            entryOptions.AbsoluteExpiration = expiresUtc;
        }

        _tickets.Set(key, ticket, entryOptions);
    }
}
