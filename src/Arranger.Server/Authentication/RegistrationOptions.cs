namespace Faemiyah.Arranger.Server.Authentication;

/// <summary>
/// Settings for self-registration.
/// </summary>
public class RegistrationOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Registration";

    /// <summary>
    /// Whether anyone may register a new account.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
