(function () {
    "use strict";

    const DISMISS_AFTER_MS = 5000;
    const DISMISS_ANIMATION_MS = 360;
    const selector = ".alert, .ciit-exam-availability-alert";

    function dismissAlert(alert) {
        if (!alert || !alert.isConnected || alert.dataset.ciitAlertDismissing === "true") {
            return;
        }

        alert.dataset.ciitAlertDismissing = "true";
        alert.classList.add("ciit-alert-auto-dismiss", "is-dismissing");

        window.setTimeout(function () {
            if (alert.isConnected) {
                alert.remove();
            }
        }, DISMISS_ANIMATION_MS);
    }

    function initializeAlert(alert) {
        if (!(alert instanceof HTMLElement)
            || alert.dataset.noAutoDismiss === "true"
            || alert.dataset.ciitAutoDismissInitialized === "true") {
            return;
        }

        alert.dataset.ciitAutoDismissInitialized = "true";
        alert.classList.add("ciit-alert-auto-dismiss");

        window.setTimeout(function () {
            dismissAlert(alert);
        }, DISMISS_AFTER_MS);
    }

    function initializeExistingAlerts() {
        document.querySelectorAll(selector).forEach(initializeAlert);
    }

    function observeNewAlerts() {
        if (!("MutationObserver" in window) || !document.body) {
            return;
        }

        const observer = new MutationObserver(function (records) {
            records.forEach(function (record) {
                record.addedNodes.forEach(function (node) {
                    if (!(node instanceof HTMLElement)) {
                        return;
                    }

                    if (node.matches?.(selector)) {
                        initializeAlert(node);
                    }

                    node.querySelectorAll?.(selector).forEach(initializeAlert);
                });
            });
        });

        observer.observe(document.body, { childList: true, subtree: true });
    }

    function start() {
        initializeExistingAlerts();
        observeNewAlerts();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
})();
