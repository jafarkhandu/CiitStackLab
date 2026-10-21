document.addEventListener("DOMContentLoaded", function () {
    const tabs = Array.from(document.querySelectorAll("[data-settings-tab]"));
    const panels = Array.from(document.querySelectorAll("[data-settings-panel]"));
    const requestedSection = new URLSearchParams(window.location.search).get("section");

    function activate(section) {
        const target = section === "security" ? "security" : "profile";

        tabs.forEach(function (tab) {
            tab.classList.toggle("active", tab.dataset.settingsTab === target);
        });

        panels.forEach(function (panel) {
            panel.classList.toggle("active", panel.dataset.settingsPanel === target);
        });
    }

    tabs.forEach(function (tab) {
        tab.addEventListener("click", function () {
            const target = tab.dataset.settingsTab || "profile";
            activate(target);

            const url = new URL(window.location.href);
            url.searchParams.set("section", target);
            window.history.replaceState({}, "", url);
        });
    });

    document.querySelectorAll("[data-password-toggle]").forEach(function (button) {
        button.addEventListener("click", function () {
            const selector = button.dataset.passwordToggle;
            const input = selector ? document.querySelector(selector) : null;

            if (!input) {
                return;
            }

            const showing = input.type === "text";
            input.type = showing ? "password" : "text";

            const icon = button.querySelector("i");
            if (icon) {
                icon.className = showing ? "bi bi-eye" : "bi bi-eye-slash";
            }
        });
    });

    document.querySelectorAll(".ciit-settings-alert").forEach(function (alert) {
        window.setTimeout(function () {
            alert.classList.add("is-fading");

            window.setTimeout(function () {
                alert.remove();
            }, 650);
        }, 5000);
    });

    document.querySelectorAll(".ciit-settings-alert-close").forEach(function (button) {
        button.addEventListener("click", function () {
            button.closest(".ciit-settings-alert")?.remove();
        });
    });

    activate(requestedSection || "profile");
});
