document.addEventListener("DOMContentLoaded", function () {
    const modal = document.getElementById("topicModal"),
        form = document.getElementById("topicForm"),
        idInput = document.getElementById("topicId"),
        nameInput = document.getElementById("topicName"),
        folderInput = document.getElementById("publicFolderId"),
        priceInput = document.getElementById("topicPrice"),
        durationInput = document.getElementById("topicDuration"),
        title = document.getElementById("topicModalLabel"),
        description = document.getElementById("topicModalDescription"),
        submit = document.getElementById("topicSubmitBtn"),
        search = document.getElementById("topicSearch"),
        rows = Array.from(document.querySelectorAll("[data-topic-row]")),
        count = document.getElementById("topicVisibleCount"),
        empty = document.getElementById("topicSearchEmpty"),
        archiveToggle = document.getElementById("topicArchiveToggle"),
        archivePanel = document.getElementById("topicArchivePanel");

    function reset() {
        form?.reset();
        if (idInput) idInput.value = "0";
    }

    function fill() {
        const topic = window.ciitEditTopic;
        if (!topic) return;

        idInput.value = topic.id;
        nameInput.value = topic.topicName || "";
        folderInput.value = topic.publicFolderId || "";
        priceInput.value = topic.price ?? "";
        durationInput.value = topic.durationMinutes ?? "";
        title.textContent = "Edit Topic";
        description.textContent = "Update the selected active topic.";
        submit.querySelector("span").textContent = "Update Topic";
    }

    if (modal) {
        modal.addEventListener("show.bs.modal", function (event) {
            if (event.relatedTarget?.getAttribute("data-topic-mode") === "create") {
                reset();
                title.textContent = "Add Topic";
                description.textContent = "Create a new active learning topic.";
                submit.querySelector("span").textContent = "Save Topic";
            } else if (window.ciitEditTopic) {
                fill();
            }
        });

        if (window.ciitEditTopic)
            bootstrap.Modal.getOrCreateInstance(modal).show();
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
