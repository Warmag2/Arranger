using System;
using System.Collections.Generic;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Add or replace an expense. See <see cref="ExpenseRules.Validate"/>.
/// </summary>
/// <param name="Description">The description.</param>
/// <param name="Amount">The amount.</param>
/// <param name="Currency">The ISO 4217 currency.</param>
/// <param name="PaidByUserId">The member who paid.</param>
/// <param name="ParticipantUserIds">The members who share it.</param>
/// <param name="Date">The date it was paid.</param>
/// <param name="IsSettlementPayment">Whether it is a payment to the only participant. Only affects how it is shown.</param>
public sealed record ExpenseRequest(
    string Description,
    decimal Amount,
    string Currency,
    Guid PaidByUserId,
    IReadOnlyList<Guid> ParticipantUserIds,
    DateOnly Date,
    bool IsSettlementPayment);
