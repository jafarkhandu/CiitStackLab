(function () {
    "use strict";

    const hubUrl = "/hubs/exam-application-notifications";
    const maxVisibleNotifications = 4;
    let stack;

    function getStack() {
        if (stack && document.body.contains(stack)) {
            return stack;
        }

        stack = document.createElement("div");
        stack.className = "ciit-exam-notification-stack";
        stack.id = "ciitExamNotificationStack";
        stack.setAttribute("aria-live", "polite");
        stack.setAttribute("aria-relevant", "additions");
        stack.setAttribute("aria-label", "Live examination notifications");
        document.body.appendChild(stack);
        return stack;
    }

    function createElement(tag, className, text) {
        const element = document.createElement(tag);
        if (className) {
            element.className = className;
        }
        if (text !== undefined && text !== null) {
            element.textContent = String(text);
        }
        return element;
    }

    function formatTimestamp(value) {
        const date = value ? new Date(value) : new Date();
        if (Number.isNaN(date.getTime())) {
            return "Just now";
        }

        return date.toLocaleTimeString([], {
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    function dismissNotification(card) {
        if (!card || !card.isConnected) {
            return;
        }

        card.classList.add("is-leaving");
        window.setTimeout(function () {
            if (card.isConnected) {
                card.remove();
            }
        }, 240);
    }

    function showExamNotification(payload) {
        if (!payload || !payload.topicTitle) {
            return;
        }

        const host = getStack();
        const card = createElement("article", "ciit-exam-notification");
        card.setAttribute("role", "status");

        const icon = createElement("div", "ciit-exam-notification-icon");
        const iconGlyph = createElement("i", "bi bi-bell-fill");
        iconGlyph.setAttribute("aria-hidden", "true");
        icon.appendChild(iconGlyph);

        const copy = createElement("div", "ciit-exam-notification-copy");
        const kicker = createElement("div", "ciit-exam-notification-kicker");
        kicker.appendChild(createElement("span", "ciit-exam-notification-live"));
        kicker.appendChild(document.createTextNode("Live exam request"));

        const title = createElement("h2", "ciit-exam-notification-title", "Exam questions unavailable");
        const detail = createElement(
            "p",
            "ciit-exam-notification-detail",
            (payload.studentName || "A student")
                + " applied for an examination in "
                + payload.topicTitle
                + (payload.courseTitle ? " (" + payload.courseTitle + ")." : ".")
        );

        const questionCount = Number.isFinite(Number(payload.validQuestionCount))
            ? Number(payload.validQuestionCount)
            : 0;
        const requiredQuestionCount = Number.isFinite(Number(payload.requiredQuestionCount))
            ? Number(payload.requiredQuestionCount)
            : 50;
        const count = createElement(
            "div",
            "ciit-exam-notification-count",
            questionCount + " / " + requiredQuestionCount + " valid questions"
        );

        const reason = createElement(
            "p",
            "ciit-exam-notification-reason",
            payload.reason
                || "The topic does not currently have enough valid questions to create an exam."
        );

        const footer = createElement("div", "ciit-exam-notification-footer");
        footer.appendChild(
            createElement("span", "ciit-exam-notification-time", formatTimestamp(payload.occurredAtUtc))
        );

        const action = createElement("a", "ciit-exam-notification-link");
        action.href = "/Admin/Examination";
        action.appendChild(document.createTextNode("Open question bank "));
        const actionIcon = createElement("i", "bi bi-arrow-up-right");
        actionIcon.setAttribute("aria-hidden", "true");
        action.appendChild(actionIcon);
        footer.appendChild(action);

        copy.appendChild(kicker);
        copy.appendChild(title);
        copy.appendChild(detail);
        copy.appendChild(count);
        copy.appendChild(reason);
        copy.appendChild(footer);

        const close = createElement("button", "ciit-exam-notification-close");
        close.type = "button";
        close.setAttribute("aria-label", "Dismiss examination notification");
        close.appendChild(createElement("i", "bi bi-x-lg"));
        close.querySelector("i").setAttribute("aria-hidden", "true");
        close.addEventListener("click", function () {
            dismissNotification(card);
        });

        card.appendChild(icon);
        card.appendChild(copy);
        card.appendChild(close);
        host.appendChild(card);

        while (host.children.length > maxVisibleNotifications) {
            host.firstElementChild.remove();
        }

        // Keep action-required alerts visible until an administrator dismisses them.
    }

    async function startConnection(connection) {
        try {
            await connection.start();
            document.documentElement.dataset.examNotificationsConnected = "true";
        } catch (error) {
            document.documentElement.dataset.examNotificationsConnected = "false";
            console.warn("Live examination notifications could not connect. Retrying shortly.", error);
            window.setTimeout(function () {
                startConnection(connection);
            }, 5000);
        }
    }

    function initializeExamNotifications() {
        if (!window.signalR || document.documentElement.dataset.examNotificationsInitialized === "true") {
            return;
        }

        document.documentElement.dataset.examNotificationsInitialized = "true";

        const connection = new window.signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(window.signalR.LogLevel.Warning)
            .build();

        connection.on("ExamQuestionsUnavailable", showExamNotification);
        connection.onreconnecting(function () {
            document.documentElement.dataset.examNotificationsConnected = "false";
        });
        connection.onreconnected(function () {
            document.documentElement.dataset.examNotificationsConnected = "true";
        });
        connection.onclose(function () {
            document.documentElement.dataset.examNotificationsConnected = "false";
            window.setTimeout(function () {
                startConnection(connection);
            }, 5000);
        });

        startConnection(connection);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initializeExamNotifications, { once: true });
    } else {
        initializeExamNotifications();
    }
})();
