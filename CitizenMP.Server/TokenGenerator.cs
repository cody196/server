using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server
{
    public class TokenGenerator
    {
        static RandomNumberGenerator ms_rng;

        static TokenGenerator()
        {
            ms_rng = RandomNumberGenerator.Create();
        }

        public static string GenerateToken()
        {
            var bytes = new byte[20];
            ms_rng.GetBytes(bytes);

            return bytes.Aggregate("", (a, b) => a + b.ToString("x2"));
        }
    }
}
