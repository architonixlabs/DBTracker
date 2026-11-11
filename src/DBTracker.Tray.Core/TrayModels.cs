using System.Runtime.Serialization;

namespace DBTracker.Tray.Core
{
    // Shapes of GET /healthz and GET /api/tray/summary. Dates stay strings and are parsed on use, so
    // the serializer never needs a date format.
    [DataContract]
    public sealed class HealthReport
    {
        [DataMember(Name = "status")] public string Status { get; set; }
        [DataMember(Name = "installed")] public bool Installed { get; set; }
        [DataMember(Name = "database")] public string Database { get; set; }
        [DataMember(Name = "version")] public string Version { get; set; }
    }

    [DataContract]
    public sealed class TraySummary
    {
        [DataMember(Name = "generatedAtUtc")] public string GeneratedAtUtc { get; set; }
        [DataMember(Name = "user")] public string User { get; set; }
        [DataMember(Name = "failed")] public int Failed { get; set; }
        [DataMember(Name = "overdue")] public int Overdue { get; set; }
        [DataMember(Name = "neverBackedUp")] public int NeverBackedUp { get; set; }
        [DataMember(Name = "atRisk")] public AtRiskInstance[] AtRisk { get; set; }
        [DataMember(Name = "dashboardUrl")] public string DashboardUrl { get; set; }
    }

    [DataContract]
    public sealed class AtRiskInstance
    {
        [DataMember(Name = "id")] public long Id { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "health")] public string Health { get; set; }
        [DataMember(Name = "environment")] public string Environment { get; set; }
        [DataMember(Name = "lastSuccessUtc")] public string LastSuccessUtc { get; set; }
    }

    // What one /healthz request produced, before interpretation.
    public sealed class HealthProbe
    {
        public int? HttpStatus { get; set; }
        public string Body { get; set; }
        public string Error { get; set; }

        public static HealthProbe Failed(string error) => new HealthProbe { Error = error };
        public static HealthProbe Response(int status, string body) => new HealthProbe { HttpStatus = status, Body = body };
    }

    public enum StatusLight
    {
        Unknown,
        Green,
        Amber,
        Red,
    }

    public sealed class HealthState
    {
        public HealthState(StatusLight light, string text, bool setupRequired)
        {
            Light = light;
            Text = text;
            SetupRequired = setupRequired;
        }

        public StatusLight Light { get; }
        public string Text { get; }
        public bool SetupRequired { get; }
    }
}
