document.addEventListener("DOMContentLoaded", function () {
    const search = document.getElementById("studentSearch");
    const rows = Array.from(document.querySelectorAll("[data-student-row]"));
    const empty = document.getElementById("studentEmpty");
    const filters = Array.from(document.querySelectorAll("[data-student-filter]"));
    const alerts = document.querySelectorAll(".ciit-student-alert");

    let currentFilter = "active";

    function refresh() {
        const query = search?.value.trim().toLowerCase() || "";
        let visible = 0;

        rows.forEach(function (row) {
            const statusMatches =
                currentFilter === "all" ||
                row.dataset.studentStatus === currentFilter;

            const textMatches =
                !query || row.dataset.studentSearch.includes(query);

            row.hidden = !(statusMatches && textMatches);

            if (!row.hidden) {
                visible++;
            }
        });

        if (empty) {
            empty.hidden = visible !== 0;
        }
    }

    filters.forEach(function (button) {
        button.addEventListener("click", function () {
            filters.forEach(function (item) {
                item.classList.remove("active");
            });

            button.classList.add("active");
            currentFilter = button.dataset.studentFilter || "active";
            refresh();
        });
    });

    search?.addEventListener("input", refresh);

    document.querySelectorAll(".ciit-student-alert-close").forEach(function (button) {
        button.addEventListener("click", function () {
            button.closest(".ciit-student-alert")?.remove();
        });
    });

    alerts.forEach(function (alert) {
        window.setTimeout(function () {
            alert.classList.add("is-fading");
            window.setTimeout(function () {
                alert.remove();
            }, 650);
        }, 5000);
    });

    document.querySelectorAll(".js-student-status-form").forEach(function (form) {
        form.addEventListener("submit", function (event) {
            const name = form.dataset.studentName || "this student";
            const activating = form.querySelector("input[name='isActive']")?.value === "True";

            const message = activating
                ? "Activate " + name + "?"
                : "Deactivate " + name + "? They will not be able to sign in while inactive.";

            if (!window.confirm(message)) {
                event.preventDefault();
            }
        });
    });

    refresh();
});
