window.dbtracker = {
    // G11: the viewer's IANA time zone, so times show in the reader's zone with the zone named instead of the server's.
    timeZone: function () {
        try { return Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC"; } catch (e) { return "UTC"; }
    },
    // U13: warn on an actual browser tab close/refresh while a form has unsaved changes. In-app
    // navigation (clicking another nav link) is handled separately via
    // NavigationManager.RegisterLocationChangingHandler, which can show a real confirm() dialog;
    // beforeunload cannot show custom text in modern browsers, so this just arms the native prompt.
    setUnloadGuard: function (enabled) {
        window.onbeforeunload = enabled ? function (e) { e.preventDefault(); return ""; } : null;
    },
    confirmLeave: function (message) {
        return window.confirm(message);
    },
    downloadCsv: function (fileName, content) {
        const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    },

    // G29: native modal dialogs (Components/Shared/ModalDialog.razor). showModal() gives the inert page,
    // focus containment, Esc and focus return; this only reports the close back to .NET.
    dialog: {
        open: function (el, dotnet, fallbackFocus) {
            if (!el || typeof el.showModal !== "function") return;
            el._dbtRef = dotnet;
            el._dbtFallback = fallbackFocus || null;
            if (!el._dbtWired) {
                el._dbtWired = true;
                el.addEventListener("close", function () {
                    // Esc sets no returnValue and browsers differ on clearing the old one.
                    el.returnValue = "";
                    const fallback = el._dbtFallback;
                    // The browser returns focus to the opener. If the confirmed action removed it (a revoked
                    // token's button), focus the given landmark instead of leaving keyboard users on <body>.
                    setTimeout(function () {
                        const active = document.activeElement;
                        if (!fallback || (active && active !== document.body)) return;
                        const target = document.querySelector(fallback);
                        if (!target) return;
                        if (!target.matches("a[href], button, input, select, textarea, [tabindex]")) target.setAttribute("tabindex", "-1");
                        target.focus();
                    }, 0);
                    const ref = el._dbtRef;
                    if (ref) ref.invokeMethodAsync("OnDialogClosed").catch(function () { });
                });
            }
            if (!el.open) el.showModal();
        },
        close: function (el) {
            if (el && el.open) el.close();
        },
        release: function (el) {
            if (!el) return;
            el._dbtRef = null;
            if (el.open) el.close();
        }
    },

    // G30: the notification menu is a popover="auto" (light dismiss, top layer, focus return are native).
    popover: {
        supported: function () {
            return Object.prototype.hasOwnProperty.call(HTMLElement.prototype, "popover");
        },
        watch: function (el, dotnet) {
            if (!el) return;
            el._dbtRef = dotnet;
            if (el._dbtWired) return;
            el._dbtWired = true;
            el.addEventListener("toggle", function (e) {
                const ref = el._dbtRef;
                if (ref) ref.invokeMethodAsync("OnPopoverToggle", e.newState === "open").catch(function () { });
            });
        },
        hide: function (el) {
            try {
                if (el && el.matches(":popover-open")) el.hidePopover();
            } catch (e) { }
        },
        // Content arrives a render after the popover opens, so re-measure the fallback position then.
        reposition: function (el) {
            if (el && window.dbtracker._placeAnchored) window.dbtracker._placeAnchored(el);
        },
        release: function (el) {
            if (el) el._dbtRef = null;
        }
    }
};

// Theme switch (Components/Shared/ThemeSwitch.razor): apply the choice at once and keep it in the dbt-theme cookie,
// which App.razor reads on the next page load to set data-theme before first paint. A delegated listener, so it
// works on every page and while the Blazor circuit is reconnecting.
(function () {
    const allowed = ["system", "dark", "light"];
    document.addEventListener("change", function (e) {
        const input = e.target;
        if (!(input instanceof HTMLInputElement) || input.name !== "dbt-theme" || !input.checked) return;
        const value = allowed.indexOf(input.value) >= 0 ? input.value : "dark";
        document.documentElement.setAttribute("data-theme", value);
        document.cookie = "dbt-theme=" + value + "; Path=/; Max-Age=31536000; SameSite=Lax" +
            (location.protocol === "https:" ? "; Secure" : "");
    });
})();

// G30: shared behavior for anchored popovers (the notification menu and toggletips).
(function () {
    const root = document.documentElement;
    if (!Object.prototype.hasOwnProperty.call(HTMLElement.prototype, "popover")) {
        // No Popover API: the notification menu falls back to Blazor-rendered show/hide and toggletip text
        // shows inline (see app.css, html.no-popover).
        root.classList.add("no-popover");
        return;
    }

    const anchorSupported = !!(window.CSS && CSS.supports("position-area: block-end"));
    const probe = document.createElement("div");
    probe.popover = "hint";
    const hintSupported = probe.popover === "hint";
    const triggers = new WeakMap();

    function invokerFor(pop, event) {
        if (event && event.source instanceof Element) return event.source;
        return triggers.get(pop) || document.querySelector('[popovertarget="' + CSS.escape(pop.id) + '"]');
    }

    // Fallback placement where CSS anchor positioning is missing: below the trigger, start-aligned,
    // flipped above when it would run off the bottom, and kept 8px inside the viewport.
    function place(pop, trigger) {
        if (anchorSupported || !trigger || !trigger.isConnected) return;
        const r = trigger.getBoundingClientRect();
        const gap = 6, edge = 8;
        const w = pop.offsetWidth, h = pop.offsetHeight;
        const vw = document.documentElement.clientWidth, vh = window.innerHeight;
        let left = r.left;
        if (left + w > vw - edge) left = vw - edge - w;
        if (left < edge) left = edge;
        let top = r.bottom + gap;
        if (top + h > vh - edge && r.top - gap - h >= edge) top = r.top - gap - h;
        pop.style.left = Math.round(left) + "px";
        pop.style.top = Math.round(top) + "px";
    }

    window.dbtracker._placeAnchored = function (pop) {
        if (pop.matches(":popover-open")) place(pop, triggers.get(pop) || invokerFor(pop));
    };

    // beforetoggle and toggle do not bubble: listen in the capture phase.
    document.addEventListener("beforetoggle", function (e) {
        const pop = e.target;
        if (!(pop instanceof HTMLElement) || e.newState !== "open") return;
        // popover="hint" reads as "manual" where unsupported, so a clicked toggletip would never close.
        // The type must change here, before it opens.
        if (!hintSupported && pop.hasAttribute("data-toggletip") && pop.getAttribute("popover") === "hint") {
            pop.popover = "auto";
        }
        if (pop.classList.contains("dbt-anchored")) {
            const trigger = invokerFor(pop, e);
            if (trigger) triggers.set(pop, trigger);
            if (!anchorSupported && trigger) {
                // A first guess before it renders, so it never flashes at the viewport's corner.
                const r = trigger.getBoundingClientRect();
                pop.style.left = Math.round(r.left) + "px";
                pop.style.top = Math.round(r.bottom + 6) + "px";
            }
        }
    }, true);

    document.addEventListener("toggle", function (e) {
        const pop = e.target;
        if (!(pop instanceof HTMLElement)) return;
        if (pop.hasAttribute("data-toggletip")) {
            const expanded = e.newState === "open" ? "true" : "false";
            document.querySelectorAll('[popovertarget="' + CSS.escape(pop.id) + '"]').forEach(function (b) {
                b.setAttribute("aria-expanded", expanded);
            });
        }
        if (e.newState === "open" && pop.classList.contains("dbt-anchored")) place(pop, invokerFor(pop, e));
    }, true);

    if (!anchorSupported) {
        let frame = 0;
        const follow = function () {
            if (frame) return;
            frame = requestAnimationFrame(function () {
                frame = 0;
                document.querySelectorAll(".dbt-anchored:popover-open").forEach(function (pop) {
                    place(pop, triggers.get(pop));
                });
            });
        };
        window.addEventListener("scroll", follow, { capture: true, passive: true });
        window.addEventListener("resize", follow, { passive: true });
    }
})();
