// G26: passkey sign-in on /Account/Login. Two ways in: the "Sign in with a passkey" button, and passkeys offered in the email
// field's autofill list (conditional mediation, autocomplete="username webauthn"). The password form stays as the fallback.
import {
    webAuthnAvailable,
    clientCapabilities,
    postJson,
    signalUnknownCredential
} from "./passkey-support.js";

const section = document.getElementById("passkey-signin");
const button = document.getElementById("passkey-signin-button");
const statusEl = document.getElementById("passkey-status");
const errorEl = document.getElementById("passkey-error");

let autofillController = null;
// The server keeps one challenge per browser, so a new options request must wait for any earlier one to land.
let pendingOptions = null;
let busy = false;

function antiforgeryToken() {
    const field = document.querySelector("form input[name='__RequestVerificationToken']");
    return field ? field.value : "";
}

function showStatus(text) {
    errorEl.textContent = "";
    statusEl.textContent = text;
}

function showError(text) {
    statusEl.textContent = "";
    errorEl.textContent = text;
}

async function fetchRequestOptions() {
    if (pendingOptions) {
        try { await pendingOptions; } catch { /* the earlier request's failure is its own */ }
    }
    pendingOptions = requestOptions();
    return pendingOptions;
}

async function requestOptions() {
    const response = await postJson("/Account/Passkeys/RequestOptions", antiforgeryToken());
    if (!response.ok || !response.body) {
        throw new Error((response.body && response.body.message) || "Passkey sign-in isn't available right now. Sign in with your password.");
    }
    return response.body;
}

// Sends the signed assertion to the server. Returns true when the browser is navigating away.
async function verify(credential, publicKey) {
    const encoded = credential.toJSON();
    showStatus("Checking your passkey…");
    let response;
    try {
        response = await postJson("/Account/Passkeys/SignIn", antiforgeryToken(), {
            credential: encoded,
            returnUrl: section.dataset.returnUrl || null
        });
    } catch {
        showError("DBTracker couldn't be reached. Check your connection and try again.");
        return false;
    }

    if (response.ok && response.body && response.body.redirectUrl) {
        showStatus("Signed in. Opening DBTracker…");
        window.location.assign(response.body.redirectUrl);
        return true;
    }
    if (response.status === 404) {
        // Still signed out here, which is the precondition for signalling an unknown credential.
        await signalUnknownCredential(publicKey.rpId, encoded.id);
    }
    showError((response.body && response.body.message) || "Passkey sign-in didn't work. Try again, or sign in with your password.");
    return false;
}

function revealAutofillHint() {
    const hint = document.getElementById("login-email-hint");
    const email = document.getElementById("login-email");
    if (!hint || !email || !hint.hidden) {
        return;
    }
    hint.hidden = false;
    email.setAttribute("aria-describedby", ((email.getAttribute("aria-describedby") || "") + " login-email-hint").trim());
}

async function startAutofill() {
    const capabilities = await clientCapabilities();
    if (capabilities.conditionalGet !== true) {
        return;
    }
    revealAutofillHint();
    autofillController = new AbortController();
    const signal = autofillController.signal;
    let publicKey;
    let credential;
    try {
        publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(await fetchRequestOptions());
        if (signal.aborted) {
            return;
        }
        credential = await navigator.credentials.get({ publicKey, signal, mediation: "conditional" });
    } catch (err) {
        // Aborted when the button takes over, or the user dismissed autofill: nothing to report.
        if (err && (err.name === "AbortError" || err.name === "NotAllowedError")) {
            return;
        }
        console.warn("Passkey autofill is unavailable:", err);
        return;
    }
    if (credential) {
        busy = true;
        if (!(await verify(credential, publicKey))) {
            busy = false;
            startAutofill();
        }
    }
}

async function signInWithButton() {
    if (busy) {
        return;
    }
    busy = true;
    button.disabled = true;
    // Only one WebAuthn request can be pending, so stop the autofill one first.
    if (autofillController) {
        autofillController.abort();
        autofillController = null;
    }

    let publicKey;
    let credential;
    try {
        showStatus("Follow the prompt from your browser or device to use your passkey.");
        publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(await fetchRequestOptions());
        credential = await navigator.credentials.get({ publicKey });
    } catch (err) {
        if (err && err.name === "NotAllowedError") {
            showStatus("Passkey sign-in was cancelled. You can try again or use your password.");
        } else if (err && err.name === "SecurityError") {
            showError("Passkeys aren't set up for this web address. Sign in with your password and tell your Administrator.");
        } else if (err && err.name === "AbortError") {
            showStatus("");
        } else {
            showError((err && err.message) || "Passkey sign-in didn't work. Try again, or sign in with your password.");
        }
        busy = false;
        button.disabled = false;
        startAutofill();
        return;
    }

    if (!(await verify(credential, publicKey))) {
        busy = false;
        button.disabled = false;
        startAutofill();
    }
}

(async function init() {
    if (!section || !button || !webAuthnAvailable()) {
        return; // No WebAuthn or not a secure context: the passkey option stays hidden and password sign-in is unchanged.
    }
    section.hidden = false;
    button.addEventListener("click", signInWithButton);
    startAutofill();
})();
