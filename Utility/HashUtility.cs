using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TeeKoASPCore.Utility
{
    public static class HashUtility
    {
        public static string GetShortHash(string input)
        {
            return String.Format("{0:X}", input.GetHashCode());
        }


    }
}
