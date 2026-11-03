document.addEventListener("DOMContentLoaded", () => {
    const room = document.querySelector("[data-meet-room]");
    if (!room || room.dataset.isHost !== "true") return;

    const syncControlsFromRoomHtml = html => {
        const parsed = new DOMParser().parseFromString(html, "text/html");
        const sourceRoom = parsed.querySelector("[data-meet-room]");
        if (!sourceRoom) throw new Error("Could not load the latest meeting controls.");

        const sourceWaitingForm = sourceRoom.querySelector("[data-meet-waiting-room-form]");
        const targetWaitingForm = document.querySelector("[data-meet-waiting-room-form]");
        if (sourceWaitingForm && targetWaitingForm) {
            const sourceState = sourceWaitingForm.querySelector("[data-meet-waiting-room-state]");
            const targetState = targetWaitingForm.querySelector("[data-meet-waiting-room-state]");
            const sourceEnabled = sourceWaitingForm.querySelector('input[name="enabled"]');
            const targetEnabled = targetWaitingForm.querySelector('input[name="enabled"]');
            const sourceButton = sourceWaitingForm.querySelector('[type="submit"]');
            const targetButton = targetWaitingForm.querySelector('[type="submit"]');

            if (sourceState && targetState) targetState.textContent = sourceState.textContent;
            if (sourceEnabled && targetEnabled) targetEnabled.value = sourceEnabled.value;
            if (sourceButton && targetButton) targetButton.textContent = sourceButton.textContent;
        }

        const sourceRecording = sourceRoom.querySelector("[data-meet-recording-controls]");
        const targetRecording = document.querySelector("[data-meet-recording-controls]");
        if (sourceRecording && targetRecording) {
            const sourceStatus = sourceRecording.querySelector("[data-meet-recording-status]");
            const targetStatus = targetRecording.querySelector("[data-meet-recording-status]");
            const recordingStatus = sourceStatus?.textContent.trim() || "";
            const recordingEnabled = sourceRecording.dataset.recordingEnabled === "true";

            if (sourceStatus && targetStatus) targetStatus.textContent = recordingStatus;
            targetRecording.dataset.recordingEnabled = String(recordingEnabled);

            const actions = {
                enable: !recordingEnabled,
                stop: recordingStatus === "Recording",
                start: recordingEnabled
                    && sourceRoom.querySelector("[data-meet-status-text]")?.textContent.trim() === "Live"
                    && (recordingStatus === "Ready" || recordingStatus === "Stopped"),
                disable: recordingEnabled && recordingStatus !== "Recording",
                delete: recordingStatus === "Stopped"
            };

            Object.entries(actions).forEach(([action, visible]) => {
                const targetForm = targetRecording.querySelector('[data-recording-action="' + action + '"]');
                if (targetForm) targetForm.hidden = !visible;
            });
            const targetDownload = targetRecording.querySelector("[data-meet-recording-download]");
            if (targetDownload) targetDownload.hidden = recordingStatus !== "Stopped";
        }

        const sourceStatusText = sourceRoom.querySelector("[data-meet-status-text]");
        const targetStatusText = room.querySelector("[data-meet-status-text]");
        if (sourceStatusText && targetStatusText) targetStatusText.textContent = sourceStatusText.textContent;
    };

    const refreshControls = async () => {
        const roomUrl = room.dataset.roomUrl;
        if (!roomUrl) throw new Error("Meeting room refresh URL is missing.");

        const response = await fetch(roomUrl, {
            method: "GET",
            cache: "no-store",
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) throw new Error("Could not refresh meeting controls (" + response.status + ").");

        syncControlsFromRoomHtml(await response.text());
    };

    document.addEventListener("submit", async event => {
        const form = event.target.closest("form[data-meet-ajax]");
        if (!form || form.querySelector("[data-meet-end-button]")
            || event.defaultPrevented) return;

        if (form.dataset.recordingAction === "delete"
            && !window.confirm("Mark this recording as deleted?")) {
            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        if (form.dataset.submitting === "true") return;

        const button = form.querySelector('[type="submit"]');
        form.dataset.submitting = "true";
        if (button) button.disabled = true;

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
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
                throw new Error(message || "Meeting control update failed (" + response.status + ").");
            }

            // Apply the persisted recording state returned by the controller immediately.
            const recordingActionJson = form.dataset.recordingAction;
            if (recordingActionJson && response.headers.get("content-type")?.includes("application/json")) {
                const result = await response.json();
                if (result.success === false) throw new Error(result.message || "Recording setting was rejected.");
                const targetRecording = document.querySelector("[data-meet-recording-controls]");
                if (targetRecording) {
                    targetRecording.dataset.recordingEnabled = String(result.recordingEnabled);
                    const targetStatus = targetRecording.querySelector("[data-meet-recording-status]");
                    if (targetStatus) targetStatus.textContent = result.recordingStatus || "";
                    const enabled = Boolean(result.recordingEnabled);
                    const status = result.recordingStatus || "";
                    const isLive = room.querySelector("[data-meet-status-text]")?.textContent.trim() === "Live";
                    const actions = {
                        enable: !enabled,
                        stop: status === "Recording",
                        start: enabled && isLive && (status === "Ready" || status === "Stopped"),
                        disable: enabled && status !== "Recording",
                        delete: status === "Stopped"
                    };
                    Object.entries(actions).forEach(([action, visible]) => {
                        const targetForm = targetRecording.querySelector('[data-recording-action="' + action + '"]');
                        if (targetForm) targetForm.hidden = !visible;
                    });
                }
            }

            // Refresh the server-rendered snapshot when available; retain the
            // just-applied JSON state if this secondary refresh transiently fails.
            try {
                await refreshControls();
            } catch (refreshError) {
                console.warn("Meeting controls HTML refresh failed after successful save:", refreshError);
            }

            // Review/admit/reject/remove and permission actions must also refresh
            // the server-authoritative participant snapshot in the live popup.
            if (window.ciitMeetSignalRConnection?.state === "Connected") {
                await window.ciitMeetSignalRConnection.invoke("JoinMeetingRoom", Number(room.dataset.sessionId));
            }

            const recordingAction = form.dataset.recordingAction;
            const waitingRoomEnabled = form.querySelector('input[name="enabled"]')?.value === "true";
            const successMessage = recordingAction
                ? ({
                    enable: "Recording has been enabled.",
                    start: "Recording status is now Recording.",
                    stop: "Recording status is now Stopped.",
                    disable: "Recording has been disabled.",
                    delete: "Recording metadata has been marked deleted."
                }[recordingAction] || "Recording setting updated.")
                : form.matches("[data-meet-waiting-room-form]")
                    ? (waitingRoomEnabled ? "Waiting room is now on." : "Waiting room is now off.")
                    : "Meeting setting updated.";

            if (typeof window.showMeetToast === "function") {
                window.showMeetToast({
                    title: "Meeting setting updated",
                    message: successMessage,
                    type: "success"
                });
            }
        } catch (error) {
            console.error("Meet control action failed:", error);
            if (typeof window.showMeetToast === "function") {
                window.showMeetToast({
                    title: "Meeting control failed",
                    message: error.message || "The setting could not be updated. Please try again.",
                    type: "error"
                });
            }
        } finally {
            form.dataset.submitting = "false";
            if (button) button.disabled = false;
        }
    }, true);
});
