document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-room-toggle]").forEach((button) => {
        button.addEventListener("click", () => {
            const active = button.getAttribute("aria-pressed") !== "true";
            const type = button.dataset.roomToggle;
            button.setAttribute("aria-pressed", active ? "true" : "false");
            button.classList.toggle("is-active", active);

            const icon = button.querySelector("i");
            const label = button.querySelector("span");
            if (!icon || !label) return;

            const states = {
                mic: active
                    ? ["bi-mic-fill", "Mute"]
                    : ["bi-mic-mute", "Unmute"],
                camera: active
                    ? ["bi-camera-video-fill", "Stop video"]
                    : ["bi-camera-video-off", "Start video"],
                share: active
                    ? ["bi-display-fill", "Sharing (preview)"]
                    : ["bi-display", "Share screen"]
            };

            icon.className = "bi " + states[type][0];
            label.textContent = states[type][1];
        });
    });
});


document.addEventListener("DOMContentLoaded", () => {
    const room = document.querySelector("[data-meet-room]");
    if (!room) return;

    room.querySelectorAll("form[data-meet-ajax]").forEach(form => {
        form.addEventListener("submit", async event => {
            if (event.defaultPrevented) return;
            event.preventDefault();
            if (form.dataset.submitting === "true") return;

            const submit = form.querySelector('[type="submit"]');
            form.dataset.submitting = "true";
            if (submit) submit.disabled = true;

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: new FormData(form),
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                if (!response.ok) throw new Error("Unable to update the meeting.");
                if (submit && !submit.hasAttribute("data-meet-end-button")) submit.disabled = false;
            } catch (error) {
                console.error(error);
                if (submit) submit.disabled = false;
            } finally {
                form.dataset.submitting = "false";
            }
        });
    });

    const toggle = room.querySelector("[data-meet-controls-toggle]");
    const controlsBody = room.querySelector("[data-meet-controls-body]");
    if (toggle && controlsBody) {
        toggle.addEventListener("click", () => {
            const expanded = toggle.getAttribute("aria-expanded") === "true";
            toggle.setAttribute("aria-expanded", expanded ? "false" : "true");
            controlsBody.hidden = expanded;
            toggle.classList.toggle("is-collapsed", expanded);
        });
    }

    if (!window.signalR) {
        console.error("SignalR client library could not be loaded.");
        return;
    }

    const sessionId = Number(room.dataset.sessionId);
    const isHost = room.dataset.isHost === "true";
    const hubUrl = room.dataset.hubUrl;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    const setText = (selector, value) => {
        const element = room.querySelector(selector);
        if (element) element.textContent = String(value);
    };

    const getToken = () => room.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

    const postAction = async (url, values) => {
        const body = new URLSearchParams({
            ...values,
            __RequestVerificationToken: getToken()
        });
        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "RequestVerificationToken": getToken()
            },
            body
        });
        if (!response.ok) throw new Error("Meeting action failed.");
    };

    const addActionButton = (row, label, className, action) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = className;
        button.textContent = label;
        button.addEventListener("click", async () => {
            button.disabled = true;
            try {
                await action();
            } catch (error) {
                console.error(error);
                button.disabled = false;
            }
        });
        row.append(button);
    };

    const renderParticipants = (participants) => {
        if (!isHost) return;
        const list = room.querySelector("[data-meet-participants-list]");
        if (!list) return;

        list.querySelectorAll("[data-meet-participant-id], [data-meet-no-participants]")
            .forEach(element => element.remove());

        if (!participants.length) {
            const empty = document.createElement("p");
            empty.dataset.meetNoParticipants = "true";
            empty.textContent = "No student has requested to join yet.";
            list.append(empty);
        }

        participants.forEach(participant => {
            const row = document.createElement("article");
            row.className = "ciit-meet-host-participant";
            row.dataset.meetParticipantId = participant.id;
            row.dataset.meetParticipantStatus = participant.status;

            const identity = document.createElement("div");
            const name = document.createElement("strong");
            name.textContent = participant.studentName;
            const status = document.createElement("small");
            status.textContent = participant.status;
            identity.append(name, status);
            row.append(identity);

            if (participant.status === "Pending") {
                addActionButton(row, "Admit", "ciit-meet-host-secondary", () =>
                    postAction(list.dataset.admitUrl, { id: participant.id, decision: "admit" }));
                addActionButton(row, "Reject", "ciit-meet-host-danger", () =>
                    postAction(list.dataset.admitUrl, { id: participant.id, decision: "reject" }));
            } else if (participant.status === "Admitted") {
                const permissions = document.createElement("div");
                permissions.className = "ciit-meet-permission-form";
                const settings = [
                    ["canUseMicrophone", "Mic", participant.canUseMicrophone],
                    ["canUseCamera", "Camera", participant.canUseCamera],
                    ["canShareScreen", "Share", participant.canShareScreen]
                ];
                settings.forEach(([key, label, checked]) => {
                    const wrapper = document.createElement("label");
                    const input = document.createElement("input");
                    input.type = "checkbox";
                    input.checked = Boolean(checked);
                    input.dataset.permission = key;
                    wrapper.append(input, document.createTextNode(" " + label));
                    permissions.append(wrapper);
                });
                const save = document.createElement("button");
                save.type = "button";
                save.className = "ciit-meet-host-secondary";
                save.textContent = "Save permissions";
                save.addEventListener("click", async () => {
                    save.disabled = true;
                    const values = { id: participant.id };
                    permissions.querySelectorAll("[data-permission]").forEach(input => {
                        values[input.dataset.permission] = input.checked;
                    });
                    try {
                        await postAction(list.dataset.permissionsUrl, values);
                    } catch (error) {
                        console.error(error);
                        save.disabled = false;
                    }
                });
                permissions.append(save);
                row.append(permissions);
                addActionButton(row, "Remove", "ciit-meet-host-danger", () =>
                    postAction(list.dataset.removeUrl, { id: participant.id }));
            }

            list.append(row);
        });
        setText("[data-meet-participant-total]", participants.length);
    };

    const updateRoom = (state) => {
        if (state.sessionId !== sessionId) return;

        setText("[data-meet-status-text]", state.status);
        setText("[data-meet-waiting-room-state]", state.waitingRoomEnabled ? "On" : "Off");
        setText("[data-meet-recording-status]", state.recordingStatus);
        const waitingRoomForm = room.querySelector("[data-meet-waiting-room-form]");
        if (waitingRoomForm) {
            const enabledInput = waitingRoomForm.querySelector('input[name="enabled"]');
            const button = waitingRoomForm.querySelector('[type="submit"]');
            if (enabledInput) enabledInput.value = String(!state.waitingRoomEnabled);
            if (button) button.textContent = state.waitingRoomEnabled ? "Turn off" : "Turn on";
        }
        const recordingControls = room.querySelector("[data-meet-recording-controls]");
        if (recordingControls) {
            const actions = {
                enable: !state.recordingEnabled,
                stop: state.recordingStatus === "Recording",
                start: state.recordingEnabled && state.status === "Live"
                    && (state.recordingStatus === "Ready" || state.recordingStatus === "Stopped"),
                disable: state.recordingEnabled && state.recordingStatus !== "Recording",
                delete: state.recordingStatus === "Stopped"
            };
            Object.entries(actions).forEach(([action, visible]) => {
                const form = recordingControls.querySelector('[data-recording-action="' + action + '"]');
                if (form) form.hidden = !visible;
            });
        }
        setText("[data-meet-pending-count]", state.pendingCount);
        setText("[data-meet-admitted-count]", state.participantCount + (isHost && state.status === "Live" ? 1 : 0));

        const badge = room.querySelector("[data-meet-pending-count]");
        if (badge) badge.classList.toggle("has-pending", state.pendingCount > 0);

        const endButton = room.querySelector("[data-meet-end-button]");
        if (endButton) endButton.disabled = state.status !== "Live";

        const waitingScreen = room.querySelector("[data-meet-waiting-screen]");
        const waitingTitle = room.querySelector("[data-meet-waiting-title]");
        const waitingMessage = room.querySelector("[data-meet-waiting-message]");
        const rejectedScreen = room.querySelector("[data-meet-rejected-screen]");
        const stage = room.querySelector("[data-meet-room-stage]");
        const endedScreen = room.querySelector("[data-meet-ended-screen]");

        if (!isHost) {
            const selfStatus = room.dataset.participantStatus;
            if (state.status !== "Live" && state.status !== "Scheduled") {
                if (stage) stage.hidden = true;
                if (waitingScreen) waitingScreen.hidden = true;
                if (endedScreen) endedScreen.hidden = false;
            } else if (selfStatus === "Admitted") {
                if (stage) stage.hidden = false;
                if (waitingScreen) waitingScreen.hidden = true;
                if (rejectedScreen) rejectedScreen.hidden = true;
            } else if (selfStatus === "Rejected" || selfStatus === "Removed") {
                if (stage) stage.hidden = true;
                if (waitingScreen) waitingScreen.hidden = false;
                if (waitingTitle) waitingTitle.textContent = selfStatus === "Removed"
                    ? "You were removed from this meeting"
                    : "Join request was declined";
                if (waitingMessage) waitingMessage.textContent = "Please contact your admin if you need access.";
            } else if (selfStatus === "Pending") {
                if (stage) stage.hidden = true;
                if (waitingScreen) waitingScreen.hidden = false;
            }
        } else if (state.status !== "Live" && endedScreen) {
            endedScreen.hidden = false;
            if (stage) stage.hidden = true;
        }

        renderParticipants(state.participants || []);
    };

    connection.on("MeetRoomUpdated", updateRoom);
    connection.on("MeetMyParticipantStatus", update => {
        if (update.sessionId !== sessionId) return;
        room.dataset.participantStatus = update.status;
        if (update.sessionStatus !== "Live" && update.sessionStatus !== "Scheduled") {
            const stage = room.querySelector("[data-meet-room-stage]");
            const waiting = room.querySelector("[data-meet-waiting-screen]");
            const ended = room.querySelector("[data-meet-ended-screen]");
            if (stage) stage.hidden = true;
            if (waiting) waiting.hidden = true;
            if (ended) ended.hidden = false;
            return;
        }
        if (update.status === "Admitted") {
            const stage = room.querySelector("[data-meet-room-stage]");
            const waiting = room.querySelector("[data-meet-waiting-screen]");
            const rejected = room.querySelector("[data-meet-rejected-screen]");
            if (stage) stage.hidden = false;
            if (waiting) waiting.hidden = true;
            if (rejected) rejected.hidden = true;
        } else if (update.status === "Rejected" || update.status === "Removed") {
            const stage = room.querySelector("[data-meet-room-stage]");
            const waiting = room.querySelector("[data-meet-waiting-screen]");
            const title = room.querySelector("[data-meet-waiting-title]");
            const message = room.querySelector("[data-meet-waiting-message]");
            if (stage) stage.hidden = true;
            if (waiting) waiting.hidden = false;
            if (title) title.textContent = update.status === "Removed" ? "You were removed from this meeting" : "Join request was declined";
            if (message) message.textContent = "Please contact your admin if you need access.";
        }
    });
    connection.onreconnected(async () => {
        await connection.invoke("JoinMeetingRoom", sessionId);
    });

    connection.start()
        .then(() => connection.invoke("JoinMeetingRoom", sessionId))
        .catch(error => console.error("Meet live connection failed:", error));
});
