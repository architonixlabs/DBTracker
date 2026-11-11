using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Entitlement;

public record EntitlementView(EntitlementResult Result, IReadOnlyList<SupportContract> Contracts);

/// <summary>
/// D21 — support-contract linking and reviewed edition-cost estimates. EntitlementStatusClassifier
/// (pure) is always computed live from this instance's SupportContract assignments only — it never
/// reads VersionIdentity/ReleaseCatalogEntry/SupportRunwayDecision, so the entitlement status reported
/// here and the instance's D02/D03 lifecycle/support-phase status are always two independent facts
/// that can legitimately disagree. Cost-estimate figures are commercial evidence: callers (the Web
/// pages) are responsible for applying CostEvidenceMasker/Roles.CommercialEvidencePolicy before
/// showing EstimatedAmount/Currency/Assumptions to a non-authorized role — this service itself returns
/// the real values and does no masking, since masking is a presentation concern applied per-viewer.
/// </summary>
public class SupportEntitlementService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<EntitlementView> GetEntitlementForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var assignments = await db.SupportContractAssignments.AsNoTracking()
            .Where(a => a.DatabaseInstanceId == instanceId)
            .Include(a => a.SupportContract!).ThenInclude(c => c!.OwnerTeam)
            .ToListAsync();
        var contracts = assignments.Select(a => a.SupportContract!).ToList();

        var result = EntitlementStatusClassifier.Classify(contracts, DateTime.UtcNow);
        return new EntitlementView(result, contracts);
    }

    public async Task<SupportContract> CreateContractAsync(SupportContract contract, IEnumerable<int> instanceIds, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        contract.CreatedAtUtc = DateTime.UtcNow;
        contract.UpdatedAtUtc = DateTime.UtcNow;
        contract.CreatedBy = actorName;
        contract.UpdatedBy = actorName;

        foreach (var instanceId in instanceIds.Distinct())
        {
            contract.Assignments.Add(new SupportContractAssignment { DatabaseInstanceId = instanceId, CreatedBy = actorName });
        }

        db.SupportContracts.Add(contract);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "SupportContractCreated", "SupportContract", contract.Id.ToString(), contract.Vendor,
            reason: $"{contract.CoverageType} coverage, {contract.Assignments.Count} instance(s) assigned");
        return contract;
    }

    public async Task<bool> AssignContractToInstanceAsync(int contractId, int instanceId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var contract = await db.SupportContracts.FirstOrDefaultAsync(c => c.Id == contractId);
        var instance = await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == instanceId);
        if (contract is null || instance is null)
        {
            return false;
        }

        var exists = await db.SupportContractAssignments.AnyAsync(a => a.SupportContractId == contractId && a.DatabaseInstanceId == instanceId);
        if (exists)
        {
            return true;
        }

        db.SupportContractAssignments.Add(new SupportContractAssignment { SupportContractId = contractId, DatabaseInstanceId = instanceId, CreatedBy = actorName });
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "SupportContractAssigned", "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: $"{contract.Vendor} ({contract.CoverageType})");
        return true;
    }

    public async Task<List<EditionCostEstimate>> ListCostEstimatesAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.EditionCostEstimates.AsNoTracking()
            .Where(e => e.DatabaseInstanceId == instanceId)
            .OrderByDescending(e => e.EstimateDateUtc)
            .ToListAsync();
    }

    public async Task<EditionCostEstimate> AddCostEstimateAsync(EditionCostEstimate estimate, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        estimate.CreatedAtUtc = DateTime.UtcNow;
        estimate.CreatedBy = actorName;
        db.EditionCostEstimates.Add(estimate);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == estimate.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "EditionCostEstimateRecorded", "DatabaseInstance", estimate.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{estimate.EstimateType}: {estimate.EstimatedAmount:N2} {estimate.Currency} — {estimate.TargetEditionOrPath}");
        return estimate;
    }
}
