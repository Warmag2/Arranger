using System;
using System.Linq;

namespace Faemiyah.Arranger.Contracts.Arrangements;

/// <summary>
/// Limits and validation for expenses, shared by the server and the client.
/// </summary>
public static class ExpenseRules
{
    /// <summary>
    /// The maximum length of a description.
    /// </summary>
    public const int DescriptionMaxLength = 200;

    /// <summary>
    /// The maximum number of expenses in an arrangement.
    /// </summary>
    public const int MaxExpenses = 2000;

    /// <summary>
    /// The largest amount.
    /// </summary>
    public const decimal MaxAmount = 1_000_000_000m;

    /// <summary>
    /// The largest exchange rate.
    /// </summary>
    public const decimal MaxRate = 1_000_000_000m;

    /// <summary>
    /// The maximum number of decimals in an exchange rate.
    /// </summary>
    public const int RateDecimals = 8;

    /// <summary>
    /// The largest conversion loss, in percent.
    /// </summary>
    public const decimal MaxLossPercent = 50;

    /// <summary>
    /// The maximum number of decimals in a conversion loss.
    /// </summary>
    public const int LossDecimals = 2;

    /// <summary>
    /// Validate an exchange rate.
    /// </summary>
    /// <param name="rate">The units of a currency one unit of the base currency buys.</param>
    /// <returns>An error message, or <b>null</b> when valid.</returns>
    public static string? ValidateRate(decimal rate)
    {
        return rate <= 0 || rate > MaxRate || decimal.Round(rate, RateDecimals) != rate
            ? $"The rate must be positive, at most {MaxRate:N0}, with at most {RateDecimals} decimals."
            : null;
    }

    /// <summary>
    /// Validate a conversion loss.
    /// </summary>
    /// <param name="lossPercent">The average loss on conversion, in percent.</param>
    /// <returns>An error message, or <b>null</b> when valid.</returns>
    public static string? ValidateLoss(decimal lossPercent)
    {
        return lossPercent < 0 || lossPercent > MaxLossPercent || decimal.Round(lossPercent, LossDecimals) != lossPercent
            ? $"The conversion loss must be between 0 and {MaxLossPercent} %, with at most {LossDecimals} decimals."
            : null;
    }

    /// <summary>
    /// Validate an expense whose description is normalized. Membership is checked by the server.
    /// </summary>
    /// <param name="request">The expense.</param>
    /// <returns>An error message, or <b>null</b> when valid.</returns>
    public static string? Validate(ExpenseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Description.Length == 0)
        {
            return "The description is required.";
        }

        if (request.Description.Length > DescriptionMaxLength)
        {
            return $"The description can be at most {DescriptionMaxLength} characters long.";
        }

        if (!Currencies.IsKnown(request.Currency))
        {
            return "Unknown currency.";
        }

        if (request.Amount <= 0 || request.Amount > MaxAmount)
        {
            return $"The amount must be positive and at most {MaxAmount:N0}.";
        }

        var decimals = Currencies.Decimals(request.Currency);
        if (decimal.Round(request.Amount, decimals) != request.Amount)
        {
            return $"{request.Currency} amounts have at most {decimals} decimals.";
        }

        if (request.Date.Year < ArrangementRules.MinTimeOptionYear || request.Date.Year > ArrangementRules.MaxTimeOptionYear)
        {
            return $"The date must be between the years {ArrangementRules.MinTimeOptionYear} and {ArrangementRules.MaxTimeOptionYear}.";
        }

        var participants = request.ParticipantUserIds;
        if (participants is not { Count: > 0 } || participants.Distinct().Count() != participants.Count)
        {
            return "Choose who shares the expense, each once.";
        }

        return request.IsSettlementPayment && (participants.Count != 1 || participants[0] == request.PaidByUserId)
            ? "A payment goes to one other member."
            : null;
    }
}
