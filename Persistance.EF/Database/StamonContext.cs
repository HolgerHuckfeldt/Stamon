using Microsoft.EntityFrameworkCore;
using Persistance.EF.Service;
using Stamon.Types.Model;

namespace Persistance.EF.Database
{
    public class StamonContext : DbContext
    {
        public DbSet<StatisticItem> Items { get; set; }
        public DbSet<Slot> Slots { get; set; }

        public string DbPath { get; }

        public StamonContext()
        {
            //var folder = Environment.SpecialFolder.LocalApplicationData;
            //var path = Environment.GetFolderPath(folder);
            DbPath = EfStatisticService.DbPath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StatisticItem>(entity =>
            {
                entity.HasIndex(b => b.Time);
                entity.HasIndex(b => b.SlotId);
            });

            modelBuilder.Entity<Slot>(
                entity =>
                {
                    entity.HasIndex(b => b.Name);
                    entity.Property(e => e.Name).HasMaxLength(255);
                    entity.HasIndex(b => b.Scale);
                });
        }
    }

    public class StatisticItem
    {
        public int Id { get; set; }

        public long Count { get; set; }

        public string? Information { get; set; }

        public DateTime Time { get; set; }

        public int SlotId { get; set; }

        public Slot? Slot { get; set; }
    }

    public class Slot
    {
        public int Id { get; set; }

        public string Name { get; set; } = String.Empty;

        public long Duration { get; set; }

        public DateTime Created { get; set; }

        public Scale Scale { get; set; }

        public ICollection<StatisticItem> Items { get; set; } = new List<StatisticItem>();
    }
}
