using System;
using System.Collections.Generic;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// An expense, split equally between its participants.
/// </summary>
/// <param name="Id">The expense ID. Later expenses have larger IDs.</param>
/// <param name="Description">The description.</param>
/// <param name="Amount">The amount, positive, in <paramref name="Currency"/>.</param>
/// <param name="Currency">The ISO 4217 currency.</param>
/// <param name="PaidByUserId">The user ID of the member who paid.</param>
/// <param name="ParticipantUserIds">The user IDs of the members who share it, in ID order.</param>
/// <param name="Date">The date it was paid.</param>
/// <param name="IsSettlementPayment">Whether it is a payment between members, with the receiver as the only participant. Only affects how it is shown.</param>
public sealed record ExpenseSnapshot(
    long Id,
    string Description,
    decimal Amount,
    string Currency,
    Guid PaidByUserId,
    IReadOnlyList<Guid> ParticipantUserIds,
    DateOnly Date,
    bool IsSettlementPayment);
