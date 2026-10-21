namespace Faemiyah.Arranger.Contracts;

/// <summary>
/// HTTP headers shared by the API and its client.
/// </summary>
public static class ApiHeaders
{
    /// <summary>
    /// A header the client must send on every state-changing API request.
    /// </summary>
    /// <remarks>
    /// A plain HTML form cannot send custom headers, and a cross-origin script cannot send one without a CORS
    /// preflight, which the server never approves. This blocks cross-site request forgery even from sibling
    /// subdomains, which <c>SameSite=Lax</c> treats as the same site.
    /// </remarks>
    public const string RequestedWith = "X-Requested-With";

    /// <summary>
    /// The value of <see cref="RequestedWith"/>.
    /// </summary>
    public const string RequestedWithValue = "Arranger";
}
