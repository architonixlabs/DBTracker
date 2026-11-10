using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Pure comparison of a declared identity against one observation (D06).</summary>
public record DriftDetectionResult(DriftStatus Status, IReadOnlyList<string> DifferingFields);

/// <summary>
/// Compares a declared VersionIdentity (manually entered/attested) against one VersionObservation
/// (manual import today; a FutureCollector result eventually). A failed observation (permission
/// denied/unreachable) is always Unknown — it must never be reported as drift, since that would
/// imply the asset changed when all that actually happened is the check couldn't run.
/// </summary>
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
