document.addEventListener("DOMContentLoaded", () => {
    const role = document.body.dataset.meetNotificationRole;
    if (role !== "student" && role !== "admin") return;

    // Meet index pages already own a live connection and notification renderer.
    if (document.querySelector("[data-meet-student-index], [data-meet-admin-index]")) return;
    if (!window.signalR) {
        console.error("SignalR client library could not be loaded for Meet notifications.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/meet")
        .withAutomaticReconnect()
        .build();

    const showNotification = notification => {
        if (!notification || typeof notification.title !== "string") return;

        let container = document.getElementById("ciit-global-meet-toast-container");
        if (!container) {
            container = document.createElement("div");
            container.id = "ciit-global-meet-toast-container";
            container.setAttribute("aria-live", "polite");
            container.setAttribute("aria-relevant", "additions");
            container.style.cssText = "position:fixed;top:16px;right:16px;z-index:11000;width:min(360px,calc(100vw - 32px));";
            document.body.append(container);
        }

        const type = ["success", "warning", "error"].includes(notification.type)
            ? (notification.type === "error" ? "danger" : notification.type)
            : "info";
        const toast = document.createElement("div");
        toast.className = "alert alert-" + type + " shadow-sm";
        toast.setAttribute("role", "status");
        toast.style.cssText = "position:relative;padding:12px 38px 12px 14px;margin:0 0 10px;border-radius:12px;box-shadow:0 12px 32px rgba(0,0,0,.24);backdrop-filter:blur(12px);";

        const title = document.createElement("strong");
        title.textContent = notification.title;
        title.style.cssText = "display:block;font-size:14px;margin-bottom:3px;";

        const message = document.createElement("div");
        message.textContent = typeof notification.message === "string" ? notification.message : "";
        message.style.cssText = "font-size:12px;line-height:1.45;";

        const close = document.createElement("button");
        close.type = "button";
        close.className = "btn-close";
        close.setAttribute("aria-label", "Dismiss notification");
        close.style.cssText = "position:absolute;top:10px;right:10px;";
        close.addEventListener("click", () => toast.remove());

        toast.append(title, message, close);
        container.append(toast);

        window.setTimeout(() => {
            if (!toast.isConnected) return;
            toast.style.transition = "opacity .22s ease, transform .22s ease";
            toast.style.opacity = "0";
            toast.style.transform = "translateY(-6px)";
            window.setTimeout(() => toast.remove(), 240);
        }, 5000);
    };

    connection.on("MeetToast", showNotification);

    const joinNotificationGroups = () => connection.invoke(
        role === "student" ? "JoinMyBatchUpdates" : "JoinAdminUpdates"
    );

    connection.onreconnected(() => {
        joinNotificationGroups().catch(error =>
            console.error("Meet notification group rejoin failed:", error));
    });

    connection.start()
        .then(joinNotificationGroups)
        .catch(error => console.error("Meet notification connection failed:", error));
});
