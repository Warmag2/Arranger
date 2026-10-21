namespace Faemiyah.Arranger.Client.Services;

/// <summary>
/// The parts of an RFC 9457 problem response the client shows.
/// </summary>
/// <param name="Title">The short summary of the problem type.</param>
/// <param name="Detail">The explanation of this occurrence.</param>
public sealed record ProblemResponse(string? Title, string? Detail);
