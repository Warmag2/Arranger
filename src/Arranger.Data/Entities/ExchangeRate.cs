using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Faemiyah.Repository.Entities;

namespace Faemiyah.Arranger.Data.Entities;

/// <summary>
/// A currency the arrangement uses besides its base currency, with its value in the base currency once known.
/// </summary>
[Index(nameof(ArrangementId), nameof(Currency), IsUnique = true)]
public class ExchangeRate : EntityBase<long>
{
    /// <summary>
    /// The arrangement's ID.
    /// </summary>
    public long ArrangementId { get; set; }

    /// <summary>
    /// The arrangement.
    /// </summary>
    public Arrangement? Arrangement { get; set; }

    /// <summary>
    /// The ISO 4217 currency, other than the base currency.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The units of <see cref="Currency"/> one unit of the base currency buys, or <b>null</b> when not yet known.
    /// </summary>
    [Column(TypeName = "numeric(19,8)")]
    public decimal? Rate { get; set; }

    /// <summary>
    /// The average loss on converting to <see cref="Currency"/>, in percent.
    /// </summary>
    [Column(TypeName = "numeric(5,2)")]
    public decimal LossPercent { get; set; }

    /// <summary>
    /// Copy the rate's own columns, for an update that must not touch this instance.
    /// </summary>
    /// <returns>The copy.</returns>
    public ExchangeRate CloneDetails()
    {
        var clone = (ExchangeRate)MemberwiseClone();
        clone.Arrangement = null;
        return clone;
    }
}
