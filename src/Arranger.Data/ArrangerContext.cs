using Faemiyah.Arranger.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faemiyah.Arranger.Data;

/// <summary>
/// The Arranger database context.
/// </summary>
/// <remarks>
/// Never registered in dependency injection. Repositories obtain a short-lived instance per operation from
/// <see cref="ArrangerContextProvider"/>.
/// </remarks>
public class ArrangerContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArrangerContext"/> class.
    /// </summary>
    /// <param name="options">The context options.</param>
    public ArrangerContext(DbContextOptions<ArrangerContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// The users.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// The profile pictures.
    /// </summary>
    public DbSet<UserPicture> UserPictures => Set<UserPicture>();

    /// <summary>
    /// The arrangements.
    /// </summary>
    public DbSet<Arrangement> Arrangements => Set<Arrangement>();

    /// <summary>
    /// The arrangement memberships.
    /// </summary>
    public DbSet<ArrangementMember> ArrangementMembers => Set<ArrangementMember>();

    /// <summary>
    /// The proposed times.
    /// </summary>
    public DbSet<TimeOption> TimeOptions => Set<TimeOption>();

    /// <summary>
    /// The answers to proposed times.
    /// </summary>
    public DbSet<Vote> Votes => Set<Vote>();

    /// <summary>
    /// The expenses.
    /// </summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>
    /// The shares in expenses.
    /// </summary>
    public DbSet<ExpenseParticipation> ExpenseParticipations => Set<ExpenseParticipation>();

    /// <summary>
    /// The exchange rates.
    /// </summary>
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Handles are ASCII only, so upper() agrees with ToUpperInvariant() used for lookups.
        modelBuilder.Entity<User>()
            .Property(u => u.NormalizedUserName)
            .HasComputedColumnSql("upper(\"UserName\")", stored: true);

        modelBuilder.Entity<UserPicture>()
            .HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserPicture>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Users who own or belong to arrangements cannot be deleted. Deleting an arrangement removes everything in
        // it with one statement.
        modelBuilder.Entity<Arrangement>()
            .HasOne(a => a.Owner)
            .WithMany()
            .HasForeignKey(a => a.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ArrangementMember>()
            .HasOne(m => m.Arrangement)
            .WithMany(a => a.Members)
            .HasForeignKey(m => m.ArrangementId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ArrangementMember>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TimeOption>()
            .HasOne(o => o.Arrangement)
            .WithMany(a => a.TimeOptions)
            .HasForeignKey(o => o.ArrangementId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TimeOption>()
            .HasOne(o => o.CreatedBy)
            .WithMany()
            .HasForeignKey(o => o.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The grain refuses to remove the final option; the database only keeps the reference valid.
        modelBuilder.Entity<Arrangement>()
            .HasOne<TimeOption>()
            .WithMany()
            .HasForeignKey(a => a.FinalTimeOptionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Vote>()
            .HasOne(v => v.TimeOption)
            .WithMany(o => o.Votes)
            .HasForeignKey(v => v.TimeOptionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Vote>()
            .HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Readable when looking at the database.
        modelBuilder.Entity<Vote>()
            .Property(v => v.Answer)
            .HasConversion<string>()
            .HasMaxLength(8);

        modelBuilder.Entity<Expense>()
            .HasOne(e => e.Arrangement)
            .WithMany(a => a.Expenses)
            .HasForeignKey(e => e.ArrangementId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Expense>()
            .HasOne(e => e.PaidBy)
            .WithMany()
            .HasForeignKey(e => e.PaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExpenseParticipation>()
            .HasOne(p => p.Expense)
            .WithMany(e => e.Participations)
            .HasForeignKey(p => p.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExpenseParticipation>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExchangeRate>()
            .HasOne(r => r.Arrangement)
            .WithMany(a => a.ExchangeRates)
            .HasForeignKey(r => r.ArrangementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
