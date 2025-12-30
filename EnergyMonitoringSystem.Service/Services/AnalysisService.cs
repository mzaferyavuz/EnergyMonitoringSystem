using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Enums;
using EnergyMonitoringSystem.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Service.Services
{
    public class AnalysisService
    {
        private readonly AppDbContext _context;

        public AnalysisService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AnalysisResponseDto> GetAnalysisData(AnalysisRequestDto request)
        {
            var response = new AnalysisResponseDto();

            // 1. Sayaçları ve Parametreleri Getir
            var meters = await _context.Meters
                .Include(m => m.Purpose)
                .Where(m => request.MeterIds.Contains(m.Id))
                .AsNoTracking()
                .ToListAsync();

            var parameters = await _context.MeasurementParameters
                .Where(p => request.ParameterIds.Contains(p.Id))
                .AsNoTracking()
                .ToListAsync();

            // 2. Zaman Dilimlerini Oluştur (Eğer Interval seçildiyse)
            List<(DateTime Start, DateTime End, string Label)> timeBuckets = new();
            if (request.Interval.HasValue)
            {
                timeBuckets = CreateTimeBuckets(request.StartDate, request.EndDate, request.Interval.Value);
                response.Labels = timeBuckets.Select(t => t.Label).ToList();
            }

            // 3. Her Sayaç İçin Döngü
            foreach (var meter in meters)
            {
                var meterDto = new MeterAnalysisDto
                {
                    MeterId = meter.Id,
                    MeterName = meter.Name,
                    UsagePurpose = meter.Purpose?.Name ?? "Other" // Null ise "Other"
                };

                // 4. Her Parametre İçin Döngü
                foreach (var param in parameters)
                {
                    var paramResult = new ParameterAnalysisResultDto
                    {
                        ParameterId = param.Id,
                        ParameterName = param.Name,
                        Unit = param.Unit
                    };

                    // --- DAL 1: TÜKETİM VERİSİ (ActiveEnergy) ---
                    if (param.Key == "ActiveEnergy")
                    {
                        // "MeterConsumptions" tablosunu kullan (Hesaplanmış Veri)

                        // Önce Özet (Summary) Hesabı: Tüm aralığın toplamı
                        // (Hangi çözünürlükten çektiğimiz önemli değil, Hourly daha hızlıdır)
                        var totalSum = await _context.MeterConsumptions
                            .Where(c => c.MeterId == meter.Id &&
                                        c.PeriodType == "Hourly" &&
                                        c.Timestamp >= request.StartDate &&
                                        c.Timestamp <= request.EndDate)
                            .SumAsync(c => c.Consumption);

                        paramResult.SummaryValue = totalSum;

                        // Eğer Interval varsa Seri Verisi Doldur
                        if (request.Interval.HasValue)
                        {
                            // İstenen interval 15dk ise "15Min", diğerleri için "Hourly" tablosundan çekelim
                            string resolution = request.Interval == DashboardInterval.FifteenMinutes ? "15Min" : "Hourly";

                            var consumptionLogs = await _context.MeterConsumptions
                                .Where(c => c.MeterId == meter.Id &&
                                            c.PeriodType == resolution &&
                                            c.Timestamp >= request.StartDate &&
                                            c.Timestamp <= request.EndDate)
                                .Select(c => new { c.Timestamp, c.Consumption })
                                .AsNoTracking()
                                .ToListAsync();

                            foreach (var bucket in timeBuckets)
                            {
                                // O aralığa düşenlerin TOPLAMI (Consumption Logic)
                                var bucketSum = consumptionLogs
                                    .Where(c => c.Timestamp > bucket.Start && c.Timestamp <= bucket.End)
                                    .Sum(c => c.Consumption);

                                paramResult.SeriesData.Add(bucketSum);
                            }
                        }
                    }
                    // --- DAL 2: DİĞER PARAMETRELER (Voltaj, Akım vb.) ---
                    else
                    {
                        // "MeterHistories" tablosunu kullan (Ham Veri)

                        var historyQuery = _context.MeterHistories
                            .Where(h => h.MeterId == meter.Id &&
                                        h.MeasurementParameterId == param.Id &&
                                        h.Timestamp >= request.StartDate &&
                                        h.Timestamp <= request.EndDate);

                        // Önce Özet (Summary) Hesabı: Tüm aralığın ORTALAMASI
                        // (Veri yoksa hata vermemesi için DefaultIfEmpty)
                        var avgValue = await historyQuery.Select(h => (decimal?)h.Value).AverageAsync();
                        paramResult.SummaryValue = avgValue ?? 0;

                        // Eğer Interval varsa Seri Verisi Doldur
                        if (request.Interval.HasValue)
                        {
                            // Performans: Tüm datayı çekip memory'de gruplamak yerine
                            // Mümkünse sadece ihtiyacımız olan datayı çekmeliyiz.
                            // Ancak LINQ ile "Her saatin ilk kaydını getir" sorgusu komplekstir.
                            // En temizi: Veriyi çekip memory'de bucket'a en yakın olanı (Snapshot) bulmak.

                            var logs = await historyQuery
                                .Select(h => new { h.Timestamp, h.Value })
                                .OrderBy(h => h.Timestamp)
                                .AsNoTracking()
                                .ToListAsync();

                            foreach (var bucket in timeBuckets)
                            {
                                // KURAL: "O saat başının verisi dönülecek" (Snapshot)
                                // Bucket.Start (Örn: 14:00) zamanına en yakın veya eşit olan ilk kaydı alıyoruz.
                                // Ya da o bucket aralığındaki İLK kaydı alıyoruz.

                                var snapshot = logs
                                    .Where(l => l.Timestamp >= bucket.Start && l.Timestamp < bucket.End)
                                    .FirstOrDefault(); // İlk kayıt = Saat başı kaydı

                                paramResult.SeriesData.Add(snapshot != null ? (decimal)snapshot.Value : null);
                            }
                        }
                    }

                    meterDto.Parameters.Add(paramResult);
                }

                response.Meters.Add(meterDto);
            }

            return response;
        }

        // DashboardService'deki metotla aynı (Helper yapılabilir demiştik)
        private List<(DateTime Start, DateTime End, string Label)> CreateTimeBuckets(DateTime start, DateTime end, DashboardInterval interval)
        {
            var buckets = new List<(DateTime, DateTime, string)>();
            var current = start;
            while (current < end)
            {
                DateTime next;
                string label;
                switch (interval)
                {
                    case DashboardInterval.FifteenMinutes: next = current.AddMinutes(15); label = current.ToString("HH:mm"); break;
                    case DashboardInterval.Hourly: next = current.AddHours(1); label = current.ToString("dd.MM HH:mm"); break;
                    case DashboardInterval.Daily: next = current.AddDays(1); label = current.ToString("dd.MM"); break;
                    case DashboardInterval.Weekly: next = current.AddDays(7); label = $"{current:dd.MM}"; break;
                    case DashboardInterval.Monthly: next = current.AddMonths(1); label = current.ToString("MMM yyyy"); break;
                    default: next = current.AddDays(1); label = current.ToString("dd.MM"); break;
                }
                if (next > end) next = end;
                if (current < next) buckets.Add((current, next, label));
                current = next;
            }
            return buckets;
        }
    }
}
