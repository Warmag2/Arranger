namespace Faemiyah.Arranger.Contracts;

/// <summary>
/// An ISO 4217 currency.
/// </summary>
/// <param name="Code">The three-letter code.</param>
/// <param name="Name">The English name.</param>
public sealed record Currency(string Code, string Name);
