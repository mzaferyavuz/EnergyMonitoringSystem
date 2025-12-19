using EnergyMonitoringSystem.Core.Entities;
using Microsoft.AspNetCore.Identity;
using EnergyMonitoringSystem.Core.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAdminUser(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Admin rolü var mı kontrol et
            if (!await roleManager.RoleExistsAsync(RoleConstants.Admin))
            {
                await roleManager.CreateAsync(new IdentityRole(RoleConstants.Admin));
            }
            // Diğer rolleri de ekleyebilirsin
            if (!await roleManager.RoleExistsAsync(RoleConstants.Standard)) await roleManager.CreateAsync(new IdentityRole(RoleConstants.Standard));
            if (!await roleManager.RoleExistsAsync(RoleConstants.External)) await roleManager.CreateAsync(new IdentityRole(RoleConstants.External));

            // Admin kullanıcısı var mı?
            if (await userManager.FindByNameAsync("admin") == null)
            {
                var user = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "admin@energy.com",
                    TenantId = null // Admin'in tenant'ı olmaz
                };

                var result = await userManager.CreateAsync(user, "Admin123!"); // Güçlü şifre
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, RoleConstants.Admin);
                }
            }
        }
    }
}
