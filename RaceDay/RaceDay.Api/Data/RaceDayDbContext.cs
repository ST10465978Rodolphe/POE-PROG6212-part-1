using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<EventRoute> Routes => Set<EventRoute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- User ----------
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("User", t =>
                t.HasCheckConstraint("CK_User_Role", "[Role] IN ('Organiser', 'Participant')"));

            entity.HasKey(u => u.UserId);

            entity.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(u => u.LastName).IsRequired().HasMaxLength(50);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(100);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(u => u.Role).IsRequired().HasMaxLength(20);
            entity.Property(u => u.Phone).HasMaxLength(20);

            entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_User_Email");
        });

        // ---------- Event ----------
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Event", t =>
            {
                t.HasCheckConstraint("CK_Event_Distance", "[Distance] > 0");
                t.HasCheckConstraint("CK_Event_Type", "[EventType] IN ('Running', 'Walking', 'Cycling')");
            });

            entity.HasKey(e => e.EventId);

            entity.Property(e => e.EventName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.Location).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Distance).HasPrecision(6, 2);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(30);

            // One Organiser -> many Events
            entity.HasOne(e => e.Organiser)
                  .WithMany(u => u.Events)
                  .HasForeignKey(e => e.OrganiserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Category ----------
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category", t =>
            {
                t.HasCheckConstraint("CK_Category_Distance", "[Distance] > 0");
                t.HasCheckConstraint("CK_Category_EntryFee", "[EntryFee] >= 0");
            });

            entity.HasKey(c => c.CategoryId);

            entity.Property(c => c.CategoryName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(255);
            entity.Property(c => c.Distance).HasPrecision(6, 2);
            entity.Property(c => c.EntryFee).HasPrecision(10, 2).HasDefaultValue(0m);

            // One Event -> many Categories
            entity.HasOne(c => c.Event)
                  .WithMany(e => e.Categories)
                  .HasForeignKey(c => c.EventId)
                  .OnDelete(DeleteBehavior.Restrict);

            // A category name must be unique inside one event
            entity.HasIndex(c => new { c.EventId, c.CategoryName })
                  .IsUnique()
                  .HasDatabaseName("UQ_Category_Event_Name");
        });

        // ---------- Enrolment ----------
        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.ToTable("Enrolment", t =>
                t.HasCheckConstraint("CK_Enrolment_Status", "[Status] IN ('Confirmed', 'Cancelled')"));

            entity.HasKey(e => e.EnrolmentId);

            entity.Property(e => e.EnrolmentDate).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Confirmed");

            // One Participant -> many Enrolments
            entity.HasOne(e => e.Participant)
                  .WithMany(u => u.Enrolments)
                  .HasForeignKey(e => e.ParticipantId)
                  .OnDelete(DeleteBehavior.Restrict);

            // One Category -> many Enrolments
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Enrolments)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            // A participant can only enter the same category once
            entity.HasIndex(e => new { e.ParticipantId, e.CategoryId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Participant_Category");
        });

        // ---------- Result ----------
        modelBuilder.Entity<Result>(entity =>
        {
            entity.ToTable("Result", t =>
                t.HasCheckConstraint("CK_Result_Position", "[FinishingPosition] > 0"));

            entity.HasKey(r => r.ResultId);

            entity.Property(r => r.ResultDate).HasDefaultValueSql("GETDATE()");

            // One Enrolment -> zero or one Result
            entity.HasOne(r => r.Enrolment)
                  .WithOne(e => e.Result)
                  .HasForeignKey<Result>(r => r.EnrolmentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- EventRoute (table "Route") ----------
        modelBuilder.Entity<EventRoute>(entity =>
        {
            entity.ToTable("Route", t =>
                t.HasCheckConstraint("CK_Route_Distance", "[Distance] > 0"));

            entity.HasKey(r => r.RouteId);

            entity.Property(r => r.RouteName).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Description).HasMaxLength(255);
            entity.Property(r => r.StartLocation).IsRequired().HasMaxLength(150);
            entity.Property(r => r.FinishLocation).IsRequired().HasMaxLength(150);
            entity.Property(r => r.Distance).HasPrecision(6, 2);

            // One Event -> zero or one Route
            entity.HasOne(r => r.Event)
                  .WithOne(e => e.Route)
                  .HasForeignKey<EventRoute>(r => r.EventId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}