window.dbtracker = {
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
    }
};
