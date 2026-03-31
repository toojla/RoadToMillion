using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace RoadToMillion.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AccountGroup> AccountGroups => Set<AccountGroup>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BalanceSnapshot> BalanceSnapshots => Set<BalanceSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Important for Identity!

        // AccountGroup
        modelBuilder.Entity<AccountGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();

            entity.HasMany(e => e.Accounts)
                  .WithOne(a => a.AccountGroup)
                  .HasForeignKey(a => a.AccountGroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Account
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => new { e.AccountGroupId, e.Name }).IsUnique();

            entity.HasMany(e => e.BalanceSnapshots)
                  .WithOne(b => b.Account)
                  .HasForeignKey(b => b.AccountId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // BalanceSnapshot
        modelBuilder.Entity<BalanceSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).IsRequired().HasPrecision(18, 2);
        });
    }
}