(function () {
    const toggle = document.getElementById("qr-toggle");
    const panel = document.getElementById("qr-panel");
    const image = document.getElementById("qr-image");
    const status = document.getElementById("qr-status");

    if (!toggle || !panel) {
        return;
    }

    let token = null;
    let pollHandle = null;

    async function startSession() {
        status.textContent = "";
        const response = await fetch("/device-link/create", { method: "POST" });
        if (!response.ok) {
            status.textContent = "Could not start a phone sign-in session. Try again.";
            return;
        }
        const data = await response.json();
        token = data.token;
        image.src = "/device-link/qr/" + encodeURIComponent(token) + "?t=" + Date.now();
        poll();
    }

    function poll() {
        clearTimeout(pollHandle);
        pollHandle = setTimeout(checkStatus, 2000);
    }

    async function checkStatus() {
        if (!token) {
            return;
        }
        try {
            const response = await fetch("/device-link/status/" + encodeURIComponent(token));
            if (!response.ok) {
                poll();
                return;
            }
            const data = await response.json();
            if (data.status === "Approved") {
                status.textContent = "Approved. Logging you in…";
                const redeemed = await fetch("/device-link/redeem/" + encodeURIComponent(token), { method: "POST" });
                if (redeemed.ok) {
                    window.location.href = "/";
                } else {
                    status.textContent = "That request expired. Open the QR panel again.";
                }
                return;
            }
            if (data.status === "Denied") {
                status.textContent = "Sign-in was denied on your phone.";
                return;
            }
            if (data.status === "Expired") {
                status.textContent = "This QR code expired. Open the panel again for a new one.";
                return;
            }
            poll();
        } catch {
            poll();
        }
    }

    toggle.addEventListener("click", function () {
        const isHidden = panel.hasAttribute("hidden");
        toggle.setAttribute("aria-expanded", isHidden ? "true" : "false");
        if (isHidden) {
            panel.removeAttribute("hidden");
            startSession();
        } else {
            panel.setAttribute("hidden", "");
            clearTimeout(pollHandle);
            token = null;
        }
    });
})();
