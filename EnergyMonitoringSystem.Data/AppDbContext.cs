using EnergyMonitoringSystem.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EnergyMonitoringSystem.Core.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tablolarımız
        public DbSet<Meter> Meters { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<ModbusDevice> ModbusDevices { get; set; }
        public DbSet<ModbusRegister> ModbusRegisters { get; set; }
        public DbSet<MeterHistory> MeterHistories { get; set; }
        public DbSet<UsagePurpose> UsagePurposes { get; set; }
        public DbSet<Tariff> Tariffs { get; set; }
        public DbSet<CarbonFactor> CarbonFactors { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ModbusRegister>()
                .HasOne(r => r.ModbusDevice)
                .WithMany() // Bir cihazın birçok register ayarı olabilir
                .HasForeignKey(r => r.ModbusDeviceId);

            // 1. Sayaç Hiyerarşisi (Self-Referencing) - Madde 4
            modelBuilder.Entity<Meter>()
                .HasOne(m => m.ParentMeter)
                .WithMany(m => m.ChildMeters)
                .HasForeignKey(m => m.ParentMeterId)
                .OnDelete(DeleteBehavior.Restrict);

            // 2. Tenant - Meter İlişkisi - Madde 7
            modelBuilder.Entity<Meter>()
                .HasOne(m => m.Tenant)
                .WithMany(t => t.Meters)
                .HasForeignKey(m => m.TenantId)
                .OnDelete(DeleteBehavior.SetNull); // Tenant silinirse sayaç silinmesin

            // 3. ModbusDevice - Meter İlişkisi
            modelBuilder.Entity<Meter>()
                .HasOne(m => m.ModbusDevice)
                .WithMany()
                .HasForeignKey(m => m.ModbusDeviceId);

            // 4. Hassas Veri Tipleri (Para birimleri için decimal ayarı) - Madde 9
            modelBuilder.Entity<Tariff>()
                .Property(t => t.UnitPrice)
                .HasPrecision(18, 4);

            // 5. Indexing (Performans için 15 dakikalık verilerde tarih indexi) - Madde 6
            modelBuilder.Entity<MeterHistory>()
                .HasIndex(h => h.Timestamp);

            // Rolleri Tanımlayalım (Madde 11)
            string adminRoleId = Guid.NewGuid().ToString();
            string standardRoleId = Guid.NewGuid().ToString();
            string viewOnlyRoleId = Guid.NewGuid().ToString();
            string externalRoleId = Guid.NewGuid().ToString();

            modelBuilder.Entity<IdentityRole>().HasData(
new IdentityRole { Id = adminRoleId, Name = RoleConstants.Admin, NormalizedName = RoleConstants.Admin.ToUpper() },
    new IdentityRole { Id = standardRoleId, Name = RoleConstants.Standard, NormalizedName = RoleConstants.Standard.ToUpper() },
    new IdentityRole { Id = viewOnlyRoleId, Name = RoleConstants.ViewOnly, NormalizedName = RoleConstants.ViewOnly.ToUpper() },
    new IdentityRole { Id = externalRoleId, Name = RoleConstants.External, NormalizedName = RoleConstants.External.ToUpper() }
            );
        }
    }
}
