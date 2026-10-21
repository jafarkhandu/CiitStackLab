document.addEventListener("DOMContentLoaded", function () {
    const modalElement = document.getElementById("courseModal"),
        form = document.getElementById("courseForm"),
        idInput = document.getElementById("courseId"),
        nameInput = document.getElementById("courseName"),
        imageInput = document.getElementById("courseImage"),
        imagePreview = document.getElementById("courseImagePreview"),
        imagePreviewWrap = document.getElementById("courseImagePreviewWrap"),
        title = document.getElementById("courseModalLabel"),
        description = document.getElementById("courseModalDescription"),
        submit = document.getElementById("courseSubmitBtn"),
        search = document.getElementById("courseSearch"),
        rows = Array.from(document.querySelectorAll("[data-course-row]")),
        visibleCount = document.getElementById("courseVisibleCount"),
        searchEmpty = document.getElementById("courseSearchEmpty"),
        archiveToggle = document.getElementById("archiveToggle"),
        archivePanel = document.getElementById("archivePanel");

    function resetForm() {
        form?.reset();
        if (idInput) idInput.value = "0";
        if (imagePreview) imagePreview.removeAttribute("src");
        if (imagePreviewWrap) imagePreviewWrap.hidden = true;
    }

    function fillEdit() {
        const course = window.ciitEditCourse;
        if (!course) return;

        idInput.value = course.id;
        nameInput.value = course.courseName || "";
        if (course.imagePath && imagePreview && imagePreviewWrap) {
            imagePreview.src = course.imagePath;
            imagePreviewWrap.hidden = false;
        }
        title.textContent = "Edit Course";
        description.textContent = "Update the selected active course.";
        submit.querySelector("span").textContent = "Update Course";
    }

    if (modalElement) {
        modalElement.addEventListener("show.bs.modal", function (event) {
            const mode = event.relatedTarget?.getAttribute("data-course-mode");

            if (mode === "create") {
                resetForm();
                title.textContent = "Add Course";
                description.textContent = "Create a new active course.";
                submit.querySelector("span").textContent = "Save Course";
            } else if (window.ciitEditCourse) {
                fillEdit();
            }
        });

        if (window.ciitEditCourse)
            bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    if (imageInput && imagePreview && imagePreviewWrap) {
        imageInput.addEventListener("change", function () {
            const file = imageInput.files?.[0];
            if (!file) return;
            if (!["image/jpeg", "image/png", "image/webp"].includes(file.type) || file.size > 5 * 1024 * 1024) {
                imageInput.value = "";
                imagePreview.removeAttribute("src");
                imagePreviewWrap.hidden = true;
                window.alert("Choose a JPG, PNG, or WebP image smaller than 5 MB.");
                return;
            }
            imagePreview.src = URL.createObjectURL(file);
            imagePreviewWrap.hidden = false;
        });
    }

    if (search) {
        search.addEventListener("input", function () {
            const term = search.value.trim().toLowerCase();
            let count = 0;

            rows.forEach(function (row) {
                const match = !term || row.dataset.courseName.includes(term);
                row.hidden = !match;
                if (match) count++;
            });

            visibleCount.textContent = count.toLocaleString();
            searchEmpty.hidden = rows.length === 0 || count !== 0;
        });
    }

    if (archiveToggle && archivePanel) {
        archiveToggle.addEventListener("click", function () {
            const open = archiveToggle.getAttribute("aria-expanded") === "true";
            archiveToggle.setAttribute("aria-expanded", open ? "false" : "true");
            archivePanel.hidden = open;
        });
    }

    document.querySelectorAll(".ciit-course-alert-close").forEach(button =>
        button.addEventListener("click", () => button.closest(".ciit-course-alert")?.remove()));

    document.querySelectorAll(".js-course-delete-form").forEach(formElement => {
        formElement.addEventListener("submit", function (event) {
            const name = formElement.dataset.courseName || "this course";
            if (!window.confirm("Archive " + name + "? You can restore it later from Archived courses."))
                event.preventDefault();
        });
    });
});
