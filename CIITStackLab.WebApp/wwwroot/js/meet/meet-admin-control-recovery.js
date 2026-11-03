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


        // Keep the host chat toggle's state and next submitted value in sync
        // with the server-rendered room snapshot after each AJAX update.
        const sourceChatForm = sourceRoom.querySelector("[data-meet-chat-enabled-form]");
        const targetChatForm = room.querySelector("[data-meet-chat-enabled-form]");
        if (sourceChatForm && targetChatForm) {
            const sourceChatState = sourceChatForm.querySelector("[data-meet-chat-state]");
            const targetChatState = targetChatForm.querySelector("[data-meet-chat-state]");
            const sourceEnabledInput = sourceChatForm.querySelector('input[name="enabled"]');
            const targetEnabledInput = targetChatForm.querySelector('input[name="enabled"]');
            const sourceChatButton = sourceChatForm.querySelector("[data-meet-chat-toggle-submit]");
            const targetChatButton = targetChatForm.querySelector("[data-meet-chat-toggle-submit]");

            if (sourceChatState && targetChatState) targetChatState.textContent = sourceChatState.textContent;
            if (sourceEnabledInput && targetEnabledInput) targetEnabledInput.value = sourceEnabledInput.value;
            if (sourceChatButton && targetChatButton) targetChatButton.textContent = sourceChatButton.textContent;
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


        event.preventDefault();
        event.stopImmediatePropagation();
        if (form.dataset.submitting === "true") return;

        const button = form.querySelector('[type="submit"]');
        form.dataset.submitting = "true";
        if (button) button.disabled = true;

        try {
            const response = await fetch(form.getAttribute('action') || location.href, {
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


            // Apply the authoritative JSON result immediately for the chat toggle.
            // The recovery handler runs in capture phase and prevents the normal
            // room-ui.js submit handler from processing this form.
            if (form.matches("[data-meet-chat-enabled-form]")) {
                const result = await response.clone().json().catch(() => null);
                if (result?.success && typeof result.chatEnabled === "boolean") {
                    window.ciitMeetSetChatEnabled?.(result.chatEnabled);
                    const liveConnection = window.ciitMeetSignalRConnection;
                    if (liveConnection?.state === "Connected") {
                        await liveConnection.invoke("JoinChatRoom", Number(room.dataset.sessionId));
                    }
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

            const waitingRoomEnabled = form.querySelector('input[name="enabled"]')?.value === "true";
            const successMessage = form.matches("[data-meet-waiting-room-form]")
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
