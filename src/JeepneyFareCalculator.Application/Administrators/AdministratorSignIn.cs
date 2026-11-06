using System.Security.Cryptography;
using System.Text;

namespace JeepneyFareCalculator.Application.Administrators;

/// <summary>
/// Checks a username and password against the one configured Administrator.
/// </summary>
public sealed class AdministratorSignIn(AdministratorSettings settings)
{
    /// <summary>The configured Administrator's username, for the signed-in identity.</summary>
    public string Username => settings.Username;

    public bool AreCredentialsValid(string username, string password)
    {
        if (settings.Username.Length == 0 || settings.PasswordHash.Length == 0)
        {
            throw new InvalidOperationException(
                $"The Administrator is not configured. Set '{AdministratorSettings.SectionName}:Username' and " +
                $"'{AdministratorSettings.SectionName}:PasswordHash' with dotnet user-secrets (see back-end/CLAUDE.md).");
        }

        // Both checks always run, so a wrong username takes as long to reject as a wrong password.
        var usernameMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(username),
            Encoding.UTF8.GetBytes(settings.Username));
        var passwordMatches = PasswordHashing.Verify(password, settings.PasswordHash);

        return usernameMatches && passwordMatches;
    }
}
