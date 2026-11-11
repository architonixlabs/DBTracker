// G26: the Passkeys page (/account/passkeys). Blazor owns the list, rename and remove; this module owns the parts that need the
// browser's WebAuthn API: checking support, creating a passkey, and keeping the passkey provider in step (Signal API).
// The Add button's click is handled here, not over the Blazor circuit, so navigator.credentials.create() runs inside the user's
// click: Safari refuses to show the passkey prompt without that gesture.
import {
    webAuthnAvailable,
    clientCapabilities,
    canCreatePasskey,
    postJson,
    signalUnknownCredential,
    signalAllAcceptedCredentials,
    isBase64Url
} from "./passkey-support.js";

const handlers = new WeakMap();

/**
 * Checks support, wires the Add button and syncs the provider. Returns { supported, reason }.
 * @param {HTMLElement} root  the add-passkey card; carries data-antiforgery
 * @param {object} dotnet     DotNetObjectReference with a ReportAsync(kind, message) method
 * @param {{rpId: string, userId: string, credentialIds: string[], name: string, displayName: string}} account
 */
export async function init(root, dotnet, account) {
    if (!webAuthnAvailable()) {
        return {
            supported: false,
            reason: window.isSecureContext
                ? "This browser doesn't support passkeys."
                : "Passkeys need a secure (https) connection, and this page isn't using one."
        };
    }

    syncAccount(account);

    const capabilities = await clientCapabilities();
    if (!canCreatePasskey(capabilities)) {
        return { supported: false, reason: "This device can't create passkeys in this browser." };
    }

    if (!handlers.has(root)) {
        const onClick = (event) => {
            const button = event.target.closest("[data-passkey-add]");
            if (button && root.contains(button)) {
                register(root, button, dotnet);
            }
        };
        root.addEventListener("click", onClick);
        handlers.set(root, onClick);
    }
    return { supported: true, reason: "" };
}

export function dispose(root) {
    const onClick = root && handlers.get(root);
    if (onClick) {
        root.removeEventListener("click", onClick);
        handlers.delete(root);
    }
}

/** After a removal (or on load): tell the passkey provider which credentials the server still accepts. */
export async function syncAccepted(account) {
    await signalAllAcceptedCredentials(account.rpId, account.userId, account.credentialIds);
}

async function syncAccount(account) {
    await signalAllAcceptedCredentials(account.rpId, account.userId, account.credentialIds);
    // Keeps the name shown in the provider's account picker current if an Administrator changed it.
    if (typeof PublicKeyCredential.signalCurrentUserDetails === "function" && account.rpId && isBase64Url(account.userId)) {
        try {
            await PublicKeyCredential.signalCurrentUserDetails({
                rpId: account.rpId,
                userId: account.userId,
                name: account.name,
                displayName: account.displayName
            });
        } catch (e) {
            console.warn("signalCurrentUserDetails sync warning:", e);
        }
    }
}

async function register(root, button, dotnet) {
    if (button.disabled) {
        return;
    }
    button.disabled = true;
    button.setAttribute("aria-busy", "true");
    const nameInput = root.querySelector("[data-passkey-name]");
    const token = root.dataset.antiforgery || "";
    const report = (kind, message) => dotnet.invokeMethodAsync("ReportAsync", kind, message);

    try {
        await report("progress", "Follow the prompt from your browser or device to create the passkey.");

        const optionsResponse = await postJson("/Account/Passkeys/CreationOptions", token);
        if (!optionsResponse.ok || !optionsResponse.body) {
            await report("error", (optionsResponse.body && optionsResponse.body.message) || "Passkeys can't be added right now. Try again later.");
            return;
        }
        const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(optionsResponse.body);

        let credential;
        try {
            credential = await navigator.credentials.create({ publicKey });
        } catch (err) {
            const name = err && err.name;
            if (name === "InvalidStateError") {
                await report("error", "This passkey provider already has a passkey for your account. Use that one, or pick a different provider.");
            } else if (name === "NotAllowedError") {
                await report("cancelled", "No passkey was added. The prompt was cancelled or timed out.");
            } else if (name === "AbortError") {
                await report("cancelled", "No passkey was added.");
            } else if (name === "SecurityError") {
                await report("error", "Passkeys aren't set up for this web address. Tell your Administrator (see Passkeys:ServerDomain in DEPLOYMENT.md).");
            } else {
                await report("error", "The passkey couldn't be created. Try again.");
            }
            return; // A cancelled or refused prompt created nothing, so there's nothing to signal.
        }

        const encoded = credential.toJSON();
        let saved;
        try {
            saved = await postJson("/Account/Passkeys/Register", token, {
                credential: encoded,
                name: nameInput ? nameInput.value : ""
            });
        } catch {
            saved = { ok: false, body: { message: "DBTracker couldn't be reached, so the passkey wasn't saved. Try again." } };
        }

        if (!saved.ok) {
            // The provider now holds a credential the server never stored; ask it to drop that orphan.
            await signalUnknownCredential(publicKey.rp && publicKey.rp.id, encoded.id);
            await report("error", (saved.body && saved.body.message) || "The passkey couldn't be saved. Try again.");
            return;
        }

        if (nameInput) {
            nameInput.value = "";
        }
        await report("added", (saved.body && saved.body.message) || "Passkey added.");
    } catch (err) {
        console.warn("Passkey registration failed:", err);
        try {
            await report("error", "The passkey couldn't be added. Try again.");
        } catch {
            // The circuit is gone; the reconnect UI already tells the user.
        }
    } finally {
        button.disabled = false;
        button.removeAttribute("aria-busy");
    }
}
