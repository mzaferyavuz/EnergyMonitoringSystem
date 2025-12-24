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

        public DbSet<MeasurementParameter> MeasurementParameters { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ModbusRegister>()
                .HasOne(r => r.Meter)
                .WithMany(m => m.ModbusRegisters) // Bir sayacın birçok register'ı olabilir (isterseniz Meter entity'sine ICollection<ModbusRegister> ekleyebilirsiniz)
                .HasForeignKey(r => r.MeterId)
                .OnDelete(DeleteBehavior.Cascade); // Sayaç silinirse registerları da silinsin

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
            //string adminRoleId = Guid.NewGuid().ToString();
            //string standardRoleId = Guid.NewGuid().ToString();
            //string viewOnlyRoleId = Guid.NewGuid().ToString();
            //string externalRoleId = Guid.NewGuid().ToString();

            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = "82e75004-5254-465e-b6b3-6c7081177656", // Sabit ID
                    Name = RoleConstants.Admin,
                    NormalizedName = RoleConstants.Admin.ToUpper()
                },
                new IdentityRole
                {
                    Id = "7d9b7113-a8f8-4035-99a7-a20dd404f6a3", // Sabit ID
                    Name = RoleConstants.Standard,
                    NormalizedName = RoleConstants.Standard.ToUpper()
                },
                 new IdentityRole
                 {
                     Id = "78a7570f-3ce5-48ba-9461-80283ed1d94d", // Sabit ID
                     Name = RoleConstants.ViewOnly,
                     NormalizedName = RoleConstants.ViewOnly.ToUpper()
                 },
                new IdentityRole
                {
                    Id = "1b95c86f-2361-4645-a75d-538d580f4215", // Sabit ID
                    Name = RoleConstants.External,
                    NormalizedName = RoleConstants.External.ToUpper()
                }
            );

            modelBuilder.Entity<MeasurementParameter>().HasData(
            new MeasurementParameter { Id = 1, Name = "Aktif Enerji (Tüketim)", Key = "ActiveEnergy", Unit = "kWh" },
            new MeasurementParameter { Id = 2, Name = "Aktif Güç", Key = "ActivePower", Unit = "kW" },
            new MeasurementParameter { Id = 3, Name = "L1 Gerilimi", Key = "Voltage_L1", Unit = "V" },
            new MeasurementParameter { Id = 4, Name = "L2 Gerilimi", Key = "Voltage_L2", Unit = "V" },
            new MeasurementParameter { Id = 5, Name = "L3 Gerilimi", Key = "Voltage_L3", Unit = "V" },
            new MeasurementParameter { Id = 6, Name = "Reaktif Güç (Endüktif)", Key = "ReactivePower_Ind", Unit = "kVAr" }, // İstediğin özellik
            new MeasurementParameter { Id = 7, Name = "L1 Akimi", Key = "Current_L1", Unit = "A" },
            new MeasurementParameter { Id = 8, Name = "L2 Akimi", Key = "Current_L2", Unit = "A" },
            new MeasurementParameter { Id = 9, Name = "L3 Akimi", Key = "Current_L3", Unit = "A" }
            );
        }
    }
}
