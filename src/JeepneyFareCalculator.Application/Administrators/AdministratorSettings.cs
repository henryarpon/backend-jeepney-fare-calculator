namespace JeepneyFareCalculator.Application.Administrators;

/// <summary>
/// The one Administrator's credentials, read from the <c>Administrator</c>
/// configuration section. The password hash belongs in user-secrets (or another
/// secret store), never in a committed settings file.
/// </summary>
public sealed class AdministratorSettings
{
    public const string SectionName = "Administrator";

    public string Username { get; set; } = "";

    /// <summary>A hash made by <see cref="PasswordHashing.Hash"/>.</summary>
    public string PasswordHash { get; set; } = "";
}
