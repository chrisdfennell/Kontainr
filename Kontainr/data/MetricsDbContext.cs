using Microsoft.EntityFrameworkCore;

namespace Kontainr.Data;

public class MetricsDbContext : DbContext
{
    public DbSet<ContainerMetric> ContainerMetrics => Set<ContainerMetric>();

    public MetricsDbContext(DbContextOptions<MetricsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ContainerMetric>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.HostId, m.ContainerId, m.Timestamp });
            e.HasIndex(m => m.Timestamp);
        });
    }
}
