using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EnergyMonitoringSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Service.Services
{
    public class ConsumptionFlowService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ConsumptionFlowService> _logger;

        public ConsumptionFlowService(AppDbContext context, ILogger<ConsumptionFlowService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ConsumptionFlowNodeDto>> GetConsumptionFlow(ConsumptionFlowRequestDto request)
        {
            try
            {
                // 1. ÖNCE TÜM SAYAÇLARI VE İLİŞKİLERİ ÇEK (Metadata)
                // Hiyerarşiyi kurabilmek için Parent-Child ilişkilerine ihtiyacımız var.
                // AsNoTracking ile performans kazanıyoruz.
                var allMeters = await _context.Meters
                    .Include(m => m.Purpose)
                    .Include(m => m.ChildMeters) // Child'ları da çekiyoruz
                    .AsNoTracking()
                    .ToListAsync();

                // 2. TÜKETİM VERİLERİNİ HESAPLA (Aggregation)
                // İlgili tarih aralığındaki tüm tüketimleri sayaç bazlı topluyoruz.
                // PeriodType = "Hourly" kullanmak en hızlısıdır.
                var consumptionDict = await _context.MeterConsumptions
                    .Where(c => c.Timestamp >= request.StartDate &&
                                c.Timestamp <= request.EndDate &&
                                (c.PeriodType == "Hourly" || c.PeriodType == "15Min")) // Saatlik yoksa 15dk'lıktan topla
                    .GroupBy(c => c.MeterId)
                    .Select(g => new
                    {
                        MeterId = g.Key,
                        Total = g.Sum(x => x.Consumption)
                    })
                    .ToDictionaryAsync(k => k.MeterId, v => v.Total);

                // 3. KÖK (ROOT) SAYAÇLARI BELİRLE
                // Hiyerarşinin en tepesindeki sayaçları bulmalıyız.
                IEnumerable<Meter> rootMeters;

                if (request.TenantIds != null && request.TenantIds.Any())
                {
                    // SENARYO A: Belirli Tenantlar Seçildi
                    // Mantık: Tenant'a ait olan sayaçlardan, Parent'ı bu tenant listesinde OLMAYANLAR köktür.
                    var tenantMeterIds = allMeters
                        .Where(m => m.TenantId.HasValue && request.TenantIds.Contains(m.TenantId.Value))
                        .Select(m => m.Id)
                        .ToHashSet();

                    rootMeters = allMeters.Where(m =>
                        tenantMeterIds.Contains(m.Id) &&
                        (m.ParentMeterId == null || !tenantMeterIds.Contains(m.ParentMeterId.Value))
                    );
                }
                else
                {
                    // SENARYO B: Tüm Saha (Tenant Ayrımı Yok)
                    // Mantık: Parent'ı null olanlar (Ana Panolar/Trafolar) köktür.
                    rootMeters = allMeters.Where(m => m.ParentMeterId == null);
                }

                // 4. AĞACI OLUŞTUR (Recursive)
                var resultTree = new List<ConsumptionFlowNodeDto>();

                foreach (var rootMeter in rootMeters)
                {
                    var node = BuildTreeNode(rootMeter, allMeters, consumptionDict, request.TenantIds);
                    resultTree.Add(node);
                }

                return resultTree;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumption Flow hesaplanırken hata oluştu.");
                throw; // Hatayı Controller yakalasın
            }
        }
        /// <summary>
        /// Recursive (Özyinelemeli) olarak ağaç düğümlerini ve çocuklarını oluşturur.
        /// </summary>
        private ConsumptionFlowNodeDto BuildTreeNode(
            Meter currentMeter,
            List<Meter> allMeters,
            Dictionary<int, decimal> consumptionData,
            List<int>? filterTenantIds)
        {
            // 1. Düğümü Oluştur
            var node = new ConsumptionFlowNodeDto
            {
                MeterId = currentMeter.Id,
                MeterName = currentMeter.Name,
                UsagePurpose = currentMeter.Purpose?.Name ?? "Tanımsız", // Gruplama için bu alan kullanılacak
                TotalConsumption = consumptionData.ContainsKey(currentMeter.Id) ? consumptionData[currentMeter.Id] : 0
            };

            // 2. Çocukları Bul (Recursive Çağrı)
            // Memory'deki listeden child'ları buluyoruz (Database'e gitmiyoruz)
            var children = allMeters.Where(m => m.ParentMeterId == currentMeter.Id);

            // Eğer Tenant filtresi varsa, sadece o tenant'a ait veya altındaki childları getirmeliyiz.
            // (İsteğe bağlı: Genelde akış diyagramında alt sayaçlar her durumda gösterilir, 
            // ama katı filtre isteniyorsa buraya tenant kontrolü eklenebilir. 
            // Şimdilik akışın kopmaması için fiziksel bağlı olanları getiriyoruz.)

            foreach (var child in children)
            {
                // Recursive olarak çocuğu ve onun çocuklarını ekle
                node.Children.Add(BuildTreeNode(child, allMeters, consumptionData, filterTenantIds));
            }

            // (Opsiyonel) Frontend için kolaylık: Çocukları Tüketime Göre Sırala
            node.Children = node.Children.OrderByDescending(c => c.TotalConsumption).ToList();

            return node;
        }
    }
}
