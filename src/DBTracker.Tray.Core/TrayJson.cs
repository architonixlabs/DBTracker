using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace DBTracker.Tray.Core
{
    public static class TrayJson
    {
        public static HealthReport ParseHealth(string json) => Parse<HealthReport>(json);

        public static TraySummary ParseSummary(string json)
        {
            var summary = Parse<TraySummary>(json);
            if (summary != null && summary.AtRisk == null)
            {
                summary.AtRisk = new AtRiskInstance[0];
            }
            return summary;
        }

        // Returns null instead of throwing: a malformed reply is treated like no reply.
        private static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(stream) as T;
                }
            }
            catch (Exception ex) when (ex is SerializationException || ex is InvalidCastException || ex is FormatException)
            {
                return null;
            }
        }
    }
}
