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
