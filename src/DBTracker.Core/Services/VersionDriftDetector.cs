using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record DriftDetectionResult(DriftStatus Status, IReadOnlyList<string> DifferingFields);

/// <summary>Compares a declared VersionIdentity with one observation. A failed observation is always Unknown, never drift.</summary>
public static class VersionDriftDetector
{
    public static DriftDetectionResult Detect(VersionIdentity declared, VersionObservation observation, DatabaseEngine engine)
    {
        if (observation.Outcome != ObservationOutcome.Success)
        {
            return new DriftDetectionResult(DriftStatus.Unknown, []);
        }

        var differing = new List<string>();

        if (!string.Equals(declared.RawVersionText.Trim(), observation.RawVersionText.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            differing.Add("RawVersionText");

            var observedParsed = VersionIdentityParser.Parse(engine, observation.RawVersionText);
            if (observedParsed.Status == VersionResolutionStatus.Resolved && declared.ResolutionStatus == VersionResolutionStatus.Resolved)
            {
                if (observedParsed.Major != declared.Major)
                {
                    differing.Add("Major");
                }
                if (observedParsed.Minor != declared.Minor)
                {
                    differing.Add("Minor");
                }
                if (!string.Equals(observedParsed.Build, declared.Build, StringComparison.OrdinalIgnoreCase))
                {
                    differing.Add("Build");
                }
            }
        }

        return differing.Count == 0
            ? new DriftDetectionResult(DriftStatus.Match, [])
            : new DriftDetectionResult(DriftStatus.Drift, differing);
    }
}
