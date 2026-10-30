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

    const configureMeetingModal = (batchId, batchName) => {
        const template = document.getElementById("meetSessionTemplate");
        if (!template || !modalContent) return;

        modalTitle.textContent = "Create meeting";
        modalContent.innerHTML = template.innerHTML;
        const form = modalContent.querySelector(".meet-session-modal-form");
        const batchIdInput = form?.querySelector("#modalSessionBatchId");
        const batchNameLabel = form?.querySelector("#modalSessionBatchName");
        const offsetInput = form?.querySelector("#modalClientUtcOffset");
        const startInput = form?.querySelector("#modalSessionStart");
        const scheduleField = form?.querySelector(".meet-schedule-field");
        const modeInput = form?.querySelector("#modalMeetingMode");
        const submitButton = form?.querySelector(".meet-session-submit");

        if (!form || !batchIdInput || !batchNameLabel || !offsetInput || !startInput || !scheduleField || !modeInput || !submitButton) return;

        batchIdInput.value = batchId;
        batchNameLabel.textContent = batchName;
        offsetInput.value = new Date().getTimezoneOffset().toString();

        const setMode = (mode) => {
            const scheduled = mode === "scheduled";
            modeInput.value = mode;
            scheduleField.hidden = !scheduled;
            startInput.disabled = !scheduled;
            startInput.required = scheduled;

            if (scheduled) {
                const now = new Date();
                now.setMinutes(now.getMinutes() + 2);
                now.setSeconds(0, 0);
                const localValue = new Date(now.getTime() - now.getTimezoneOffset() * 60000)
                    .toISOString()
                    .slice(0, 16);
                startInput.min = localValue;
            }

            form.querySelectorAll("[data-meeting-tab]").forEach((tab) => {
                const active = tab.dataset.meetingTab === mode;
                tab.classList.toggle("is-active", active);
                tab.setAttribute("aria-selected", active ? "true" : "false");
            });

            submitButton.innerHTML = scheduled
                ? '<i class="bi bi-calendar-check"></i> Schedule meeting'
                : '<i class="bi bi-camera-video-fill"></i> Start instant meeting';
        };

        form.querySelectorAll("[data-meeting-tab]").forEach((tab) => {
            tab.addEventListener("click", () => setMode(tab.dataset.meetingTab));
        });

        setMode("instant");
        form.addEventListener("submit", async event => {
            event.preventDefault();

            const feedback = form.querySelector(".meet-modal-feedback");
            submitButton.disabled = true;
            if (feedback) {
                feedback.hidden = true;
                feedback.textContent = "";
            }

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: new FormData(form),
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                const result = await response.json();

                if (!response.ok || !result.success) {
                    throw new Error(result.message || "Unable to create this meeting.");
                }

                closeModal();
            } catch (error) {
                if (feedback) {
                    feedback.textContent = error.message;
                    feedback.hidden = false;
                }
                submitButton.disabled = false;
            }
        });
        openModal();
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
        const meetingCard = event.target.closest("[data-open-meeting-modal]");
        const clickedControl = event.target.closest("a, button, form, input, select, textarea");

        if (meetingCard && !clickedControl) {
            configureMeetingModal(meetingCard.dataset.batchId, meetingCard.dataset.batchName);
            return;
        }

        const manageButton = event.target.closest("[data-open-manage-modal]");

        if (manageButton) {
            event.preventDefault();
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

        if ((event.key === "Enter" || event.key === " ") && event.target.matches("[data-open-meeting-modal]")) {
            event.preventDefault();
            configureMeetingModal(event.target.dataset.batchId, event.target.dataset.batchName);
        }
    });

    document.querySelectorAll(".meet-alert-close").forEach((button) => {
        button.addEventListener("click", () => {
            button.closest(".meet-alert")?.remove();
        });
    });
});
