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
                    ? ["bi-display-fill", "Starting screen share…"]
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
                const formData = new FormData(form);
                const response = await fetch(form.getAttribute('action') || location.href, {
                    method: "POST",
                    body: formData,
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                if (!response.ok) {
                    const detail = await response.text().catch(() => "");
                    throw new Error(detail || "Unable to update the meeting.");
                }

                const endingMeeting = submit?.hasAttribute("data-meet-end-button") === true;
                if (room.dataset.isHost === "true"
                    && window.ciitMeetSignalRConnection?.state === "Connected") {
                    await window.ciitMeetSignalRConnection.invoke("JoinMeetingRoom", Number(room.dataset.sessionId));
                }

                if (endingMeeting) {
                    // Release camera, microphone, and screen capture as soon as the
                    // server confirms the meeting-end action.
                    window.ciitMeetWebRtcCleanup?.();
                    // Close the settings dialog immediately after the server confirms
                    // the end action; don't leave a stale controls card over the ended screen.
                    participantsPopup?.hide();
                    participantsPopup?.hide();
                    const endedScreen = room.querySelector("[data-meet-ended-screen]");
                    const stage = room.querySelector("[data-meet-room-stage]");
                    if (stage) stage.hidden = true;
                    if (endedScreen) endedScreen.hidden = false;
                } else {
                    if (submit) submit.disabled = false;
                    window.showMeetToast?.({ title: "Meeting updated", message: "The change was saved successfully.", type: "success" });
                }
            } catch (error) {
                console.error(error);
                window.showMeetToast?.({ title: "Action failed", message: error.message || "Unable to update the meeting.", type: "error" });
                if (submit) submit.disabled = false;
            } finally {
                form.dataset.submitting = "false";
            }
        });
    });

    const hostPanel = room.querySelector("[data-meet-controls-panel]");
    const isHost = room.dataset.isHost === "true";
    const createPopup = (className, title, icon) => {
        const backdrop = document.createElement("div");
        backdrop.className = "ciit-meet-glass-backdrop " + className;
        backdrop.hidden = true;
        const panel = document.createElement("section");
        panel.className = "ciit-meet-glass-popup";
        panel.setAttribute("role", "dialog");
        panel.setAttribute("aria-modal", "true");
        const heading = document.createElement("header");
        heading.className = "ciit-meet-glass-popup-heading";
        const titleNode = document.createElement("h2");
        titleNode.innerHTML = '<i class="bi ' + icon + '"></i> ' + title;
        const close = document.createElement("button");
        close.type = "button";
        close.className = "ciit-meet-glass-close";
        close.setAttribute("aria-label", "Close");
        close.innerHTML = "&times;";
        heading.append(titleNode, close);
        const content = document.createElement("div");
        content.className = "ciit-meet-glass-popup-content";
        panel.append(heading, content);
        backdrop.append(panel);
        document.body.append(backdrop);
        const hide = () => { backdrop.hidden = true; };
        close.addEventListener("click", hide);
        backdrop.addEventListener("click", event => { if (event.target === backdrop) hide(); });
        return { backdrop, content, hide };
    };

        let participantsPopup;
    let pendingRequestsList;
    if (isHost && hostPanel) {
                const participantsButton = room.querySelector("[data-open-participants]");
        const settings = hostPanel.querySelector(".ciit-meet-host-settings");
        const participants = hostPanel.querySelector("[data-meet-participants-list]");
        const panelHeading = hostPanel.querySelector(".ciit-meet-host-panel-heading");

                participantsPopup = createPopup("ciit-meet-participants-backdrop", "Participants", "bi-people");
        if (panelHeading) {
            panelHeading.querySelector("[data-meet-controls-toggle]")?.remove();
            panelHeading.remove();
        }
        
        if (participants) {
            participantsPopup.content.append(participants);
            pendingRequestsList = document.createElement("section");
            pendingRequestsList.className = "ciit-meet-pending-requests";
            pendingRequestsList.innerHTML = '<h3>Join requests <span data-meet-pending-total>0</span></h3><div data-meet-pending-list></div>';
            participantsPopup.content.append(pendingRequestsList);

            const admittedRoster = document.createElement("section");
            admittedRoster.className = "ciit-meet-admitted-roster";
            admittedRoster.innerHTML = '<h3>Admitted participants</h3><div data-meet-admitted-list></div>';
            const oldHeading = participants.querySelector("h2");
            const admittedHeading = admittedRoster.querySelector("h3");
            if (oldHeading) {
                const count = oldHeading.querySelector("span");
                if (count) count.dataset.meetParticipantTotal = "true";
                admittedHeading.append(count || document.createTextNode(" "));
                oldHeading.replaceWith(admittedHeading);
            }
            participants.querySelector("[data-meet-no-participants]")?.remove();

            const roster = admittedRoster.querySelector("[data-meet-admitted-list]");
            participants.querySelectorAll("[data-meet-participant-id]").forEach(row => {
                if (row.dataset.meetParticipantStatus === "Pending") {
                    pendingRequestsList.querySelector("[data-meet-pending-list]").append(row);
                } else if (row.dataset.meetParticipantStatus === "Admitted") {
                    roster.append(row);
                } else {
                    row.remove();
                }
            });
            participants.append(admittedRoster);
        }
        hostPanel.hidden = true;
                participantsButton?.addEventListener("click", () => { participantsPopup.backdrop.hidden = false; });
        document.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                participantsPopup.backdrop.hidden = true;
            }
        });
    }

    if (!window.signalR) {
        console.error("SignalR client library could not be loaded.");
        return;
    }

    const endMenuToggle = room.querySelector("[data-meet-end-toggle]");
    const endMenuForm = room.querySelector("[data-meet-end-form]");
    endMenuToggle?.addEventListener("click", () => {
        const isExpanded = endMenuToggle.getAttribute("aria-expanded") === "true";
        endMenuToggle.setAttribute("aria-expanded", String(!isExpanded));
        if (endMenuForm) endMenuForm.hidden = isExpanded;
        endMenuToggle.setAttribute("aria-label", isExpanded ? "Show end meeting option" : "Hide end meeting option");
    });

    const sessionId = Number(room.dataset.sessionId);
    // The pending-count badge is removed when the host panel is moved into
    // the participants popup. Keep a persistent baseline so room snapshots
    // cannot repeatedly announce the same waiting students.
    let lastPendingCount = Number(room.querySelector("[data-meet-pending-count]")?.textContent
        || room.querySelectorAll('[data-meet-participant-status="Pending"]').length
        || 0);
    const hubUrl = room.dataset.hubUrl;
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    const setText = (selector, value) => {
        const element = document.querySelector(selector);
        if (element) element.textContent = String(value);
    };

    const getToken = () => {
        const tokenInput = Array.from(document.querySelectorAll('input[name="__RequestVerificationToken"]'))
            .find(input => typeof input.value === "string" && input.value.trim().length > 0);
        return tokenInput?.value.trim() || "";
    };

    const postAction = async (url, values) => {
        const token = getToken();
        if (!token) {
            throw new Error("Security token is missing. Refresh the meeting page and try again.");
        }

        const body = new URLSearchParams({
            ...values,
            __RequestVerificationToken: token
        });
        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "RequestVerificationToken": token,
                "X-Requested-With": "XMLHttpRequest"
            },
            body
        });
        if (!response.ok) {
            const detail = await response.text().catch(() => "");
            let message = detail;
            try {
                const payload = JSON.parse(detail);
                message = payload.message || payload.title || detail;
            } catch {
                message = detail;
            }
            message = message.replace(/<[^>]*>/g, " ").replace(/\\s+/g, " ").trim();
            throw new Error(message || "Meeting action failed (" + response.status + ").");
        }

        // Re-request the authoritative server snapshot after roster actions.
        // This keeps the admitted list, pending requests, counts and permissions
        // in sync even when a redirected MVC response contains no JSON payload.
        if (connection.state === signalR.HubConnectionState.Connected) {
            await connection.invoke("JoinMeetingRoom", sessionId);
        }
    };

    const addActionButton = (row, label, className, action) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = className;
        button.textContent = label;
        button.addEventListener("click", async () => {
            if (label === "Remove" && !window.confirm("Remove this participant from the meeting?")) {
                return;
            }

            button.disabled = true;
            try {
                await action();
                // Admit/reject already produce one authoritative server-side
                // notification through MeetNotificationService. Avoid a second
                // local success toast for the same action.
                if (label === "Remove") {
                    showMeetToast({
                        title: "Participant removed",
                        message: "The participant was removed from the meeting.",
                        type: "success"
                    });
                }
            } catch (error) {
                console.error(error);

                // An action can race with meeting completion. Refresh the
                // authoritative participant roster to clear stale requests.
                if (connection.state === signalR.HubConnectionState.Connected) {
                    try {
                        await connection.invoke("JoinMeetingRoom", sessionId);
                    } catch (refreshError) {
                        console.warn("Participant roster refresh failed:", refreshError);
                    }
                }

                showMeetToast({
                    title: "Participant action failed",
                    message: error.message || "The action could not be completed. Please retry.",
                    type: "error"
                });
                button.disabled = false;
            }
        });
        row.append(button);
    };

    const renderParticipants = (participants) => {
        if (!isHost) return;
        const list = document.querySelector("[data-meet-participants-list]");
        if (!list) return;
        const admittedList = list.querySelector("[data-meet-admitted-list]");
        const targetList = admittedList || list;

        list.querySelectorAll("[data-meet-no-participants]").forEach(element => element.remove());
        targetList.querySelectorAll("[data-meet-participant-id]")
            .forEach(element => element.remove());
        const admittedParticipants = participants.filter(item => item.status === "Admitted");

        if (!admittedParticipants.length) {
            const empty = document.createElement("p");
            empty.dataset.meetNoParticipants = "true";
            empty.textContent = "No admitted participants yet.";
            list.append(empty);
        }

        admittedParticipants.forEach(participant => {
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

            if (participant.status === "Admitted") {
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

            targetList.append(row);
        });
        setText("[data-meet-participant-total]", admittedParticipants.length);
    };

    const renderPendingRequests = participants => {
        if (!isHost || !pendingRequestsList) return;
        const list = pendingRequestsList.querySelector("[data-meet-pending-list]");
        list.querySelectorAll("[data-meet-participant-id], [data-meet-no-requests]").forEach(item => item.remove());
        const pending = participants.filter(item => item.status === "Pending");
        setText("[data-meet-pending-total]", pending.length);
        if (!pending.length) {
            const empty = document.createElement("p");
            empty.dataset.meetNoRequests = "true";
            empty.textContent = "No pending join requests.";
            list.append(empty);
            return;
        }
        list.querySelector("[data-meet-no-requests]")?.remove();
        pending.forEach(participant => {
            const row = document.createElement("article");
            row.className = "ciit-meet-host-participant";
            row.dataset.meetParticipantId = participant.id;
            row.dataset.meetParticipantStatus = participant.status;
            const identity = document.createElement("div");
            const name = document.createElement("strong");
            name.textContent = participant.studentName;
            const status = document.createElement("small");
            status.textContent = "Waiting for approval";
            identity.append(name, status);
            row.append(identity);
            addActionButton(row, "Admit", "ciit-meet-host-secondary", () =>
                postAction(document.querySelector("[data-meet-participants-list]").dataset.admitUrl, { id: participant.id, decision: "admit" }));
            addActionButton(row, "Reject", "ciit-meet-host-danger", () =>
                postAction(document.querySelector("[data-meet-participants-list]").dataset.admitUrl, { id: participant.id, decision: "reject" }));
            list.append(row);
        });
    };

    const updateRoom = (state) => {
        if (state.sessionId !== sessionId) return;
        if (state.status !== "Live" && state.status !== "Scheduled") {
            window.ciitMeetWebRtcCleanup?.();
        }

        const previousPendingCount = lastPendingCount;
        const nextPendingCount = Number(state.pendingCount || 0);
        if (isHost && nextPendingCount > previousPendingCount) {
            const addedRequests = nextPendingCount - previousPendingCount;
            showMeetToast({
                title: addedRequests === 1 ? "New join request" : "New join requests",
                message: addedRequests === 1
                    ? "A student is waiting for admission."
                    : addedRequests + " students are waiting for admission.",
                type: "info"
            });
        }

        setText("[data-meet-status-text]", state.status);
        setText("[data-meet-waiting-room-state]", state.waitingRoomEnabled ? "On" : "Off");

        const waitingRoomForm = document.querySelector("[data-meet-waiting-room-form]");
        if (waitingRoomForm) {
            const enabledInput = waitingRoomForm.querySelector('input[name="enabled"]');
            const button = waitingRoomForm.querySelector('[type="submit"]');
            if (enabledInput) enabledInput.value = String(!state.waitingRoomEnabled);
            if (button) button.textContent = state.waitingRoomEnabled ? "Turn off" : "Turn on";
        }

        lastPendingCount = nextPendingCount;
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
        renderPendingRequests(state.participants || []);
        (state.participants || []).forEach(participant => {
            const row = document.querySelector('[data-meet-participant-id="' + participant.id + '"]');
            if (row) row.dataset.meetParticipantStatus = participant.status;
        });
    };

    const showMeetToast = notification => {
        const containerId = "ciit-meet-toast-container";
        let container = document.getElementById(containerId);
        if (!container) {
            container = document.createElement("div");
            container.id = containerId;
            container.setAttribute("aria-live", "polite");
            container.setAttribute("aria-atomic", "false");
            container.style.cssText = "position:fixed;top:20px;right:20px;z-index:10000;display:flex;flex-direction:column;gap:10px;width:min(360px,calc(100vw - 32px));";
            document.body.append(container);
        }

        const toast = document.createElement("div");
        const type = ["success", "warning", "error"].includes(notification?.type) ? notification.type : "info";
        toast.style.cssText = "background:rgba(20,25,38,.94);color:#fff;border:1px solid rgba(255,255,255,.18);border-left:4px solid " +
            (type === "success" ? "#38bdf8" : type === "warning" ? "#fbbf24" : type === "error" ? "#fb7185" : "#818cf8") +
            ";border-radius:12px;padding:12px 14px;box-shadow:0 12px 32px rgba(0,0,0,.24);backdrop-filter:blur(12px);font:500 13px/1.45 system-ui,sans-serif;animation:ciitMeetToastIn .2s ease-out;";
        const heading = document.createElement("strong");
        heading.textContent = notification?.title || "Meet notification";
        heading.style.cssText = "display:block;font-size:14px;margin-bottom:3px;";
        const message = document.createElement("div");
        message.textContent = notification?.message || "";
        toast.append(heading, message);
        container.append(toast);

        if (!document.getElementById("ciit-meet-toast-animation")) {
            const style = document.createElement("style");
            style.id = "ciit-meet-toast-animation";
            style.textContent = "@keyframes ciitMeetToastIn{from{opacity:0;transform:translateY(-8px)}to{opacity:1;transform:translateY(0)}}";
            document.head.append(style);
        }
        window.setTimeout(() => {
            toast.style.transition = "opacity .2s ease,transform .2s ease";
            toast.style.opacity = "0";
            toast.style.transform = "translateY(-8px)";
            window.setTimeout(() => toast.remove(), 220);
        }, 5000);
    };


    connection.on("MeetToast", showMeetToast);

    connection.on("MeetRoomUpdated", updateRoom);
    connection.on("MeetMyParticipantStatus", update => {
        if (update.sessionId !== sessionId) return;
        const previousStatus = room.dataset.participantStatus;
        room.dataset.participantStatus = update.status;
        window.ciitMeetApplyMediaPermissions?.(update);
        if (update.status === "Admitted" && previousStatus !== "Admitted") {
            connection.invoke("JoinMeetingRoom", sessionId)
                .catch(error => console.error("Meet peer room admission failed:", error));
        } else if (update.status === "Rejected" || update.status === "Removed"
            || (update.sessionStatus !== "Live" && update.sessionStatus !== "Scheduled")) {
            window.ciitMeetWebRtcCleanup?.();
        }
        if (previousStatus !== update.status) {
            if (update.status === "Admitted") showMeetToast({ title: "Access approved", message: "The host admitted you to the meeting.", type: "success" });
            else if (update.status === "Rejected") showMeetToast({ title: "Join request declined", message: "The host declined your request.", type: "warning" });
            else if (update.status === "Removed") showMeetToast({ title: "Removed from meeting", message: "The host removed you from this meeting.", type: "error" });
            else if (update.sessionStatus !== "Live" && update.sessionStatus !== "Scheduled") showMeetToast({ title: "Meeting ended", message: "This meeting is no longer live.", type: "info" });
        }
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
    let lastRoomJoinAt = 0;
    const rejoinRoomForFreshState = () => {
        if (connection.state !== signalR.HubConnectionState.Connected
            || Date.now() - lastRoomJoinAt < 2000) return;

        lastRoomJoinAt = Date.now();
        connection.invoke("JoinMeetingRoom", sessionId)
            .catch(error => {
                lastRoomJoinAt = 0;
                console.warn("Meet room state refresh failed:", error);
            });
    };

    connection.onreconnected(async () => {
        await connection.invoke("JoinMeetingRoom", sessionId);
        lastRoomJoinAt = Date.now();
        if (isHost) {
            const pendingCount = Number(room.querySelector("[data-meet-pending-count]")?.textContent || 0);
            if (pendingCount > 0) {
                showMeetToast({
                    title: pendingCount === 1 ? "Pending join request" : "Pending join requests",
                    message: pendingCount === 1
                        ? "A student is waiting for admission. Review the Participants panel."
                        : pendingCount + " students are waiting for admission. Review the Participants panel.",
                    type: "info"
                });
            }
        }
    });
    window.addEventListener("pageshow", rejoinRoomForFreshState);
    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) rejoinRoomForFreshState();
    });

    window.ciitMeetSignalRConnection = connection;
    window.ciitMeetSignalRReady = connection.start()
        .then(async () => {
            await connection.invoke("JoinMeetingRoom", sessionId);
            lastRoomJoinAt = Date.now();
        })
        .catch(error => {
            console.error("Meet live connection failed:", error);
            throw error;
        });
});


document.addEventListener("DOMContentLoaded", () => {
    const room = document.querySelector("[data-meet-room]");
    if (!room) return;

    const sessionId = Number(room.dataset.sessionId);
    const isHost = room.dataset.isHost === "true";
    const connection = window.ciitMeetSignalRConnection;
    const chatPanel = room.querySelector("[data-meet-chat-panel]");
    const chatToggle = room.querySelector("[data-meet-chat-toggle]");
    const chatClose = room.querySelector("[data-meet-chat-close]");
    const chatFeed = room.querySelector("[data-meet-chat-messages]");
    const chatForm = room.querySelector("[data-meet-chat-compose]");
    const chatInput = room.querySelector("[data-meet-chat-input]");
    const chatSend = room.querySelector("[data-meet-chat-send]");
    const chatStatus = room.querySelector("[data-meet-chat-status]");
    const handButton = room.querySelector("[data-meet-raise-hand]");
    let chatEnabled = room.dataset.chatEnabled !== "false";
    let handRaised = false;
    let chatJoined = false;

    if (!connection || !chatPanel || !chatFeed || !chatForm || !chatInput || !chatSend) return;

    const setChatEnabled = enabled => {
        chatEnabled = Boolean(enabled);
        room.dataset.chatEnabled = String(chatEnabled);
        chatInput.disabled = !chatEnabled;
        chatSend.disabled = !chatEnabled;
        if (chatStatus) {
            chatStatus.classList.toggle("is-disabled", !chatEnabled);
            chatStatus.innerHTML = chatEnabled
                ? '<i class="bi bi-shield-check"></i> Everyone in this meeting can see messages'
                : '<i class="bi bi-shield-lock"></i> Chat is disabled by the host';
        }
        if (!chatEnabled) chatInput.placeholder = "Chat is disabled by the host";
        else chatInput.placeholder = "Message everyone…";

        const settingForm = room.querySelector("[data-meet-chat-enabled-form]");
        if (settingForm) {
            const enabledInput = settingForm.querySelector('input[name="enabled"]');
            const button = settingForm.querySelector("[data-meet-chat-toggle-submit]");
            const stateLabel = settingForm.querySelector("[data-meet-chat-state]");
            if (enabledInput) enabledInput.value = String(!chatEnabled);
            if (button) button.textContent = chatEnabled ? "Disable chat" : "Enable chat";
            if (stateLabel) stateLabel.textContent = chatEnabled ? "On" : "Off";
        }
    };

    const showFeatureToast = (title, message, accent = "#8b9cff") => {
        let container = document.getElementById("ciit-meet-toast-container");
        if (!container) {
            container = document.createElement("div");
            container.id = "ciit-meet-toast-container";
            container.setAttribute("aria-live", "polite");
            container.style.cssText = "position:fixed;top:20px;right:20px;z-index:10000;display:flex;flex-direction:column;gap:10px;width:min(360px,calc(100vw - 32px));";
            document.body.append(container);
        }

        const toast = document.createElement("div");
        toast.style.cssText = "background:linear-gradient(145deg,rgba(35,49,70,.94),rgba(13,20,32,.92));color:#f3f7ff;border:1px solid rgba(177,202,238,.2);border-left:3px solid " + accent + ";border-radius:14px;padding:12px 15px;box-shadow:0 16px 42px rgba(0,0,0,.38);backdrop-filter:blur(22px);font:500 12px/1.45 system-ui,sans-serif;";
        const heading = document.createElement("strong");
        heading.textContent = title;
        heading.style.cssText = "display:block;font-size:13px;margin-bottom:3px;";
        const body = document.createElement("div");
        body.textContent = message;
        toast.append(heading, body);
        container.append(toast);
        window.setTimeout(() => {
            toast.style.transition = "opacity .22s ease,transform .22s ease";
            toast.style.opacity = "0";
            toast.style.transform = "translateY(-7px)";
            window.setTimeout(() => toast.remove(), 240);
        }, 4200);
    };

    const appendChatMessage = item => {
        if (!item || item.sessionId !== sessionId) return;

        const empty = chatFeed.querySelector(".ciit-meet-chat-empty");
        if (empty) empty.remove();

        const article = document.createElement("article");
        article.className = "ciit-meet-chat-message";

        const meta = document.createElement("div");
        meta.className = "ciit-meet-chat-message-meta";
        const sender = document.createElement("span");
        sender.textContent = item.displayName || "Participant";
        const time = document.createElement("time");
        const sentAt = item.sentAtUtc ? new Date(item.sentAtUtc) : new Date();
        time.dateTime = sentAt.toISOString();
        time.textContent = sentAt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        meta.append(sender, time);

        const bubble = document.createElement("div");
        bubble.className = "ciit-meet-chat-bubble";
        bubble.textContent = item.message || "";
        article.append(meta, bubble);
        chatFeed.append(article);

        while (chatFeed.children.length > 100) chatFeed.firstElementChild?.remove();
        chatFeed.scrollTop = chatFeed.scrollHeight;
    };

    const joinChat = async () => {
        if (chatJoined || connection.state !== "Connected") return;
        if (!isHost && room.dataset.participantStatus !== "Admitted") return;
        try {
            await connection.invoke("JoinChatRoom", sessionId);
            chatJoined = true;
        } catch (error) {
            console.warn("Meet group chat join failed:", error);
        }
    };

    const toggleChat = open => {
        chatPanel.hidden = !open;
        if (chatToggle) {
            chatToggle.setAttribute("aria-pressed", String(open));
            chatToggle.classList.toggle("is-active", open);
        }
        if (open) {
            chatInput.focus();
            chatFeed.scrollTop = chatFeed.scrollHeight;
        }
    };

    chatToggle?.addEventListener("click", () => toggleChat(chatPanel.hidden));
    chatClose?.addEventListener("click", () => toggleChat(false));

    chatForm.addEventListener("submit", async event => {
        event.preventDefault();
        const message = chatInput.value.trim();
        if (!message || !chatEnabled || message.length > 1000 || chatSend.disabled) return;

        chatSend.disabled = true;
        try {
            await connection.invoke("SendChatMessage", sessionId, message);
            chatInput.value = "";
            chatInput.style.height = "auto";
        } catch (error) {
            showFeatureToast("Message not sent", error.message || "Unable to send this message.", "#fb7185");
        } finally {
            chatSend.disabled = !chatEnabled;
        }
    });

    chatInput.addEventListener("input", () => {
        chatInput.style.height = "auto";
        chatInput.style.height = Math.min(chatInput.scrollHeight, 100) + "px";
    });

    handButton?.addEventListener("click", async () => {
        const nextRaised = !handRaised;
        handButton.disabled = true;
        try {
            await connection.invoke("RaiseHand", sessionId, nextRaised);
            handRaised = nextRaised;
            handButton.setAttribute("aria-pressed", String(handRaised));
            handButton.classList.toggle("is-active", handRaised);
            const label = handButton.querySelector("span");
            const icon = handButton.querySelector("i");
            if (label) label.textContent = handRaised ? "Lower hand" : "Raise hand";
            if (icon) icon.className = handRaised ? "bi bi-hand-index" : "bi bi-hand";
        } catch (error) {
            showFeatureToast("Raise hand failed", error.message || "Unable to update your hand.", "#fb7185");
        } finally {
            handButton.disabled = false;
        }
    });

    connection.on("MeetChatEnabledState", state => {
        if (state?.sessionId !== sessionId) return;
        setChatEnabled(state.enabled);
    });

    connection.on("MeetChatHistory", messages => {
        if (!Array.isArray(messages)) return;
        chatFeed.replaceChildren();
        if (!messages.length) {
            const empty = document.createElement("div");
            empty.className = "ciit-meet-chat-empty";
            empty.textContent = "No messages yet. Start the conversation with everyone.";
            chatFeed.append(empty);
            return;
        }
        messages.forEach(appendChatMessage);
    });

    connection.on("MeetChatMessage", appendChatMessage);

    connection.on("MeetHandRaised", state => {
        if (state?.sessionId !== sessionId) return;
        if (handButton && state.userId === room.dataset.userId) {
            handRaised = Boolean(state.raised);
            handButton.setAttribute("aria-pressed", String(handRaised));
            handButton.classList.toggle("is-active", handRaised);
            const label = handButton.querySelector("span");
            const icon = handButton.querySelector("i");
            if (label) label.textContent = handRaised ? "Lower hand" : "Raise hand";
            if (icon) icon.className = handRaised ? "bi bi-hand-index" : "bi bi-hand";
        }
        if (state.raised) {
            showFeatureToast("Raised hand", (state.displayName || "A participant") + " has raised the hand", "#f4bf5f");
        }
    });

    connection.on("MeetHandSnapshot", snapshot => {
        if (snapshot?.sessionId !== sessionId || !handButton || !Array.isArray(snapshot.raisedHands)) return;
        handRaised = snapshot.raisedHands.some(hand => hand.userId === room.dataset.userId);
        handButton.setAttribute("aria-pressed", String(handRaised));
        handButton.classList.toggle("is-active", handRaised);
        const label = handButton.querySelector("span");
        const icon = handButton.querySelector("i");
        if (label) label.textContent = handRaised ? "Lower hand" : "Raise hand";
        if (icon) icon.className = handRaised ? "bi bi-hand-index" : "bi bi-hand";
    });

    connection.on("MeetMyParticipantStatus", state => {
        if (state?.sessionId !== sessionId) return;
        if (state.status === "Admitted") joinChat();
    });

    connection.onreconnected(() => {
        chatJoined = false;
        joinChat();
    });

    setChatEnabled(chatEnabled);
    if (isHost || room.dataset.participantStatus === "Admitted") {
        window.ciitMeetSignalRReady?.then(joinChat).catch(error => console.warn("Meet chat readiness failed:", error));
    }
});
