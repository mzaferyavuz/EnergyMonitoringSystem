using EnergyMonitoringSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Service.Auth
{
    public interface ITokenService
    {
        (string Token, DateTime Expiration) CreateToken(ApplicationUser user, IList<string> roles);
    }
}
