const systemTheme = window.matchMedia("(prefers-color-scheme: dark)");
window.cepbankTheme = {
    apply(mode) {
        const selected = ["light", "dark", "device", "glassy"].includes(mode) ? mode : "device";
        document.documentElement.setAttribute("data-theme", selected);
        document.documentElement.setAttribute("data-resolved-theme",
            selected === "device" ? (systemTheme.matches ? "dark" : "light") : selected);
    }
};
systemTheme.addEventListener("change", () => {
    if (document.documentElement.getAttribute("data-theme") === "device")
        window.cepbankTheme.apply("device");
});
