using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Faemiyah.Repository.Interfaces;

namespace Faemiyah.Arranger.Data.Queries;

/// <summary>
/// The public part of a user, as needed by arrangements.
/// </summary>
/// <param name="Id">The internal user ID.</param>
/// <param name="UniqueId">The public user ID.</param>
/// <param name="UserName">The handle.</param>
/// <param name="DefaultCurrency">The ISO 4217 default currency.</param>
public sealed record UserProfile(long Id, Guid UniqueId, string UserName, string DefaultCurrency);
