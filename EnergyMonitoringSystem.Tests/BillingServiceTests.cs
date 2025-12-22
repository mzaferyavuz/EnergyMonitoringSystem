using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using EnergyMonitoringSystem.Service.Billing; // Billing namespace'i
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnergyMonitoringSystem.Tests
{
    public class BillingServiceTests
    {
        [Fact]
        public async Task CalculateBill_ShouldReturnCorrectAmount()
        {
            // 1. ARRANGE (Hazırlık)
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "BillingTestDb_" + Guid.NewGuid()) // Her test için benzersiz DB adı
                .Options;

            using (var context = new AppDbContext(options))
            {
                // a) Parametre
                var param = new MeasurementParameter { Id = 1, Key = "ActiveEnergy", Name = "Aktif Enerji", Unit = "kWh" };
                context.MeasurementParameters.Add(param);

                // b) Tarife (Birim Fiyat: 2.50 TL olsun)
                context.Tariffs.Add(new Tariff
                {
                    UnitPrice = 2.50m, // 'm' harfi decimal olduğunu belirtir
                    ValidFrom = DateTime.MinValue,
                    TenantId = null // Genel tarife
                });

                // c) Sayaç
                var meter = new Meter { Id = 1, Name = "Test Sayacı", TenantId = null };
                context.Meters.Add(meter);

                // d) Geçmiş Veri (Tüketim: 200 - 100 = 100 kWh)
                context.MeterHistories.Add(new MeterHistory
                {
                    MeterId = 1,
                    MeasurementParameterId = 1,
                    Value = 100,
                    Timestamp = DateTime.Parse("2023-01-01 09:00")
                });

                context.MeterHistories.Add(new MeterHistory
                {
                    MeterId = 1,
                    MeasurementParameterId = 1,
                    Value = 200,
                    Timestamp = DateTime.Parse("2023-01-01 10:00")
                });

                await context.SaveChangesAsync();
            }

            // 2. ACT (Eylem)
            using (var context = new AppDbContext(options))
            {
                var service = new BillingService(context);

                var result = await service.CalculateBill(1,
                    DateTime.Parse("2023-01-01 09:00"),
                    DateTime.Parse("2023-01-01 10:00"));

                // 3. ASSERT (Doğrulama)
                // Beklenen: (200 - 100) * 2.50 = 250.00 TL
                Assert.Equal(250.00m, result);
            }
        }
    }
}