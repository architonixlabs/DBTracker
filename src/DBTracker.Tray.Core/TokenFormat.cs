namespace DBTracker.Tray.Core
{
    public static class TokenFormat
    {
        public const string Prefix = "dbt_";

        // Strips the whitespace a paste usually brings along.
        public static string Normalize(string token) => (token ?? "").Trim();

        public static bool LooksValid(string token)
        {
            var t = Normalize(token);
            if (!t.StartsWith(Prefix) || t.Length < Prefix.Length + 16 || t.Length > 512)
            {
                return false;
            }
            foreach (var c in t)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
