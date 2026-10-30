document.addEventListener("DOMContentLoaded", () => {
    const search = document.getElementById("meetStudentSearch");
    const picker = document.getElementById("meetStudentPicker");

    if (search && picker) {
        search.addEventListener("input", () => {
            const query = search.value.trim().toLowerCase();

            picker.querySelectorAll("[data-search]").forEach((item) => {
                const searchableText = item.getAttribute("data-search") || "";
                item.hidden = query.length > 0 && !searchableText.includes(query);
            });
        });
    }

    document.querySelectorAll(".meet-alert-close").forEach((button) => {
        button.addEventListener("click", () => {
            button.closest(".meet-alert")?.remove();
        });
    });
});
