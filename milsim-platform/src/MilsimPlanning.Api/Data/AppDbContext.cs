using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Data.Entities;

namespace MilsimPlanning.Api.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventMembership> EventMemberships => Set<EventMembership>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // MUST be first — sets up Identity tables

        // UserProfile 1:1 with AppUser
        builder.Entity<UserProfile>()
            .HasOne(p => p.User)
            .WithOne(u => u.Profile)
            .HasForeignKey<UserProfile>(p => p.UserId);

        // EventMembership composite unique index (one membership per user per event)
        builder.Entity<EventMembership>()
            .HasIndex(m => new { m.UserId, m.EventId })
            .IsUnique();

        // EventMembership FK relations
        builder.Entity<EventMembership>()
            .HasOne(m => m.User)
            .WithMany(u => u.EventMemberships)
            .HasForeignKey(m => m.UserId);

        builder.Entity<EventMembership>()
            .HasOne(m => m.Event)
            .WithMany(e => e.Memberships)
            .HasForeignKey(m => m.EventId);

        // MagicLinkToken index on (UserId, TokenHash) for fast lookup
        builder.Entity<MagicLinkToken>()
            .HasIndex(t => new { t.UserId, t.TokenHash });

        builder.Entity<MagicLinkToken>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId);

        // Event default status
        builder.Entity<Event>()
            .Property(e => e.Status)
            .HasDefaultValue("draft");
    }
}
