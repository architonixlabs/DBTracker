using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DBTracker.Tray
{
    // The access token, encrypted with DPAPI for the current Windows user only.
    internal static class TokenStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DBTracker.Tray.Token.v1");

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DBTracker", "Tray", "token.dat");

        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return null;
                }
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static void Save(string token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, cipher);
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
