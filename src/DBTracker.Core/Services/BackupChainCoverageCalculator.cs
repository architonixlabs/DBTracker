using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ChainMemberCoverage(ChainMemberType MemberType, DateTime? LastSuccessfulUtc, bool Satisfied);

public record BackupChainCoverageResult(
    ChainCoverageStatus Status,
    DateTime? RecoverablePointUtc,
    IReadOnlyList<ChainMemberCoverage> Members,
    IReadOnlyList<string> MissingReasons);

/// <summary>
/// D16 — pure computation of a database instance's backup-chain coverage against whatever
/// BackupChainRequirement rows have actually been recorded for it. No requirements recorded means
/// Unknown, not Complete — a chain concept is never forced onto an engine nobody has configured one
/// for. A required Base that isn't satisfied always blocks a recoverable point entirely: a fresh
/// TransactionLog/WalArchive/Binlog record is never, by itself, treated as proof of a valid base or
/// end-to-end recoverability, however recent it is.
/// </summary>
public static class BackupChainCoverageCalculator
{
    public static ChainMemberType EffectiveChainRole(BackupRecord record) => record.ChainRole ?? MapFromType(record.Type);

    private static ChainMemberType MapFromType(BackupType type) => type switch
    {
        BackupType.Full => ChainMemberType.Base,
        BackupType.Differential => ChainMemberType.Differential,
        BackupType.TransactionLog => ChainMemberType.TransactionLog,
        _ => ChainMemberType.Base
    };

    public static BackupChainCoverageResult Compute(
        IReadOnlyCollection<BackupChainRequirement> requirements,
        IReadOnlyCollection<BackupRecord> records,
        DateTime nowUtc,
        int defaultMaxAgeHours)
    {
        var required = requirements.Where(r => r.IsRequired).ToList();
        if (required.Count == 0)
        {
            return new BackupChainCoverageResult(ChainCoverageStatus.Unknown, null, [],
                ["No backup-chain requirements recorded for this instance — coverage can't be evaluated."]);
        }

        var members = new List<ChainMemberCoverage>();
        var missing = new List<string>();

        foreach (var requirement in required)
        {
            var maxAgeHours = Math.Max(1, requirement.MaxAgeHoursOverride ?? defaultMaxAgeHours);
            var cutoffUtc = nowUtc.AddHours(-maxAgeHours);

            var last = records
                .Where(r => r.Status == BackupStatus.Success
                            && r.TimestampUtc <= nowUtc
                            && EffectiveChainRole(r) == requirement.MemberType)
                .OrderByDescending(r => r.TimestampUtc)
                .FirstOrDefault();

            var satisfied = last is not null && last.TimestampUtc >= cutoffUtc;
            members.Add(new ChainMemberCoverage(requirement.MemberType, last?.TimestampUtc, satisfied));

            if (!satisfied)
            {
                missing.Add(last is null
                    ? $"No successful {requirement.MemberType} evidence recorded."
                    : $"{requirement.MemberType} evidence is stale (last success {last!.TimestampUtc:u}, older than {maxAgeHours}h).");
            }
        }

        var baseMember = members.FirstOrDefault(m => m.MemberType == ChainMemberType.Base);

        if (missing.Count > 0)
        {
            // Incomplete chains can still name a recoverable point, but only up to a satisfied base —
            // never past unproven log/WAL/binlog evidence, and never at all if the base itself is missing.
            var recoverableDespiteGaps = baseMember is { Satisfied: true } ? baseMember.LastSuccessfulUtc : null;
            return new BackupChainCoverageResult(ChainCoverageStatus.Incomplete, recoverableDespiteGaps, members, missing);
        }

        var recoverablePoint = members.Where(m => m.Satisfied).Select(m => m.LastSuccessfulUtc).Max();
        return new BackupChainCoverageResult(ChainCoverageStatus.Complete, recoverablePoint, members, []);
    }
}
