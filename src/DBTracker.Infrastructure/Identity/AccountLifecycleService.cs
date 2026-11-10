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

/// <summary>
/// Admin-facing account lifecycle operations (U16): role changes, deactivate/reactivate, and
/// expiring single-use reset/invite links built on ASP.NET Core Identity's own data-protection
/// token provider (GeneratePasswordResetTokenAsync) rather than a second parallel token table — the
/// token is already scoped to the user's current security stamp, so a successful ResetPasswordAsync
/// rotates the stamp and the same token can never be replayed. Every mutating call here is audited
/// (U19) and goes through the "don't remove the last active Admin" guard.
/// </summary>
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
                return new RoleChangeResult(false, "Can't change this user's role — they're the last active Admin.");
            }
        }

        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, newRole);

        // A role downgrade (Admin losing Admin) may reduce what the user's existing session is
        // allowed to do — rotate the security stamp so any active session re-validates against the
        // new role within IdentityRevalidatingAuthenticationStateProvider's revalidation window
        // (30 minutes) instead of continuing on stale claims until the cookie itself expires.
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
                    return new DeactivateResult(false, "Can't deactivate this user — they're the last active Admin.");
                }
            }
        }

        user.IsActive = isActive;
        await userManager.UpdateAsync(user);

        if (!isActive)
        {
            // Revoke the ability to use any session/device-link already issued to this account:
            // rotating the stamp invalidates the cookie claim within the revalidation window, and
            // any pending (not-yet-redeemed) device-link token this user had already approved for a
            // new device must stop working too, or a deactivated account could still bootstrap a
            // brand-new signed-in session through that side channel.
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

    /// <summary>
    /// Neutral "forgot password" response (U16): always returns a token/link when the account
    /// exists and a provider is configured, but the caller shows the same generic confirmation text
    /// to the requester either way — this method's AccountExists flag is for the admin-triggered
    /// "send reset link" path (which already knows the account exists) and tests, not for rendering
    /// a different message to an anonymous requester.
    /// </summary>
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
