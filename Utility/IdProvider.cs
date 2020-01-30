using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeeKoASPCore.Utility
{
    public class IdProvider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
        {
            return connection.User.Claims.Where(c => c.Type == "id").Select(c=>c.Value).First();
        }
    }
}
