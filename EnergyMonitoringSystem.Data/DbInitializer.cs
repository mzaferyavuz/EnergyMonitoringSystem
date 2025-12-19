using EnergyMonitoringSystem.Core.Entities;
using Microsoft.AspNetCore.Identity;
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
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            // Diğer rolleri de ekleyebilirsin
            if (!await roleManager.RoleExistsAsync("Standard")) await roleManager.CreateAsync(new IdentityRole("Standard"));
            if (!await roleManager.RoleExistsAsync("External")) await roleManager.CreateAsync(new IdentityRole("External"));

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
                    await userManager.AddToRoleAsync(user, "Admin");
                }
            }
        }
    }
}
