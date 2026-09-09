using EventReservationApp.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventReservationApp.Data;

/// <summary>
/// EF Core database context. Inherits from IdentityDbContext so that
/// Identity's Users/Roles/Claims/Logins/Tokens tables are included
/// automatically alongside the application's own tables.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Event> Events { get; set; } = null!;

    public DbSet<EventReservation> EventReservations { get; set; } = null!;
    public DbSet<ChatConversation> ChatConversations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---------------------------------------------------------------
        // Event
        // ---------------------------------------------------------------
        builder.Entity<Event>(entity =>
        {
            entity.ToTable("Events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
        });

        // ---------------------------------------------------------------
        // EventReservation: explicit relationship configuration.
        // One User -> many EventReservations (restrict delete: a user with
        // reservations cannot be hard-deleted without handling those first).
        // One Event -> many EventReservations (cascade delete: deleting an
        // event removes its reservations).
        // ---------------------------------------------------------------
        builder.Entity<EventReservation>(entity =>
        {
            entity.ToTable("EventReservations");
            entity.HasKey(r => r.Id);

            entity.HasOne(r => r.User)
                .WithMany(u => u.EventReservations)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Event)
                .WithMany(e => e.EventReservations)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // A user can only reserve a given event once.
            entity.HasIndex(r => new { r.UserId, r.EventId }).IsUnique();
        });
        builder.Entity<ChatConversation>(entity =>
        {
            entity.ToTable("ChatConversations");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(x => x.FoundryConversationId)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.UserId)
                .IsUnique();
        });
    }
}
