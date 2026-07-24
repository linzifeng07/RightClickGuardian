using System;
using System.Security.Cryptography;
using System.Text;

namespace RightClickGuardian
{
    public static class HashUtil
    {
        public static string StableId(params string[] parts)
        {
            string joined = string.Join("|", parts ?? new string[0]);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(joined.ToLowerInvariant()));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < 12; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "item";
            StringBuilder builder = new StringBuilder();
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') builder.Append(c);
                else builder.Append('_');
            }
            string result = builder.ToString();
            return result.Length > 80 ? result.Substring(0, 80) : result;
        }
    }
}
