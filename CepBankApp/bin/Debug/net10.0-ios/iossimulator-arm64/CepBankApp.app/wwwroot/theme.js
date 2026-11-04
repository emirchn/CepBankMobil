const systemTheme = window.matchMedia("(prefers-color-scheme: dark)");
window.cepbankTheme = {
    apply(mode, accent = "#6268d5", wallpaper = "") {
        const selected = ["light", "dark", "device", "glassy", "custom"].includes(mode) ? mode : "device";
        const root = document.documentElement;
        root.style.setProperty("--custom-accent", /^#[0-9a-fA-F]{6}$/.test(accent) ? accent : "#6268d5");
        if (wallpaper.startsWith("data:image/")) root.style.setProperty("--custom-wallpaper", `url("${wallpaper}")`);
        else root.style.removeProperty("--custom-wallpaper");
        root.setAttribute("data-theme", selected);
        root.setAttribute("data-resolved-theme",
            selected === "device" ? (systemTheme.matches ? "dark" : "light") : selected);
    }
};
systemTheme.addEventListener("change", () => {
    if (document.documentElement.getAttribute("data-theme") === "device")
        window.cepbankTheme.apply("device");
});
