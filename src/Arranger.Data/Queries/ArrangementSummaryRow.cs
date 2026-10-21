using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RepositoryPrototype.Interfaces;

namespace Faemiyah.Arranger.Data.Queries;

/// <summary>
/// One of a user's arrangements.
/// </summary>
/// <param name="UniqueId">The arrangement ID.</param>
/// <param name="Title">The title.</param>
/// <param name="CreatedAt">The creation time in UTC.</param>
/// <param name="MemberCount">The number of members.</param>
/// <param name="IsOwner">Whether the user created the arrangement.</param>
/// <param name="IsAdmin">Whether the user is an admin.</param>
public sealed record ArrangementSummaryRow(Guid UniqueId, string Title, DateTime CreatedAt, int MemberCount, bool IsOwner, bool IsAdmin);
