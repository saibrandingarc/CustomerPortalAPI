using System.Xml;
using Microsoft.EntityFrameworkCore;
using CustomerPortalAPI.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Define DbSets for your entities
    public DbSet<Staff> Staff { get; set; }
    public DbSet<MainMenu> MainMenu { get; set; }
    public DbSet<Settings> Settings { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<LoginActivity> LoginActivities { get; set; }
    public DbSet<DeliverableFileApproval> DeliverableFileApprovals { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<LoginActivity>().ToTable("LoginActivities");
        modelBuilder.Entity<LoginActivity>()
            .HasIndex(x => new { x.Email, x.CreatedAtUtc });
        modelBuilder.Entity<DeliverableFileApproval>().ToTable("DeliverableFileApprovals");
        modelBuilder.Entity<DeliverableFileApproval>()
            .HasIndex(x => new { x.DeliverableId, x.FileId });
        // Configure entity properties and relationships here
    }
}
