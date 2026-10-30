let startupReadyPromise;
window.cepbankStartup = {
    ready() {
        if (startupReadyPromise) return startupReadyPromise;
        startupReadyPromise = new Promise(resolve => {
            const overlay = document.getElementById("startup-screen");
            if (!overlay) { resolve(); return; }
            overlay.classList.add("is-ready");
            window.setTimeout(() => { overlay.remove(); resolve(); }, 400);
        });
        return startupReadyPromise;
    }
};
window.setTimeout(() => window.cepbankStartup.ready(), 20000);
