using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Builds one AuditEvent, including the rule for recording a System actor versus a named user.</summary>
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
