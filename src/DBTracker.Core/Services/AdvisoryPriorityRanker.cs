using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record RankedAdvisoryAssessment(AdvisoryInstanceAssessment Assessment, int Score, string Rationale);

/// <summary>Orders advisory assessments by severity, service criticality, exposure and applicability together, not by
/// severity alone. Unknown severity and Unknown criticality are weighted like High, so missing facts never sink an item to
/// the bottom. Not applicable items always sort last. Ties go to the earliest remediation deadline.</summary>
public static class AdvisoryPriorityRanker
{
    public static IReadOnlyList<RankedAdvisoryAssessment> Rank(IEnumerable<AdvisoryInstanceAssessment> assessments, DatabaseInstance instance) =>
        Rank(assessments.Select(a => (a, instance)));

    public static IReadOnlyList<RankedAdvisoryAssessment> Rank(IEnumerable<(AdvisoryInstanceAssessment Assessment, DatabaseInstance Instance)> items) =>
        items
            .Select(x => Score(x.Assessment, x.Instance))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Assessment.RemediationDeadlineUtc ?? DateTime.MaxValue)
            .ThenByDescending(r => r.Assessment.AdvisoryRecord?.PublishedAtUtc ?? DateTime.MinValue)
            .ToList();

    public static RankedAdvisoryAssessment Score(AdvisoryInstanceAssessment assessment, DatabaseInstance instance)
    {
        var severity = assessment.AdvisoryRecord?.Severity ?? AdvisorySeverity.Unknown;
        var severityWeight = severity switch
        {
            AdvisorySeverity.Critical => 4,
            AdvisorySeverity.High => 3,
            AdvisorySeverity.Medium => 2,
            AdvisorySeverity.Low => 1,
            _ => 3
        };
        var criticalityWeight = instance.Criticality switch
        {
            InstanceCriticality.Critical => 4,
            InstanceCriticality.High => 3,
            InstanceCriticality.Medium => 2,
            InstanceCriticality.Low => 1,
            _ => 3
        };
        var exposureWeight = instance.Environment is EnvironmentTier.Production or EnvironmentTier.DisasterRecovery ? 2 : 1;
        var applicabilityWeight = assessment.Classification switch
        {
            AdvisoryClassification.Confirmed => 3,
            AdvisoryClassification.Potential => 2,
            AdvisoryClassification.Unknown => 2,
            _ => 0
        };

        var score = severityWeight * criticalityWeight * exposureWeight * applicabilityWeight;
        var rationale = $"{severity} severity, {instance.Criticality} criticality, {instance.Environment}, {assessment.Classification}";
        return new RankedAdvisoryAssessment(assessment, score, rationale);
    }
}
