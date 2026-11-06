using System.Security.Claims;
using JeepneyFareCalculator.Application.Administrators;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JeepneyFareCalculator.Api.Controllers;

/// <summary>
/// Signs the Administrator in and out. Every action needs a signed-in session
/// except <see cref="LoginAsync"/>.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public sealed class AdminSessionController(
    AdministratorSignIn signIn,
    ILogger<AdminSessionController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync(AdministratorLoginRequest request)
    {
        if (!signIn.AreCredentialsValid(request.Username, request.Password))
        {
            // Never log the username or password that was tried.
            logger.LogWarning("Administrator sign-in failed: wrong username or password.");
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Wrong username or password.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, signIn.Username)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        logger.LogInformation("Administrator signed in.");
        return NoContent();
    }

    /// <summary>Ends the session on the server and tells the browser to delete the cookie.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        logger.LogInformation("Administrator signed out.");
        return NoContent();
    }

    /// <summary>Lets the admin UI's route guard check whether the session is still signed in.</summary>
    [HttpGet("me")]
    public ActionResult<SignedInAdministrator> Me()
    {
        return new SignedInAdministrator(User.Identity?.Name ?? "");
    }
}
