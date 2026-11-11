// G26: shared WebAuthn helpers for the sign-in page (passkey-login.js) and the Passkeys page (passkeys.js).
// The polyfill fills in PublicKeyCredential.parseCreationOptionsFromJSON / parseRequestOptionsFromJSON, credential.toJSON()
// and getClientCapabilities() on browsers that have WebAuthn but not those newer helpers. It changes nothing where they exist.
import "../lib/webauthn-polyfills/webauthn-polyfills-0.1.7.min.js";

/** WebAuthn needs a secure context (https, or http://localhost) and a browser that has PublicKeyCredential. */
export function webAuthnAvailable() {
    return window.isSecureContext === true && typeof window.PublicKeyCredential === "function";
}

export async function clientCapabilities() {
    if (!webAuthnAvailable() || typeof PublicKeyCredential.getClientCapabilities !== "function") {
        return {};
    }
    try {
        return (await PublicKeyCredential.getClientCapabilities()) || {};
    } catch {
        return {};
    }
}

/** Can this device create a passkey? The polyfill can't detect passkeyPlatformAuthenticator, so fall back to a user-verifying platform authenticator. */
export function canCreatePasskey(capabilities) {
    if (capabilities.passkeyPlatformAuthenticator === true) {
        return true;
    }
    return capabilities.passkeyPlatformAuthenticator === undefined && capabilities.userVerifyingPlatformAuthenticator === true;
}

/** POST JSON with the antiforgery token in the header the server validates. Returns { ok, status, body }. */
export async function postJson(url, antiforgeryToken, payload) {
    const response = await fetch(url, {
        method: "POST",
        credentials: "same-origin",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": antiforgeryToken
        },
        body: payload === undefined ? undefined : JSON.stringify(payload)
    });
    let body = null;
    try {
        body = await response.json();
    } catch {
        body = null;
    }
    return { ok: response.ok, status: response.status, body };
}

// Valid unpadded Base64URL: [A-Za-z0-9_-] only, no padding, and length % 4 !== 1. The Signal API throws on anything else.
export function isBase64Url(value) {
    return typeof value === "string" && value.length > 0 && value.length % 4 !== 1 && /^[A-Za-z0-9_-]+$/.test(value);
}

/** Tells the passkey provider the server doesn't know this credential, so it stops offering it. Passive and never fatal. */
export async function signalUnknownCredential(rpId, credentialId) {
    if (!rpId || !isBase64Url(credentialId) || typeof PublicKeyCredential.signalUnknownCredential !== "function") {
        return;
    }
    try {
        await PublicKeyCredential.signalUnknownCredential({ rpId, credentialId });
    } catch (e) {
        console.warn("signalUnknownCredential sync warning:", e);
    }
}

/** Keeps the passkey provider's list in step with the server's after a load or a removal. Passive and never fatal. */
export async function signalAllAcceptedCredentials(rpId, userId, credentialIds) {
    if (!webAuthnAvailable() || typeof PublicKeyCredential.signalAllAcceptedCredentials !== "function") {
        return;
    }
    if (!rpId || !isBase64Url(userId) || !Array.isArray(credentialIds) || !credentialIds.every(isBase64Url)) {
        console.warn("signalAllAcceptedCredentials skipped: the RP ID and Base64URL user and credential IDs are required.");
        return;
    }
    try {
        await PublicKeyCredential.signalAllAcceptedCredentials({ rpId, userId, allAcceptedCredentialIds: credentialIds });
    } catch (e) {
        console.warn("signalAllAcceptedCredentials sync warning:", e);
    }
}
