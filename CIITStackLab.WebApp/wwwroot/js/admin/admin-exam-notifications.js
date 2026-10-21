(function () {
    "use strict";

    const hubUrl = "/hubs/exam-application-notifications";
    const pendingUrl = "/Admin/ExamApplicationNotifications/Pending";
    const markReadUrl = "/Admin/ExamApplicationNotifications/MarkAllRead";
    const maxVisibleToasts = 4;
    const toastDismissMs = 5000;
    const toastAnimationMs = 280;
    const renderedToastIds = new Set();
    const toastSessionKey = "ciitExamNotificationToastsSeen";
    let toastStack = null;
    let connection = null;
    let refreshInProgress = false;
    let refreshRequested = false;

    const elements = {};

    function cacheElements() {
        elements.button = document.getElementById("ciitExamNotificationButton");
        elements.badge = document.getElementById("ciitExamNotificationBadge");
        elements.panel = document.getElementById("ciitExamNotificationPanel");
        elements.close = document.getElementById("ciitExamNotificationClose");
        elements.summary = document.getElementById("ciitExamNotificationSummary");
        elements.list = document.getElementById("ciitExamNotificationList");
        elements.markRead = document.getElementById("ciitExamNotificationMarkRead");
        elements.antiForgeryToken = document.querySelector(
            '#ciitExamNotificationAntiforgeryForm input[name="__RequestVerificationToken"]'
        )?.value || "";
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

    function formatDate(value) {
        const date = value ? new Date(value) : null;
        if (!date || Number.isNaN(date.getTime())) {
            return "Time unavailable";
        }

        return date.toLocaleString("en-IN", {
            day: "2-digit",
            month: "short",
            year: "numeric",
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    function formatRelativeSummary(unreadCount, totalCount) {
        if (unreadCount > 0) {
            return unreadCount + (unreadCount === 1 ? " unread alert" : " unread alerts")
                + " · " + totalCount + (totalCount === 1 ? " saved" : " saved");
        }

        return totalCount > 0
            ? totalCount + " saved alert" + (totalCount === 1 ? "" : "s") + " · all caught up"
            : "No exam-question alerts yet";
    }

    function setUnreadCount(count) {
        const unread = Math.max(0, Number(count) || 0);
        if (elements.badge) {
            elements.badge.hidden = unread === 0;
            elements.badge.textContent = unread > 99 ? "99+" : String(unread);
        }

        elements.button?.classList.toggle("has-unread", unread > 0);
        elements.button?.setAttribute(
            "aria-label",
            unread > 0
                ? "Exam notifications, " + unread + " unread"
                : "Exam notifications"
        );
    }

    function renderEmpty(message, detail) {
        if (!elements.list) {
            return;
        }

        elements.list.replaceChildren();
        const empty = createElement("div", "ciit-admin-notification-empty");
        const icon = createElement("span");
        icon.appendChild(createElement("i", "bi bi-bell-slash"));
        icon.querySelector("i").setAttribute("aria-hidden", "true");
        empty.appendChild(icon);
        empty.appendChild(createElement("strong", "", message));
        empty.appendChild(createElement("small", "", detail));
        elements.list.appendChild(empty);
    }

    function renderNotification(notification) {
        const isRead = Boolean(notification.isRead);
        const item = createElement(
            "article",
            "ciit-admin-notification-item" + (isRead ? "" : " is-unread")
        );
        item.dataset.notificationId = String(notification.notificationId || "");

        const icon = createElement("div", "ciit-admin-notification-item-icon");
        const iconGlyph = createElement("i", "bi bi-journal-x");
        iconGlyph.setAttribute("aria-hidden", "true");
        icon.appendChild(iconGlyph);

        const copy = createElement("div", "ciit-admin-notification-item-copy");
        const top = createElement("div", "ciit-admin-notification-item-top");
        top.appendChild(createElement(
            "strong",
            "ciit-admin-notification-item-title",
            "Exam questions unavailable"
        ));
        top.appendChild(createElement(
            "span",
            "ciit-admin-notification-item-status",
            isRead ? "Read" : "New"
        ));

        const studentName = notification.studentName || "A student";
        const courseTitle = notification.courseTitle || "Unknown course";
        const topicTitle = notification.topicTitle || "Unknown topic";
        const detail = createElement(
            "p",
            "ciit-admin-notification-item-body",
            studentName + " applied for an examination in " + topicTitle
                + " (" + courseTitle + ")."
        );

        const validCount = Number.isFinite(Number(notification.validQuestionCount))
            ? Number(notification.validQuestionCount)
            : 0;
        const requiredCount = Number.isFinite(Number(notification.requiredQuestionCount))
            ? Number(notification.requiredQuestionCount)
            : 50;
        const count = createElement(
            "div",
            "ciit-admin-notification-question-count",
            validCount + " / " + requiredCount + " valid questions"
        );

        const reason = createElement(
            "p",
            "ciit-admin-notification-reason",
            notification.reason || "The topic does not have enough valid questions for an exam."
        );

        const actions = createElement("div", "ciit-admin-notification-item-actions");
        actions.appendChild(createElement(
            "time",
            "ciit-admin-notification-date",
            formatDate(notification.occurredAtUtc)
        ));

        if (!isRead && notification.notificationId) {
            const markRead = createElement(
                "button",
                "ciit-admin-notification-mark-read",
                "Mark as read"
            );
            markRead.type = "button";
            markRead.dataset.notificationMarkRead = String(notification.notificationId);
            actions.appendChild(markRead);
        }

        const action = createElement("a", "ciit-admin-notification-action");
        action.href = "/Admin/Examination";
        action.appendChild(document.createTextNode("Open question bank "));
        const arrow = createElement("i", "bi bi-arrow-up-right");
        arrow.setAttribute("aria-hidden", "true");
        action.appendChild(arrow);
        actions.appendChild(action);

        copy.appendChild(top);
        copy.appendChild(detail);
        copy.appendChild(count);
        copy.appendChild(reason);
        copy.appendChild(actions);

        item.appendChild(icon);
        item.appendChild(copy);
        return item;
    }

    function renderNotifications(notifications) {
        if (!elements.list) {
            return;
        }

        const items = Array.isArray(notifications) ? notifications : [];
        const unread = items.filter(item => !item.isRead).length;
        setUnreadCount(unread);

        if (elements.summary) {
            elements.summary.textContent = formatRelativeSummary(unread, items.length);
        }

        if (elements.markRead) {
            elements.markRead.disabled = unread === 0;
        }

        if (items.length === 0) {
            renderEmpty(
                "No exam-question alerts yet",
                "When a student applies without enough valid questions, the saved alert will appear here."
            );
            return;
        }

        const fragment = document.createDocumentFragment();
        items.forEach(item => fragment.appendChild(renderNotification(item)));
        elements.list.replaceChildren(fragment);
    }

    async function refreshNotifications() {
        if (!elements.list) {
            return;
        }

        if (refreshInProgress) {
            refreshRequested = true;
            return;
        }

        refreshInProgress = true;
        try {
            const response = await fetch(pendingUrl, {
                credentials: "same-origin",
                cache: "no-store",
                headers: { "Accept": "application/json" }
            });

            if (!response.ok) {
                throw new Error("Could not load saved exam notifications.");
            }

            const result = await response.json();
            if (!result || result.success !== true || !Array.isArray(result.notifications)) {
                throw new Error("The exam notification response was invalid.");
            }

            renderNotifications(result.notifications);

            // Show each unread notification once per browser tab, including alerts
            // that were persisted while offline or whose previous toast was missed.
            const unreadNotifications = result.notifications.filter(item => !item.isRead);
            unreadNotifications.forEach(showToast);

            if (connection && connection.state === window.signalR?.HubConnectionState.Connected) {
                for (const item of unreadNotifications) {
                    if (!item.isDelivered) {
                        await acknowledgeNotification(item.notificationId);
                    }
                }
            }
        } catch (error) {
            console.warn("Unable to refresh saved exam notifications.", error);
            if (elements.summary) {
                elements.summary.textContent = "Saved alerts are temporarily unavailable";
            }
            if (!elements.list.children.length) {
                renderEmpty(
                    "Unable to load notifications",
                    "Check the connection and open the bell again to retry."
                );
            }
        } finally {
            refreshInProgress = false;
            if (refreshRequested) {
                refreshRequested = false;
                await refreshNotifications();
            }
        }
    }

    function showToast(payload) {
        if (!payload || !payload.topicTitle) {
            return;
        }

        const id = String(payload.notificationId || "");
        if (id) {
            let seenIds = [];
            try {
                seenIds = JSON.parse(window.sessionStorage.getItem(toastSessionKey) || "[]");
            } catch {
                seenIds = [];
            }

            if (renderedToastIds.has(id) || seenIds.includes(id)) {
                renderedToastIds.add(id);
                return;
            }

            renderedToastIds.add(id);
            seenIds.push(id);
            try {
                // sessionStorage survives navigation between admin pages, but resets for a new tab session.
                window.sessionStorage.setItem(toastSessionKey, JSON.stringify(seenIds.slice(-200)));
            } catch {
                // In-memory deduplication still prevents duplicate toasts on this page.
            }
        }

        if (!toastStack || !document.body.contains(toastStack)) {
            toastStack = createElement("div", "ciit-exam-notification-stack");
            toastStack.id = "ciitExamNotificationStack";
            toastStack.setAttribute("aria-live", "polite");
            toastStack.setAttribute("aria-relevant", "additions");
            toastStack.setAttribute("aria-label", "Live examination notifications");
            document.body.appendChild(toastStack);
        }

        const card = createElement("article", "ciit-exam-notification");
        card.dataset.notificationId = id;
        card.setAttribute("role", "status");

        const icon = createElement("div", "ciit-exam-notification-icon");
        const glyph = createElement("i", "bi bi-bell-fill");
        glyph.setAttribute("aria-hidden", "true");
        icon.appendChild(glyph);

        const copy = createElement("div", "ciit-exam-notification-copy");
        const kicker = createElement("div", "ciit-exam-notification-kicker");
        kicker.appendChild(createElement("span", "ciit-exam-notification-live"));
        kicker.appendChild(document.createTextNode("Live exam request"));

        const title = createElement("h2", "ciit-exam-notification-title", "Exam questions unavailable");
        const detail = createElement(
            "p",
            "ciit-exam-notification-detail",
            (payload.studentName || "A student")
                + " applied for an examination in " + payload.topicTitle
                + (payload.courseTitle ? " (" + payload.courseTitle + ")." : ".")
        );

        const validCount = Number.isFinite(Number(payload.validQuestionCount))
            ? Number(payload.validQuestionCount)
            : 0;
        const requiredCount = Number.isFinite(Number(payload.requiredQuestionCount))
            ? Number(payload.requiredQuestionCount)
            : 50;
        const count = createElement(
            "div",
            "ciit-exam-notification-count",
            validCount + " / " + requiredCount + " valid questions"
        );
        const reason = createElement(
            "p",
            "ciit-exam-notification-reason",
            payload.reason || "The topic does not have enough valid questions for an exam."
        );
        const footer = createElement("div", "ciit-exam-notification-footer");
        footer.appendChild(createElement("span", "ciit-exam-notification-time", formatDate(payload.occurredAtUtc)));

        const action = createElement("a", "ciit-exam-notification-link");
        action.href = "/Admin/Examination";
        action.appendChild(document.createTextNode("Open question bank "));
        const arrow = createElement("i", "bi bi-arrow-up-right");
        arrow.setAttribute("aria-hidden", "true");
        action.appendChild(arrow);
        footer.appendChild(action);

        copy.appendChild(kicker);
        copy.appendChild(title);
        copy.appendChild(detail);
        copy.appendChild(count);
        copy.appendChild(reason);
        copy.appendChild(footer);

        const close = createElement("button", "ciit-exam-notification-close");
        close.type = "button";
        close.setAttribute("aria-label", "Dismiss live notification");
        close.appendChild(createElement("i", "bi bi-x-lg"));
        close.querySelector("i").setAttribute("aria-hidden", "true");
        close.addEventListener("click", function () {
            dismissToast(card);
        });

        card.appendChild(icon);
        card.appendChild(copy);
        card.appendChild(close);
        toastStack.appendChild(card);

        while (toastStack.children.length > maxVisibleToasts) {
            toastStack.firstElementChild.remove();
        }

        window.setTimeout(function () {
            dismissToast(card);
        }, toastDismissMs);
    }

    function dismissToast(card) {
        if (!card || !card.isConnected || card.dataset.leaving === "true") {
            return;
        }

        card.dataset.leaving = "true";
        card.classList.add("is-leaving");
        window.setTimeout(function () {
            card.remove();
        }, toastAnimationMs);
    }

    async function acknowledgeNotification(notificationId) {
        if (!connection || connection.state !== window.signalR.HubConnectionState.Connected) {
            return;
        }

        try {
            await connection.invoke("AcknowledgeExamNotification", Number(notificationId));
        } catch (error) {
            // It stays undelivered in the database and is replayed after reconnect.
            console.warn("Exam notification acknowledgement failed; it will be replayed.", error);
        }
    }

    async function receiveLiveNotification(payload) {
        if (!payload || !payload.notificationId) {
            return;
        }

        showToast(payload);
        await refreshNotifications();
        await acknowledgeNotification(payload.notificationId);
    }

    async function receiveMissedNotifications(payloads) {
        if (!Array.isArray(payloads) || payloads.length === 0) {
            await refreshNotifications();
            return;
        }

        payloads.forEach(showToast);
        await refreshNotifications();

        for (const payload of payloads) {
            if (payload && payload.notificationId) {
                await acknowledgeNotification(payload.notificationId);
            }
        }
        await refreshNotifications();
    }

    async function markNotificationRead(notificationId, button) {
        if (!notificationId || !elements.antiForgeryToken) {
            console.error("The exam notification anti-forgery token is missing.");
            return;
        }

        if (button) {
            button.disabled = true;
        }

        const body = new URLSearchParams();
        body.set("__RequestVerificationToken", elements.antiForgeryToken);

        try {
            const response = await fetch(
                "/Admin/ExamApplicationNotifications/" + encodeURIComponent(notificationId) + "/MarkRead",
                {
                    method: "POST",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8",
                        "Accept": "application/json"
                    },
                    body
                }
            );

            if (!response.ok) {
                throw new Error("Could not mark the exam notification as read.");
            }

            // The Pending endpoint returns unread records only, so this item disappears from the inbox.
            await refreshNotifications();
        } catch (error) {
            console.error("Unable to mark this exam notification as read.", error);
            await refreshNotifications();
        } finally {
            if (button && button.isConnected) {
                button.disabled = false;
            }
        }
    }

    async function markAllRead() {
        if (!elements.markRead || elements.markRead.disabled) {
            return;
        }

        if (!elements.antiForgeryToken) {
            console.error("The exam notification anti-forgery token is missing.");
            return;
        }

        elements.markRead.disabled = true;
        const body = new URLSearchParams();
        body.set("__RequestVerificationToken", elements.antiForgeryToken);

        try {
            const response = await fetch(markReadUrl, {
                method: "POST",
                credentials: "same-origin",
                cache: "no-store",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8",
                    "Accept": "application/json"
                },
                body
            });

            if (!response.ok) {
                throw new Error("Could not mark notifications as read.");
            }

            await refreshNotifications();
        } catch (error) {
            console.error("Unable to mark exam alerts as read.", error);
            await refreshNotifications();
        } finally {
            if (elements.markRead) {
                elements.markRead.disabled = false;
            }
        }
    }

    function setPanelOpen(open) {
        if (!elements.panel || !elements.button) {
            return;
        }

        elements.panel.hidden = !open;
        elements.button.setAttribute("aria-expanded", open ? "true" : "false");
        if (open) {
            refreshNotifications();
        }
    }

    function startConnection() {
        if (!window.signalR || connection) {
            return;
        }

        connection = new window.signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(window.signalR.LogLevel.Warning)
            .build();

        connection.on("ExamQuestionsUnavailable", function (payload) {
            receiveLiveNotification(payload);
        });
        connection.on("ExamMissedNotificationsReceived", function (payloads) {
            receiveMissedNotifications(payloads);
        });
        connection.onreconnecting(function () {
            document.documentElement.dataset.examNotificationsConnected = "false";
        });
        connection.onreconnected(async function () {
            document.documentElement.dataset.examNotificationsConnected = "true";
            await refreshNotifications();
        });
        connection.onclose(function () {
            document.documentElement.dataset.examNotificationsConnected = "false";
            window.setTimeout(function () {
                if (connection && connection.state === window.signalR.HubConnectionState.Disconnected) {
                    startConnectionAfterClose();
                }
            }, 5000);
        });

        startInitialConnection();
    }

    async function startInitialConnection() {
        try {
            await connection.start();
            document.documentElement.dataset.examNotificationsConnected = "true";
        } catch (error) {
            document.documentElement.dataset.examNotificationsConnected = "false";
            console.warn("Live exam alerts could not connect. Retrying shortly.", error);
            window.setTimeout(startInitialConnection, 5000);
        }
    }

    async function startConnectionAfterClose() {
        if (!connection || connection.state !== window.signalR.HubConnectionState.Disconnected) {
            return;
        }

        try {
            await connection.start();
            document.documentElement.dataset.examNotificationsConnected = "true";
            await refreshNotifications();
        } catch (error) {
            document.documentElement.dataset.examNotificationsConnected = "false";
            console.warn("Live exam alerts remain disconnected. Retrying shortly.", error);
            window.setTimeout(startConnectionAfterClose, 5000);
        }
    }

    function initialize() {
        cacheElements();

        if (elements.button && elements.panel) {
            elements.button.addEventListener("click", function () {
                setPanelOpen(elements.panel.hidden);
            });
        }

        elements.close?.addEventListener("click", function () {
            setPanelOpen(false);
        });

        elements.markRead?.addEventListener("click", markAllRead);
        elements.list?.addEventListener("click", function (event) {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const button = target.closest("[data-notification-mark-read]");
            if (!button) {
                return;
            }

            event.preventDefault();
            markNotificationRead(button.dataset.notificationMarkRead, button);
        });

        document.addEventListener("click", function (event) {
            if (!elements.panel || elements.panel.hidden) {
                return;
            }

            const target = event.target;
            if (target instanceof Node
                && !elements.panel.contains(target)
                && !elements.button?.contains(target)) {
                setPanelOpen(false);
            }
        });

        refreshNotifications();
        startConnection();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
