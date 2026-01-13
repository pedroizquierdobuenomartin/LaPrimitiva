using Microsoft.EntityFrameworkCore;
using LaPrimitiva.Domain.Entities;

namespace LaPrimitiva.Infrastructure.Persistence
{
    public class PrimitivaDbContext : DbContext
    {
        public PrimitivaDbContext(DbContextOptions<PrimitivaDbContext> options) : base(options)
        {
        }

        public DbSet<Plan> Plans { get; set; } = null!;
        public DbSet<DrawRecord> DrawRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Plan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CostPerBet).HasPrecision(10, 2);
                entity.Property(e => e.JokerCostPerBet).HasPrecision(10, 2);
                
                // Requirement: Block overlaps usually, or define priority. 
                // We will handle overlap logic in the Service layer.
            });

            modelBuilder.Entity<DrawRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FixedPrize).HasPrecision(10, 2);
                entity.Property(e => e.AutoPrize).HasPrecision(10, 2);
                entity.Property(e => e.JokerFixedPrize).HasPrecision(10, 2);
                entity.Property(e => e.JokerAutoPrize).HasPrecision(10, 2);

                entity.Property(e => e.CosteFija).HasPrecision(10, 2);
                entity.Property(e => e.CosteAuto).HasPrecision(10, 2);
                entity.Property(e => e.CosteJokerFija).HasPrecision(10, 2);
                entity.Property(e => e.CosteJokerAuto).HasPrecision(10, 2);
                entity.Property(e => e.TotalCoste).HasPrecision(10, 2);
                entity.Property(e => e.TotalPremios).HasPrecision(10, 2);
                entity.Property(e => e.Neto).HasPrecision(10, 2);
                entity.Property(e => e.Acumulado).HasPrecision(12, 2);

                // Constraint: Unique (PlanId, DrawDate, DrawType)
                entity.HasIndex(e => new { e.PlanId, e.DrawDate, e.DrawType }).IsUnique();
                
                entity.HasOne(d => d.Plan)
                    .WithMany(p => p.Draws)
                    .HasForeignKey(d => d.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => e.DrawDate);
                entity.HasIndex(e => e.WeekNumber);
            });
        }
    }
}
