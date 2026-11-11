using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

public record WorkloadComparisonResult(
    bool IsComparable,
    double? Change,
    double? ChangePercent,
    IReadOnlyList<string> Warnings,
    string Summary);

/// <summary>Compares a pre-change and a post-change measurement. It reports the change and anything that makes the comparison unreliable
/// (different measurement types, units or periods, wrong order, stale data); it doesn't decide whether the change is a regression, because
/// whether higher is better depends on the metric.</summary>
public static class WorkloadComparisonEvaluator
{
    public const int DefaultStaleAfterDays = 90;

    /// <summary>Problems that stop a comparison from being saved at all.</summary>
    public static IReadOnlyList<string> ValidateForSave(CapacityMeasurement? pre, CapacityMeasurement? post, int instanceId)
    {
        var errors = new List<string>();
        if (pre is null && post is null)
        {
            errors.Add("Choose at least one measurement to compare.");
        }
        if (pre is not null && pre.DatabaseInstanceId != instanceId)
        {
            errors.Add("The pre-change measurement belongs to a different instance.");
        }
        if (post is not null && post.DatabaseInstanceId != instanceId)
        {
            errors.Add("The post-change measurement belongs to a different instance.");
        }
        if (pre is not null && post is not null && pre.Id == post.Id && pre.Id != 0)
        {
            errors.Add("Pick two different measurements.");
        }
        return errors;
    }

    public static WorkloadComparisonResult Compare(CapacityMeasurement? pre, CapacityMeasurement? post, DateTime nowUtc, int staleAfterDays = DefaultStaleAfterDays)
    {
        var warnings = new List<string>();

        if (pre is null || post is null)
        {
            var missing = pre is null ? "pre-change" : "post-change";
            return new(false, null, null, [$"No {missing} measurement yet, so there is nothing to compare."],
                $"Waiting for the {missing} measurement.");
        }

        if (pre.MeasurementType != post.MeasurementType)
        {
            warnings.Add($"Different measurement types ({pre.MeasurementType} and {post.MeasurementType}).");
        }
        if (!string.Equals(pre.Unit.Trim(), post.Unit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Different units ({Show(pre.Unit)} and {Show(post.Unit)}).");
        }
        if (!string.Equals(pre.MeasurementPeriodDescription.Trim(), post.MeasurementPeriodDescription.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Different measurement periods ({Show(pre.MeasurementPeriodDescription)} and {Show(post.MeasurementPeriodDescription)}).");
        }
        if (post.MeasuredAtUtc <= pre.MeasuredAtUtc)
        {
            warnings.Add("The post-change measurement is not later than the pre-change one.");
        }
        if ((nowUtc - post.MeasuredAtUtc).TotalDays > staleAfterDays)
        {
            warnings.Add($"The post-change measurement is more than {staleAfterDays} days old.");
        }

        var comparable = pre.MeasurementType == post.MeasurementType &&
                         string.Equals(pre.Unit.Trim(), post.Unit.Trim(), StringComparison.OrdinalIgnoreCase) &&
                         post.MeasuredAtUtc > pre.MeasuredAtUtc;
        if (!comparable)
        {
            return new(false, null, null, warnings, "Not directly comparable. See the warnings.");
        }

        var change = post.Value - pre.Value;
        double? percent = pre.Value == 0 ? null : change / Math.Abs(pre.Value) * 100;
        var summary = $"{pre.Value:N1} to {post.Value:N1} {post.Unit.Trim()} ({(change >= 0 ? "+" : "")}{change:N1}" +
                      (percent is { } p ? $", {(p >= 0 ? "+" : "")}{p:N1}%" : "") + ").";
        return new(true, change, percent, warnings, summary);
    }

    private static string Show(string value) => string.IsNullOrWhiteSpace(value) ? "not recorded" : value.Trim();
}
