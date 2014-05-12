using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server
{
    static class Utils
    {
        public static Dictionary<string, string> ParseQueryString(String query)
        {
            Dictionary<String, String> queryDict = new Dictionary<string, string>();
            foreach (String token in query.TrimStart(new char[] { '?' }).Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = token.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                    queryDict[parts[0].Trim()] = parts[1].Trim();
                else
                    queryDict[parts[0].Trim()] = "";
            }
            return queryDict;
        }

        public static string GetFileSHA1String(string filename)
        {
            using (var baseStream = File.OpenRead(filename))
            {
                using (var stream = new BufferedStream(baseStream))
                {
                    using (var sha = SHA1.Create())
                    {
                        var hash = sha.ComputeHash(stream);
                        var sb = new StringBuilder(2 * hash.Length);
                        
                        foreach (var b in hash)
                        {
                            sb.AppendFormat("{0:X2}", b);
                        }

                        return sb.ToString();
                    }
                }
            }
        }
    }
}
