document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("[data-meet-student-index]");
    if (!root || !window.signalR) return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(root.dataset.hubUrl)
        .withAutomaticReconnect()
        .build();

    const token = () => document.querySelector('[data-meet-antiforgery] input[name="__RequestVerificationToken"]')
        ?.value || document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    const refreshBatchAction = state => {
        const card = root.querySelector('[data-meet-batch-id="' + state.batchId + '"]');
        if (!card) return;

        const isLive = state.status === "Live"
            && state.startedAtUtc
            && new Date(state.startedAtUtc).getTime() + Number(state.durationMinutes) * 60000 > Date.now();

        let form = card.querySelector(".ciit-student-meet-join-form");
        const waiting = card.querySelector(".ciit-student-meet-waiting");

        if (isLive) {
            if (!form) {
                form = document.createElement("form");
                form.method = "post";
                form.action = "/Student/Meet/Join";
                form.className = "ciit-student-meet-join-form";

                const csrf = document.createElement("input");
                csrf.type = "hidden";
                csrf.name = "__RequestVerificationToken";
                csrf.value = token();

                const id = document.createElement("input");
                id.type = "hidden";
                id.name = "id";
                id.value = String(state.sessionId);

                const button = document.createElement("button");
                button.type = "submit";
                button.className = "ciit-student-meet-open ciit-student-meet-join";
                button.innerHTML = '<i class="bi bi-camera-video-fill"></i> Join Meet <i class="bi bi-arrow-up-right"></i>';

                form.append(csrf, id, button);
                if (waiting) waiting.replaceWith(form);
                else card.append(form);
            } else {
                const id = form.querySelector('input[name="id"]');
                if (id) id.value = String(state.sessionId);
                if (waiting) waiting.remove();
            }
        } else if (state.status === "Completed" || state.status === "Cancelled") {
            if (form) form.remove();
            if (!waiting) {
                const placeholder = document.createElement("div");
                placeholder.className = "ciit-student-meet-waiting";
                placeholder.innerHTML = '<i class="bi bi-hourglass-split"></i> Waiting for admin to start';
                card.append(placeholder);
            }
        }
    };

    // Reconcile only the batch action controls against the server-rendered page.
    // This uses the same view-model logic as a manual refresh without reloading
    // the student's page or disturbing the current scroll position.
    const refreshActionsFromServer = async () => {
        const response = await fetch(window.location.pathname + window.location.search, {
            method: "GET",
            cache: "no-store",
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) {
            throw new Error("Unable to refresh live Meet actions (" + response.status + ").");
        }

        const html = await response.text();
        const parsed = new DOMParser().parseFromString(html, "text/html");
        const sourceRoot = parsed.querySelector("[data-meet-student-index]");
        if (!sourceRoot) throw new Error("Student Meet dashboard was not found in the refresh response.");

        root.querySelectorAll("[data-meet-batch-id]").forEach(card => {
            const batchId = card.dataset.meetBatchId;
            const sourceCard = sourceRoot.querySelector('[data-meet-batch-id="' + batchId + '"]');
            if (!sourceCard) return;

            const sourceAction = sourceCard.querySelector(".ciit-student-meet-join-form, .ciit-student-meet-waiting");
            const currentAction = card.querySelector(".ciit-student-meet-join-form, .ciit-student-meet-waiting");
            if (!sourceAction) {
                currentAction?.remove();
                return;
            }

            const actionCopy = sourceAction.cloneNode(true);
            if (currentAction) {
                currentAction.replaceWith(actionCopy);
            } else {
                const meta = card.querySelector(".ciit-student-meet-card-meta");
                if (meta) meta.insertAdjacentElement("afterend", actionCopy);
                else card.append(actionCopy);
            }
        });
    };

    connection.on("MeetBatchUpdated", refreshBatchAction);
    connection.on("MeetToast", notification => {
        if (notification?.title === "Meeting is live") {
            // Query the current hub snapshot and server-rendered card state.
            // The latter is authoritative for the Join form and antiforgery token.
            connection.invoke("JoinMyBatchUpdates")
                .catch(error => console.error("Meet live hub snapshot refresh failed:", error));

            refreshActionsFromServer()
                .catch(error => console.error("Meet live card action refresh failed:", error));
        }
    });

    const join = () => connection.invoke("JoinMyBatchUpdates");
    connection.onreconnected(join);
    connection.start().then(join)
        .catch(error => console.error("Meet live action recovery connection failed:", error));
});
