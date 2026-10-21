using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RepositoryPrototype.Interfaces;

namespace Faemiyah.Arranger.Data.Queries;

/// <summary>
/// A user's picture, or the data to draw a placeholder when there is none.
/// </summary>
/// <param name="UserName">The handle.</param>
/// <param name="Data">The image bytes, or <b>null</b> for none.</param>
/// <param name="ContentType">The MIME type of <paramref name="Data"/>.</param>
/// <param name="Version">The picture row version, or <b>null</b> for none.</param>
public sealed record UserPictureData(string UserName, byte[]? Data, string? ContentType, uint? Version);
