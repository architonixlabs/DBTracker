using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>What a reviewer must supply for an advisory review. Every review needs evidence. Confirmed needs a remediation
/// owner, and a deadline until the fix is verified, so a confirmed exposure is never left unowned or undated. Verified
/// can't sit on an assessment whose applicability is still Unknown.</summary>
public static class AdvisoryReviewValidator
{
    public static string? Validate(AdvisoryClassification classification, string reviewerEvidence, string owner,
        DateTime? remediationDeadlineUtc, AdvisoryVerificationStatus verificationStatus)
    {
        if (string.IsNullOrWhiteSpace(reviewerEvidence))
        {
            return "Evidence is required for a review.";
        }
        if (classification == AdvisoryClassification.Confirmed)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                return "A confirmed advisory needs a remediation owner.";
            }
            if (remediationDeadlineUtc is null && verificationStatus != AdvisoryVerificationStatus.Verified)
            {
                return "A confirmed advisory needs a remediation deadline until the fix is verified.";
            }
        }
        if (verificationStatus == AdvisoryVerificationStatus.Verified && classification == AdvisoryClassification.Unknown)
        {
            return "Verified can't be recorded while applicability is still Unknown.";
        }
        return null;
    }
}
