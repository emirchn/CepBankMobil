const systemTheme = window.matchMedia("(prefers-color-scheme: dark)");
window.cepbankTheme = {
    apply(mode, accent = "#6268d5") {
        const selected = ["light", "dark", "device", "glassy", "custom"].includes(mode) ? mode : "device";
        const root = document.documentElement;
        root.style.setProperty("--custom-accent", /^#[0-9a-fA-F]{6}$/.test(accent) ? accent : "#6268d5");
        root.setAttribute("data-theme", selected);
        root.setAttribute("data-resolved-theme",
            selected === "device" ? (systemTheme.matches ? "dark" : "light") : selected);
    }
};
window.notesSurface = {
    apply(dark, mode) {
        const color = mode === "forest" ? "#051F20" : mode === "violet" ? "#0B0720" : mode === "glassy" ? "#100C20" : dark ? "#11161C" : "#F7F8FA";
        document.documentElement.style.background = color;
        document.body.style.background = color;
    },
    restore() {
        document.documentElement.style.removeProperty("background");
        document.body.style.removeProperty("background");
    }
};
systemTheme.addEventListener("change", () => {
    if (document.documentElement.getAttribute("data-theme") === "device")
        window.cepbankTheme.apply("device");
});
