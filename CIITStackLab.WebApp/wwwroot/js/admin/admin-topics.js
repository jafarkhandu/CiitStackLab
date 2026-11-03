document.addEventListener("DOMContentLoaded", function () {
    const modal = document.getElementById("topicModal"),
        form = document.getElementById("topicForm"),
        idInput = document.getElementById("topicId"),
        nameInput = document.getElementById("topicName"),
        priceInput = document.getElementById("topicPrice"),
        durationInput = document.getElementById("topicDuration"),
        title = document.getElementById("topicModalLabel"),
        description = document.getElementById("topicModalDescription"),
        submit = document.getElementById("topicSubmitBtn"),
        logoPanel = document.getElementById("topicLogoPanel"),
        logoIcon = document.getElementById("topicLogoIcon"),
        logoName = document.getElementById("topicLogoName"),
        logoMeta = document.getElementById("topicLogoMeta"),
        logoConfirm = document.getElementById("topicLogoConfirm"),
        confirmedLogoClass = document.getElementById("confirmedLogoClass"),
        search = document.getElementById("topicSearch"),
        rows = Array.from(document.querySelectorAll("[data-topic-row]")),
        count = document.getElementById("topicVisibleCount"),
        empty = document.getElementById("topicSearchEmpty"),
        archiveToggle = document.getElementById("topicArchiveToggle"),
        archivePanel = document.getElementById("topicArchivePanel");

    let logoRequest = null;
    let logoRequestTimer = null;

    function setSubmitState(enabled) {
        if (submit) submit.disabled = !enabled;
    }

    function clearLogoConfirmation() {
        if (logoConfirm) logoConfirm.checked = false;
        if (confirmedLogoClass) confirmedLogoClass.value = "";
    }

    function resetLogoPreview() {
        clearLogoConfirmation();
        if (logoPanel) logoPanel.hidden = true;
        if (logoIcon) logoIcon.className = "bi bi-code-slash";
        if (logoName) logoName.textContent = "Detected logo";
        if (logoMeta) logoMeta.textContent = "Confirm this logo before saving.";
    }

    function reset() {
        form?.reset();
        if (idInput) idInput.value = "0";
        resetLogoPreview();
        setSubmitState(false);
    }

    function fill(topic) {
        if (!topic) return;

        idInput.value = topic.id ?? "0";
        nameInput.value = topic.topicName || "";
        priceInput.value = topic.price ?? "";
        durationInput.value = topic.durationHours ?? "";
        title.textContent = "Edit Topic";
        description.textContent = "Update the selected active topic.";
        submit.querySelector("span").textContent = "Update Topic";
        resetLogoPreview();
        setSubmitState(true);
    }

    async function previewLogo() {
        const name = nameInput?.value.trim() || "";
        clearLogoConfirmation();

        if (!name) {
            resetLogoPreview();
            setSubmitState(false);
            return;
        }

        if (logoPanel) logoPanel.hidden = false;
        if (logoIcon) logoIcon.className = "bi bi-hourglass-split";
        if (logoName) logoName.textContent = name;
        if (logoMeta) logoMeta.textContent = "Detecting topic logo...";
        setSubmitState(false);

        if (logoRequest) logoRequest.abort();

        logoRequest = new AbortController();
        try {
            const response = await fetch("/Admin/Topics/PreviewLogo?topicName=" + encodeURIComponent(name), {
                method: "GET",
                headers: { "Accept": "application/json" },
                signal: logoRequest.signal
            });

            if (!response.ok) throw new Error("Logo preview request failed.");

            const result = await response.json();
            if (!result.success || nameInput.value.trim() !== name) return;

            logoIcon.className = result.iconClass || "bi bi-code-slash";
            logoName.textContent = result.topicName || name;
            logoMeta.textContent = result.category
                ? "Detected logo · " + result.category
                : "Detected logo";
        } catch (error) {
            if (error.name === "AbortError") return;
            if (nameInput.value.trim() !== name) return;

            logoIcon.className = "bi bi-exclamation-circle";
            logoName.textContent = name;
            logoMeta.textContent = "Logo detection failed. Try again.";
        }
    }

    if (nameInput) {
        nameInput.addEventListener("input", function () {
            clearTimeout(logoRequestTimer);
            clearLogoConfirmation();
            setSubmitState(false);
            logoRequestTimer = setTimeout(previewLogo, 300);
        });
    }

    if (logoConfirm) {
        logoConfirm.addEventListener("change", function () {
            const confirmed = logoConfirm.checked;
            if (confirmed) {
                confirmedLogoClass.value = logoIcon?.className || "";
            } else {
                confirmedLogoClass.value = "";
            }
            setSubmitState(confirmed && Boolean(confirmedLogoClass.value));
        });
    }

    if (form) {
        form.addEventListener("submit", function (event) {
            if (idInput.value === "0" && (!logoConfirm.checked || !confirmedLogoClass.value)) {
                event.preventDefault();
                logoConfirm.focus();
            }
        });
    }

    if (modal) {
        modal.addEventListener("show.bs.modal", function (event) {
            const trigger = event.relatedTarget;
            const mode = trigger?.getAttribute("data-topic-mode");

            if (mode === "create") {
                reset();
                title.textContent = "Add Topic";
                description.textContent = "Create a new active learning topic.";
                submit.querySelector("span").textContent = "Save Topic";
            } else if (mode === "edit") {
                fill({
                    id: trigger.dataset.topicId,
                    topicName: trigger.dataset.topicName,
                    price: trigger.dataset.topicPrice,
                    durationHours: trigger.dataset.topicDurationHours
                });
            }
        });

        modal.addEventListener("hidden.bs.modal", function () {
            clearTimeout(logoRequestTimer);
            if (logoRequest) logoRequest.abort();
            resetLogoPreview();
        });
    }

    if (search) {
        search.addEventListener("input", function () {
            const term = search.value.trim().toLowerCase();
            let shown = 0;

            rows.forEach(row => {
                const match = !term || row.dataset.topicName.includes(term);
                row.hidden = !match;
                if (match) shown++;
            });

            count.textContent = shown.toLocaleString();
            empty.hidden = rows.length === 0 || shown !== 0;
        });
    }

    if (archiveToggle && archivePanel) {
        archiveToggle.addEventListener("click", function () {
            const open = archiveToggle.getAttribute("aria-expanded") === "true";
            archiveToggle.setAttribute("aria-expanded", open ? "false" : "true");
            archivePanel.hidden = open;
        });
    }

    document.querySelectorAll(".ciit-topic-alert-close").forEach(button =>
        button.addEventListener("click", () => button.closest(".ciit-topic-alert")?.remove()));

    document.querySelectorAll(".js-topic-delete-form").forEach(formElement => {
        formElement.addEventListener("submit", function (event) {
            const name = formElement.dataset.topicName || "this topic";
            if (!window.confirm("Archive " + name + "? You can restore it later."))
                event.preventDefault();
        });
    });
});
