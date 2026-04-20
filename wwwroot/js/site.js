// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    const THEME_STORAGE_KEY = "employee-documents-viewer-theme";
    const DARK_THEME = "dark";
    const LIGHT_THEME = "light";

    const tooltipText = (theme) => `Switch between dark and light mode (currently ${theme} mode)`;

    const applyTheme = (theme, toggleButton, toggleIcon) => {
        const isDarkTheme = theme === DARK_THEME;

        document.body.classList.toggle("theme-dark", isDarkTheme);
        document.body.classList.toggle("theme-light", !isDarkTheme);

        if (toggleIcon) {
            toggleIcon.textContent = isDarkTheme ? "🌙" : "☀️";
        }

        if (toggleButton) {
            const title = tooltipText(theme);
            toggleButton.title = title;
            toggleButton.setAttribute("aria-label", title);
        }

        localStorage.setItem(THEME_STORAGE_KEY, theme);
    };

    const getInitialTheme = () => {
        const savedTheme = localStorage.getItem(THEME_STORAGE_KEY);

        if (savedTheme === DARK_THEME || savedTheme === LIGHT_THEME) {
            return savedTheme;
        }

        return window.matchMedia("(prefers-color-scheme: dark)").matches ? DARK_THEME : LIGHT_THEME;
    };

    document.addEventListener("DOMContentLoaded", () => {
        const toggleButton = document.getElementById("darkThemeToggleButton");
        if (!toggleButton) {
            return;
        }

        const toggleIcon = toggleButton.querySelector(".dark-theme-toggle-icon");

        let currentTheme = getInitialTheme();
        applyTheme(currentTheme, toggleButton, toggleIcon);

        toggleButton.addEventListener("click", () => {
            currentTheme = currentTheme === DARK_THEME ? LIGHT_THEME : DARK_THEME;
            applyTheme(currentTheme, toggleButton, toggleIcon);
        });
    });
})();
