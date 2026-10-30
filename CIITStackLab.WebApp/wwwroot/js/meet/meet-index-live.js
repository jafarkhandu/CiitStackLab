document.addEventListener("DOMContentLoaded", () => {
    const adminRoot = document.querySelector("[data-meet-admin-index]");
    const studentRoot = document.querySelector("[data-meet-student-index]");
    const root = adminRoot || studentRoot;
    if (!root || !window.signalR) return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(root.dataset.hubUrl)
        .withAutomaticReconnect()
        .build();

    const token = () => document.querySelector('[data-meet-antiforgery] input[name="__RequestVerificationToken"]')
        ?.value || document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    const addHidden = (form, name, value) => {
        const input = document.createElement("input");
        input.type = "hidden";
        input.name = name;
        input.value = String(value);
        form.append(input);
    };

    const updateStudentBatch = state => {
        const card = studentRoot.querySelector('[data-meet-batch-id="' + state.batchId + '"]');
        if (!card) return;

        let joinForm = card.querySelector(".ciit-student-meet-join-form");
        let waiting = card.querySelector(".ciit-student-meet-waiting");
        const isLive = state.status === "Live"
            && state.startedAtUtc
            && new Date(state.startedAtUtc).getTime() + state.durationMinutes * 60000 > Date.now();

        if (isLive) {
            if (!joinForm) {
                joinForm = document.createElement("form");
                joinForm.method = "post";
                joinForm.action = "/Student/Meet/Join";
                joinForm.className = "ciit-student-meet-join-form";
                addHidden(joinForm, "__RequestVerificationToken", token());
                addHidden(joinForm, "id", state.sessionId);
                const button = document.createElement("button");
                button.type = "submit";
                button.className = "ciit-student-meet-open ciit-student-meet-join";
                button.textContent = "Join Meet";
                joinForm.append(button);
                if (waiting) waiting.replaceWith(joinForm);
                else card.append(joinForm);
            } else {
                const id = joinForm.querySelector('input[name="id"]');
                if (id) id.value = state.sessionId;
            }
        } else if (joinForm) {
            joinForm.remove();
            if (!waiting) {
                waiting = document.createElement("div");
                waiting.className = "ciit-student-meet-waiting";
                waiting.innerHTML = '<i class="bi bi-hourglass-split"></i> Waiting for admin to start';
                card.append(waiting);
            }
        }

        let upcoming = card.querySelector(".ciit-student-meet-upcoming");
        if (state.status === "Scheduled") {
            if (!upcoming) {
                upcoming = document.createElement("div");
                upcoming.className = "ciit-student-meet-upcoming";
                upcoming.innerHTML = "<span>UPCOMING MEETING</span><strong></strong><small></small>";
                card.append(upcoming);
            }
            upcoming.querySelector("strong").textContent = state.title;
            upcoming.querySelector("small").textContent = new Date(state.startsAtUtc).toLocaleString();
        } else if (upcoming && (state.status === "Cancelled" || state.status === "Live")) {
            upcoming.remove();
        }
    };

    const updateAdminSession = state => {
        let list = adminRoot.querySelector("[data-meet-admin-session-list]");
        let card = list?.querySelector('[data-meet-session-id="' + state.sessionId + '"]');
        if (!card) {
            const empty = adminRoot.querySelector(".meet-session-list-empty");
            if (empty) empty.remove();
            const listSection = adminRoot.querySelector(".meet-session-list-section");
            if (!listSection) return;
            list = listSection.querySelector("[data-meet-admin-session-list]");
            if (!list) {
                list = document.createElement("div");
                list.className = "meet-session-list";
                list.dataset.meetAdminSessionList = "";
                listSection.append(list);
            }
            card = document.createElement("article");
            card.className = "meet-session-list-item";
            card.dataset.meetSessionId = state.sessionId;
            card.dataset.meetBatchId = state.batchId;
            const icon = document.createElement("div");
            icon.className = "meet-session-list-icon";
            icon.innerHTML = '<i class="bi bi-camera-video"></i>';
            const copy = document.createElement("div");
            copy.className = "meet-session-list-copy";
            const title = document.createElement("strong");
            title.textContent = state.title;
            copy.append(title);
            const status = document.createElement("span");
            status.className = "meet-session-status";
            status.dataset.meetSessionStatus = "";
            const link = document.createElement("a");
            link.className = "meet-secondary-button";
            link.href = "/Admin/Meet/Room/" + state.sessionId;
            link.textContent = "Details";
            card.append(icon, copy, status, link);
            list.prepend(card);
        }

        const status = card.querySelector("[data-meet-session-status]");
        if (status) status.textContent = state.status;

        card.querySelectorAll("[data-meet-session-action]").forEach(element => element.remove());
        if (state.status === "Scheduled") {
            [
                ["Start now", "/Admin/Meet/Start"],
                ["Cancel", "/Admin/Meet/Cancel"]
            ].forEach(([label, action]) => {
                const form = document.createElement("form");
                form.method = "post";
                form.action = action;
                form.dataset.meetSessionAction = "";
                form.dataset.meetAjax = "";
                addHidden(form, "__RequestVerificationToken", token());
                addHidden(form, "id", state.sessionId);
                const button = document.createElement("button");
                button.type = "submit";
                button.className = label === "Start now" ? "meet-primary-button" : "meet-secondary-button";
                button.textContent = label;
                form.append(button);
                card.append(form);
            });
        }

        const link = card.querySelector("a");
        if (link) {
            link.textContent = state.status === "Live" ? "Open live room" : "Details";
            link.className = state.status === "Live" ? "meet-primary-button" : "meet-secondary-button";
        }
    };

    if (adminRoot) {
        adminRoot.addEventListener("submit", async event => {
            const form = event.target.closest("form[data-meet-ajax]");
            if (!form) return;
            event.preventDefault();
            const button = form.querySelector('[type="submit"]');
            if (button) button.disabled = true;
            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: new FormData(form),
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                if (!response.ok) throw new Error("Unable to update this meeting.");
                if (button) button.disabled = false;
            } catch (error) {
                console.error(error);
                if (button) button.disabled = false;
            }
        });
    }

    const handleUpdate = state => {
        if (adminRoot) updateAdminSession(state);
        if (studentRoot) updateStudentBatch(state);
    };

    connection.on("MeetAdminUpdated", handleUpdate);
    connection.on("MeetBatchUpdated", handleUpdate);

    const joinGroup = () => connection.invoke(adminRoot ? "JoinAdminUpdates" : "JoinMyBatchUpdates");
    connection.onreconnected(joinGroup);
    connection.start().then(joinGroup).catch(error => console.error("Meet dashboard live connection failed:", error));
});
