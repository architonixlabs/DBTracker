using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Builds a single AuditEvent record from its parts (U19) — pure/testable so the "who gets recorded
/// as System vs. a named user" rule can be verified without a database.
/// </summary>
public static class AuditEventFactory
{
    public const string SystemActorPrefix = "Background: ";

    public static AuditEvent Create(
        AuditActorType actorType,
        string actorId,
        string actorName,
        string action,
        string targetType,
        string targetId,
        string targetName,
        DateTime nowUtc,
        string reason = "",
        string? beforeJson = null,
        string? afterJson = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action is required.", nameof(action));
        }

        return new AuditEvent
        {
            TimestampUtc = nowUtc,
            ActorType = actorType,
            ActorUserId = actorType == AuditActorType.User ? actorId : "",
            ActorName = actorType == AuditActorType.System && !actorName.StartsWith(SystemActorPrefix, StringComparison.Ordinal)
                ? SystemActorPrefix + actorName
                : actorName,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson
        };
    }

    public static AuditEvent ForSystem(string workerName, string action, string targetType, string targetId, string targetName, DateTime nowUtc, string reason = "") =>
        Create(AuditActorType.System, "", workerName, action, targetType, targetId, targetName, nowUtc, reason);

    public static AuditEvent ForUser(string userId, string userName, string action, string targetType, string targetId, string targetName, DateTime nowUtc, string reason = "", string? beforeJson = null, string? afterJson = null) =>
        Create(AuditActorType.User, userId, userName, action, targetType, targetId, targetName, nowUtc, reason, beforeJson, afterJson);
}
