document.addEventListener("DOMContentLoaded", function () {
    const shell = document.getElementById("ciitAdminShell");
    const toggle = document.getElementById("ciitAdminMenuToggle");
    const overlay = document.getElementById("ciitAdminOverlay");

    if (shell && toggle && overlay) {
        const closeMenu = function () {
            shell.classList.remove("sidebar-open");
            overlay.classList.remove("is-visible");
            toggle.setAttribute("aria-expanded", "false");
        };

        toggle.addEventListener("click", function () {
            const open = shell.classList.toggle("sidebar-open");
            overlay.classList.toggle("is-visible", open);
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        });

        overlay.addEventListener("click", closeMenu);

        window.addEventListener("resize", function () {
            if (window.innerWidth > 992) {
                closeMenu();
            }
        });
    }

    document.querySelectorAll("[data-count]").forEach(function (element) {
        const target = Number(element.getAttribute("data-count"));
        if (!Number.isFinite(target) || target === 0) {
            return;
        }

        const duration = 550;
        const start = performance.now();

        const tick = function (now) {
            const progress = Math.min((now - start) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            element.textContent = Math.round(target * eased).toLocaleString();

            if (progress < 1) {
                requestAnimationFrame(tick);
            }
        };

        requestAnimationFrame(tick);
    });
});
