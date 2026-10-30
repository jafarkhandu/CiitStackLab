document.addEventListener("DOMContentLoaded", () => {
    const modal = document.getElementById("meetModal");
    const modalTitle = document.getElementById("meetModalTitle");
    const modalContent = document.getElementById("meetModalContent");

    const openModal = () => {
        modal?.classList.add("is-open");
        modal?.setAttribute("aria-hidden", "false");
        document.body.classList.add("meet-modal-open");
    };

    const closeModal = () => {
        modal?.classList.remove("is-open");
        modal?.setAttribute("aria-hidden", "true");
        document.body.classList.remove("meet-modal-open");
        if (modalContent) modalContent.replaceChildren();
    };

    const wireModalContent = () => {
        const search = modalContent?.querySelector("#meetStudentSearch");
        const picker = modalContent?.querySelector("#meetStudentPicker");

        if (search && picker) {
            search.addEventListener("input", () => {
                const query = search.value.trim().toLowerCase();
                picker.querySelectorAll("[data-search]").forEach((item) => {
                    item.hidden = query.length > 0 && !(item.dataset.search || "").includes(query);
                });
            });
        }

        const form = modalContent?.querySelector("form");
        form?.addEventListener("submit", async (event) => {
            event.preventDefault();

            const submit = form.querySelector('[type="submit"]');
            const feedback = form.querySelector(".meet-modal-feedback");
            if (submit) submit.disabled = true;

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: new FormData(form),
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                const result = await response.json();

                if (!response.ok || !result.success) {
                    throw new Error(result.message || "Unable to save this batch.");
                }

                window.location.reload();
            } catch (error) {
                if (feedback) {
                    feedback.textContent = error.message;
                    feedback.hidden = false;
                }

                if (submit) submit.disabled = false;
            }
        });
    };

    document.querySelector("[data-open-create-modal]")?.addEventListener("click", () => {
        modalTitle.textContent = "Create batch";
        modalContent.innerHTML = document.getElementById("meetCreateTemplate").innerHTML;
        openModal();
        wireModalContent();
    });

    document.addEventListener("click", async (event) => {
        const manageButton = event.target.closest("[data-open-manage-modal]");

        if (manageButton) {
            modalTitle.textContent = "Manage batch";
            modalContent.innerHTML = '<div class="meet-modal-loading">Loading batch details...</div>';
            openModal();

            try {
                const response = await fetch(manageButton.dataset.url, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (!response.ok) {
                    throw new Error("Unable to load batch details.");
                }

                modalContent.innerHTML = await response.text();
                wireModalContent();
            } catch (error) {
                modalContent.textContent = error.message;
            }
        }

        if (event.target.closest("[data-close-meet-modal]") || event.target === modal) {
            closeModal();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") closeModal();
    });

    document.querySelectorAll(".meet-alert-close").forEach((button) => {
        button.addEventListener("click", () => {
            button.closest(".meet-alert")?.remove();
        });
    });
});
