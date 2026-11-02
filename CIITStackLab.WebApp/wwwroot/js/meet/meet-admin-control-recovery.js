document.addEventListener("DOMContentLoaded", () => {
    const room = document.querySelector("[data-meet-room]");
    if (!room || room.dataset.isHost !== "true") return;

    const syncControlsFromResponse = html => {
        const parsed = new DOMParser().parseFromString(html, "text/html");
        const sourceRoom = parsed.querySelector("[data-meet-room]");
        if (!sourceRoom) return;

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
            if (sourceStatus && targetStatus) targetStatus.textContent = sourceStatus.textContent;

            ["enable", "stop", "start", "disable", "delete"].forEach(action => {
                const sourceForm = sourceRecording.querySelector('[data-recording-action="' + action + '"]');
                const targetForm = targetRecording.querySelector('[data-recording-action="' + action + '"]');
                if (sourceForm && targetForm) targetForm.hidden = sourceForm.hidden;
            });
        }

        const sourceStatusText = sourceRoom.querySelector("[data-meet-status-text]");
        const targetStatusText = room.querySelector("[data-meet-status-text]");
        if (sourceStatusText && targetStatusText) targetStatusText.textContent = sourceStatusText.textContent;
    };

    document.addEventListener("submit", async event => {
        const form = event.target.closest("form[data-meet-ajax]");
        if (!form || form.querySelector("[data-meet-end-button]") || event.defaultPrevented) return;

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
            if (!response.ok) throw new Error("Meeting control update failed (" + response.status + ").");
            syncControlsFromResponse(await response.text());
        } catch (error) {
            console.error("Meet control action failed:", error);
        } finally {
            form.dataset.submitting = "false";
            if (button) button.disabled = false;
        }
    }, true);
});
