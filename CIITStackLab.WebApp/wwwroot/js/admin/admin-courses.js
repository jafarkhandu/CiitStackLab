document.addEventListener("DOMContentLoaded", function () {
    const modalElement = document.getElementById("courseModal"),
        form = document.getElementById("courseForm"),
        idInput = document.getElementById("courseId"),
        nameInput = document.getElementById("courseName"),
        imageInput = document.getElementById("courseImage"),
        imagePreview = document.getElementById("courseImagePreview"),
        imagePreviewWrap = document.getElementById("courseImagePreviewWrap"),
        discountModeInput = document.getElementById("discountMode"),
        discountValueInput = document.getElementById("discountValue"),
        discountValueField = document.getElementById("discountValueField"),
        discountValueLabel = document.getElementById("discountValueLabel"),
        discountValueHelp = document.getElementById("discountValueHelp"),
        discountPreview = document.getElementById("discountPreview"),
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
        if (discountModeInput) discountModeInput.value = "";
        if (discountValueInput) discountValueInput.value = "";
        updateDiscountUi();
    }

    function fillEdit() {
        const course = window.ciitEditCourse;
        if (!course) return;

        idInput.value = course.id;
        nameInput.value = course.courseName || "";
        if (discountModeInput) discountModeInput.value = course.discountMode || "";
        if (discountValueInput) discountValueInput.value = course.discountValue ?? "";
        updateDiscountUi();
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

    function updateDiscountUi() {
        if (!discountModeInput || !discountValueInput || !discountValueField) return;
        const mode = discountModeInput.value;
        discountValueField.hidden = !mode;
        discountValueInput.disabled = !mode;
        discountValueInput.required = !!mode;
        discountValueInput.max = mode === "percentage" ? "100" : "";
        discountValueInput.placeholder = mode === "percentage" ? "e.g. 20" : "e.g. 1499";
        if (discountValueLabel) discountValueLabel.textContent = mode === "percentage" ? "Discount percentage (%)" : "Discounted price (₹)";
        if (discountValueHelp) discountValueHelp.textContent = mode === "percentage" ? "Enter 0–100%. Calculated from the current course price." : "Enter the final price students should pay. It cannot exceed the original price.";
        if (!discountPreview) return;
        const selected = Number(discountValueInput.value);
        const activeRow = rows.find(row => row.dataset.courseName === (nameInput?.value || "").trim().toLowerCase());
        const original = Number(activeRow?.dataset.coursePrice || 0);
        if (!mode || !discountValueInput.value || !Number.isFinite(selected) || selected < 0) discountPreview.textContent = "";
        else if (mode === "percentage" && selected > 100) discountPreview.textContent = "Percentage must be between 0 and 100.";
        else if (mode === "direct" && selected > original && original > 0) discountPreview.textContent = "Discounted price cannot exceed ₹" + original.toLocaleString("en-IN", { maximumFractionDigits: 2 });
        else if (original <= 0) {
            discountPreview.textContent = "Error: a discount cannot be applied while the course total price is ₹0.00.";
            discountPreview.classList.add("is-error");
        } else {
            discountPreview.classList.remove("is-error");
        }
        else {
            const finalPrice = mode === "percentage" ? original * (1 - selected / 100) : selected;
            discountPreview.textContent = "Student pays ₹" + finalPrice.toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " · saves ₹" + (original - finalPrice).toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }
    }

    discountModeInput?.addEventListener("change", function () {
        if (discountValueInput) discountValueInput.value = "";
        updateDiscountUi();
    });
    discountValueInput?.addEventListener("input", updateDiscountUi);
    nameInput?.addEventListener("input", updateDiscountUi);

    form?.addEventListener("submit", function (event) {
        if (discountModeInput?.value && discountValueInput?.value) {
            const activeRow = rows.find(row => row.dataset.courseName === (nameInput?.value || "").trim().toLowerCase());
            const original = Number(activeRow?.dataset.coursePrice || 0);
            if (original <= 0) {
                event.preventDefault();
                if (discountPreview) {
                    discountPreview.textContent = "Error: a discount cannot be applied while the course total price is ₹0.00. Assign active topics with a price first.";
                    discountPreview.setAttribute("role", "alert");
                }
                discountValueInput.focus();
            }
        }
    });

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
            archivePanel.setAttribute("aria-hidden", open ? "true" : "false");
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
