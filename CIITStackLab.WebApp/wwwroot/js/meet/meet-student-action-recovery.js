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

    connection.on("MeetBatchUpdated", refreshBatchAction);
    connection.on("MeetToast", notification => {
        if (notification?.title === "Meeting is live") {
            // Re-fetch the server's current batch/session snapshot in case the
            // toast arrived but the corresponding card update was missed.
            connection.invoke("JoinMyBatchUpdates")
                .catch(error => console.error("Meet live action refresh failed:", error));
        }
    });

    const join = () => connection.invoke("JoinMyBatchUpdates");
    connection.onreconnected(join);
    connection.start().then(join)
        .catch(error => console.error("Meet live action recovery connection failed:", error));
});
