using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Enums
{
    public enum DashboardScope
    {
        All = 1,            // Tüm Sayaçlar (Tenant'ı olsun olmasın)
        Tenant = 2,         // Belirli Tenant(lar)
        NoTenant = 3        // Tenant'ı Olmayanlar (Yönetim sayaçları vb.)
    }

    public enum DashboardInterval
    {
        FifteenMinutes = 1,
        Hourly = 2,
        Daily = 3,
        Weekly = 4,
        Monthly = 5,
        Yearly = 6
    }
}
