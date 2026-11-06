using JeepneyFareCalculator.Application.Administrators;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace JeepneyFareCalculator.Api.Authentication;

/// <summary>
/// Cookie authentication for the one Administrator. Admin endpoints opt in with
/// <c>[Authorize]</c>; a request without a valid session gets 401 ProblemDetails,
/// never a redirect to a login page.
/// </summary>
internal static class AdminAuthentication
{
    public const string CookieName = "JeepneyFareAdmin";

    // Sliding: the session lasts this long after the Administrator's last request,
    // so it stays signed in while they work and ends when they walk away.
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(2);

    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services)
    {
        // Read when first needed, not at start-up, so the API (and the commuter
        // endpoints) still start when the Administrator isn't configured yet.
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IConfiguration>()
                .GetSection(AdministratorSettings.SectionName)
                .Get<AdministratorSettings>() ?? new AdministratorSettings());
        services.AddSingleton<AdministratorSignIn>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = IdleTimeout;
                options.SlidingExpiration = true;
                options.SessionStore = new InMemoryTicketStore();

                // An API answers with a status code; UseStatusCodePages turns it into ProblemDetails.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();

        return services;
    }
}
