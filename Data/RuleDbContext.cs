using Microsoft.EntityFrameworkCore;
using DemoRuleEngine.Models;

namespace DemoRuleEngine.Data;

public class RuleDbContext : DbContext
{
    public RuleDbContext(DbContextOptions<RuleDbContext> options) : base(options)
    {
    }

    public DbSet<RuleAuditLog> RuleAuditLogs { get; set; }
    public DbSet<WorkflowEntity> Workflows { get; set; }
    public DbSet<RuleEntity> Rules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Audit Log table
        modelBuilder.Entity<RuleAuditLog>(entity =>
        {
            entity.ToTable("RuleAuditLogs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EntityName);
            entity.HasIndex(e => e.ChangedDate);
        });

        modelBuilder.Entity<WorkflowEntity>(entity =>
        {
            entity.ToTable("Workflows");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WorkflowName).IsUnique();
        });

        modelBuilder.Entity<RuleEntity>(entity =>
        {
            entity.ToTable("WorkflowRules");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WorkflowId);

            entity.Property(e => e.Definition)
                  .HasConversion(new DemoRuleEngine.Converters.RuleJsonConverter())
                  .HasColumnType("nvarchar(max)");

            entity.HasOne(e => e.Workflow)
                  .WithMany(w => w.Rules)
                  .HasForeignKey(e => e.WorkflowId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
