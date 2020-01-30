using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace TeeKoASPCore.Utility
{
    public static class ClaimsPrincipalExtensions
    {
        public static string ReadFirstClaim(this ClaimsPrincipal principal, string type) {
            return principal.Claims.Where(c => c.Type == type).Select(c => c.Value).First();
        }

        public static List<string> ReadAllClaims(this ClaimsPrincipal principal, string type)
        {
            return principal.Claims.Where(c => c.Type == type).Select(c => c.Value).ToList();
        }

        public static bool IsIdentical(this ClaimsPrincipal principal,  ClaimsPrincipal otherPrincipal)
        {
            return principal.ReadFirstClaim("id") == otherPrincipal.ReadFirstClaim("id");
        }

        public static string GetId(this ClaimsPrincipal principal)
        {
            return principal.ReadFirstClaim("id");
        }
    }
}
