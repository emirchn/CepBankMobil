let startupReadyPromise;
window.cepbankStartup = {
    ready() {
        if (startupReadyPromise) return startupReadyPromise;
        startupReadyPromise = new Promise(resolve => {
            const overlay = document.getElementById("startup-screen");
            if (!overlay) { resolve(); return; }
            const visibleFor = window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 1200;
            window.setTimeout(() => {
                overlay.classList.add("is-ready");
                window.setTimeout(() => { overlay.remove(); resolve(); }, 400);
            }, visibleFor);
        });
        return startupReadyPromise;
    }
};
window.setTimeout(() => window.cepbankStartup.ready(), 20000);
