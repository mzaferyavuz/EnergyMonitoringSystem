using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using EnergyMonitoringSystem.Service.Emission;
using Microsoft.EntityFrameworkCore;
using Xunit; // xUnit kütüphanesi

namespace EnergyMonitoringSystem.Tests
{
    public class EmissionServiceTests
    {
        // Test senaryosu: "Normal bir kullanımda emisyon doğru hesaplanmalı"
        [Fact]
        public async Task CalculateTotalEmission_ShouldReturnCorrectValue()
        {
            // 1. ARRANGE (Hazırlık) -> Ortamı kuruyoruz

            // Gerçek SQL yerine RAM'de çalışan geçici bir veritabanı oluşturuyoruz
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "EmissionTestDb") // Sanal DB adı
                .Options;

            using (var context = new AppDbContext(options))
            {
                // Test verilerini ekle (Parametre, Sayaç, Karbon Faktörü)

                // a) Aktif Enerji Parametresi
                var param = new MeasurementParameter { Id = 1, Key = "ActiveEnergy", Name = "Aktif Enerji", Unit = "kWh" };
                context.MeasurementParameters.Add(param);

                // b) Karbon Faktörü (1 kWh = 0.5 kgCO2 olsun ki hesaplaması kolay olsun)
                context.CarbonFactors.Add(new CarbonFactor { Factor = 0.5, ValidFrom = DateTime.MinValue });

                // c) Sayaç Geçmişi (History)
                // İlk Endeks: 100 kWh
                context.MeterHistories.Add(new MeterHistory
                {
                    MeterId = 1,
                    MeasurementParameterId = 1,
                    Value = 100,
                    Timestamp = DateTime.Parse("2023-01-01 10:00")
                });

                // Son Endeks: 150 kWh (Tüketim = 50 kWh olmalı)
                context.MeterHistories.Add(new MeterHistory
                {
                    MeterId = 1,
                    MeasurementParameterId = 1,
                    Value = 150,
                    Timestamp = DateTime.Parse("2023-01-01 12:00")
                });

                await context.SaveChangesAsync();
            }

            // 2. ACT (Eylem) -> Metodu çalıştırıyoruz
            using (var context = new AppDbContext(options))
            {
                var service = new EmissionService(context);

                // 10:00 ile 12:00 arasını sorguluyoruz
                var result = await service.CalculateTotalEmission(1,
                    DateTime.Parse("2023-01-01 10:00"),
                    DateTime.Parse("2023-01-01 12:00"));

                // 3. ASSERT (Doğrulama) -> Sonuç beklediğimiz gibi mi?

                // Beklenen: (150 - 100) * 0.5 = 25.0
                Assert.Equal(25.0, result);
            }
        }
    }
}