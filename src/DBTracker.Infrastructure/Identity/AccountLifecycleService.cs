using DBTracker.Core.Enums;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DBTracker.Infrastructure.Identity;

public record RoleChangeResult(bool Succeeded, string? Error);
public record DeactivateResult(bool Succeeded, string? Error);
public record ResetLinkOutcome(bool AccountExists, EmailSendResult? EmailResult);

/// <summary>Admin account operations: role changes, deactivate/reactivate, and reset/invite links. Every change is audited and guarded against removing the last active Admin.</summary>
/// <remarks>Reset links use Identity's own token provider; a successful reset rotates the security stamp, so a link can't be replayed.</remarks>
public class AccountLifecycleService(
    UserManager<ApplicationUser> userManager,
    DBTrackerDbContext db,
    IEmailSender emailSender,
    AuditRecorder audit)
{
    public async Task<int> CountActiveAdminsAsync(string? excludingUserId = null)
    {
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        return admins.Count(u => u.IsActive && u.Id != excludingUserId);
    }

    public async Task<RoleChangeResult> ChangeRoleAsync(string userId, string newRole, string actorId, string actorName)
    {
        if (!Roles.All.Contains(newRole))
        {
            return new RoleChangeResult(false, $"'{newRole}' is not a recognized role.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new RoleChangeResult(false, "User not found.");
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Contains(newRole) && currentRoles.Count == 1)
        {
            return new RoleChangeResult(true, null); // no-op
        }

        if (currentRoles.Contains(Roles.Admin) && newRole != Roles.Admin)
        {
            var remainingAdmins = await CountActiveAdminsAsync(excludingUserId: userId);
            if (remainingAdmins == 0)
            {
                return new RoleChangeResult(false, "Can't change this user's role: they're the last active Admin.");
            }
        }

        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, newRole);

        // Rotate the security stamp so active sessions pick up the reduced role within the 30-minute revalidation window.
        if (currentRoles.Contains(Roles.Admin) && newRole != Roles.Admin)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }

        await audit.RecordUserActionAsync(actorId, actorName, "UserRoleChanged", "User", userId, user.Email ?? user.UserName ?? userId,
            reason: $"{string.Join(",", currentRoles)} -> {newRole}");

        return new RoleChangeResult(true, null);
    }

    public async Task<DeactivateResult> SetActiveAsync(string userId, bool isActive, string actorId, string actorName)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new DeactivateResult(false, "User not found.");
        }

        if (!isActive)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Contains(Roles.Admin))
            {
                var remainingAdmins = await CountActiveAdminsAsync(excludingUserId: userId);
                if (remainingAdmins == 0)
                {
                    return new DeactivateResult(false, "Can't deactivate this user: they're the last active Admin.");
                }
            }
        }

        user.IsActive = isActive;
        await userManager.UpdateAsync(user);

        if (!isActive)
        {
            // Rotating the stamp ends cookie sessions within the revalidation window; pending device-link tokens must also be revoked,
            // or a deactivated account could still sign in a new device.
            await userManager.UpdateSecurityStampAsync(user);
            var pendingApprovedLinks = await db.DeviceLinkTokens
                .Where(t => t.ApprovedByUserId == userId && t.Status == DeviceLinkStatus.Approved)
                .ToListAsync();
            foreach (var link in pendingApprovedLinks)
            {
                link.Status = DeviceLinkStatus.Denied;
            }
            if (pendingApprovedLinks.Count > 0)
            {
                await db.SaveChangesAsync();
            }
        }

        await audit.RecordUserActionAsync(actorId, actorName, isActive ? "UserReactivated" : "UserDeactivated", "User", userId, user.Email ?? user.UserName ?? userId);
        return new DeactivateResult(true, null);
    }

    public async Task<bool> ChangeOwnPasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return false;
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await audit.RecordUserActionAsync(userId, user.Email ?? user.UserName ?? userId, "PasswordChangedBySelf", "User", userId, user.Email ?? user.UserName ?? userId);
        return true;
    }

    /// <summary>Forgot-password support. Callers always show the same confirmation text; AccountExists is for the admin path and tests, never for anonymous requesters.</summary>
    public async Task<ResetLinkOutcome> SendPasswordResetLinkAsync(string email, string baseUri, string actorId = "", string actorName = "")
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return new ResetLinkOutcome(false, null);
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = $"{baseUri.TrimEnd('/')}/Account/ResetPassword?userId={Uri.EscapeDataString(user.Id)}&code={encoded}";

        var body = $"A password reset was requested for your DBTracker account.\n\n" +
                   $"Reset it here (expires soon, single use): {link}\n\n" +
                   "If you didn't request this, you can ignore this email.";
        var result = await emailSender.SendAsync(user.Email!, "DBTracker password reset", body);

        var actingId = string.IsNullOrEmpty(actorId) ? user.Id : actorId;
        var actingName = string.IsNullOrEmpty(actorName) ? "self-service reset request" : actorName;
        await audit.RecordUserActionAsync(actingId, actingName, "PasswordResetLinkSent", "User", user.Id, user.Email ?? user.UserName ?? user.Id,
            reason: $"Delivery: {result.Outcome}");

        return new ResetLinkOutcome(true, result);
    }

    public async Task<EmailSendResult> SendInviteAsync(ApplicationUser user, string temporaryPassword, string baseUri, string actorId, string actorName)
    {
        var body = $"An account was created for you on DBTracker.\n\n" +
                   $"Sign in at {baseUri.TrimEnd('/')}/Account/Login with:\nEmail: {user.Email}\nTemporary password: {temporaryPassword}\n\n" +
                   "You'll be asked to set your own password on first sign-in.";
        var result = await emailSender.SendAsync(user.Email!, "Your DBTracker account", body);
        await audit.RecordUserActionAsync(actorId, actorName, "UserInviteSent", "User", user.Id, user.Email ?? user.UserName ?? user.Id,
            reason: $"Delivery: {result.Outcome}");
        return result;
    }
}
