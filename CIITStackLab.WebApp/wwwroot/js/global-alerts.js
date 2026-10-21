(function () {
    "use strict";

    const DISMISS_AFTER_MS = 5000;
    const DISMISS_ANIMATION_MS = 360;
    const selector = ".alert, .ciit-exam-availability-alert";

    function removeAlert(alert) {
        if (alert && alert.isConnected) {
            alert.remove();
        }
    }

    function dismissAlert(alert) {
        if (!alert || !alert.isConnected || alert.dataset.ciitAlertDismissing === "true") {
            return;
        }

        alert.dataset.ciitAlertDismissing = "true";
        alert.setAttribute("aria-hidden", "true");

        const reduceMotion = window.matchMedia
            && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

        if (reduceMotion || typeof alert.animate !== "function") {
            if (!reduceMotion) {
                alert.classList.add("ciit-alert-auto-dismiss", "is-dismissing");
            }
            window.setTimeout(function () {
                removeAlert(alert);
            }, reduceMotion ? 0 : DISMISS_ANIMATION_MS);
            return;
        }

        const computed = window.getComputedStyle(alert);
        const height = Math.ceil(alert.getBoundingClientRect().height);

        alert.classList.add("ciit-alert-auto-dismiss");
        alert.style.overflow = "hidden";

        const animation = alert.animate(
            [
                {
                    opacity: Number(computed.opacity) || 1,
                    transform: "translateY(0) scale(1)",
                    height: height + "px",
                    maxHeight: height + "px",
                    marginTop: computed.marginTop,
                    marginBottom: computed.marginBottom,
                    paddingTop: computed.paddingTop,
                    paddingBottom: computed.paddingBottom,
                    borderTopWidth: computed.borderTopWidth,
                    borderBottomWidth: computed.borderBottomWidth
                },
                {
                    opacity: 0,
                    transform: "translateY(-7px) scale(.99)",
                    height: "0px",
                    maxHeight: "0px",
                    marginTop: "0px",
                    marginBottom: "0px",
                    paddingTop: "0px",
                    paddingBottom: "0px",
                    borderTopWidth: "0px",
                    borderBottomWidth: "0px"
                }
            ],
            {
                duration: DISMISS_ANIMATION_MS,
                easing: "cubic-bezier(.2, .8, .2, 1)",
                fill: "forwards"
            }
        );

        animation.onfinish = function () {
            removeAlert(alert);
        };
        animation.oncancel = function () {
            removeAlert(alert);
        };
    }

    function initializeAlert(alert) {
        if (!(alert instanceof HTMLElement)
            || alert.dataset.noAutoDismiss === "true"
            || alert.dataset.ciitAutoDismissInitialized === "true") {
            return;
        }

        alert.dataset.ciitAutoDismissInitialized = "true";
        window.setTimeout(function () {
            dismissAlert(alert);
        }, DISMISS_AFTER_MS);
    }

    function initializeExistingAlerts() {
        document.querySelectorAll(selector).forEach(initializeAlert);
    }

    function observeNewAlerts() {
        if (!(window.MutationObserver) || !document.body) {
            return;
        }

        const observer = new MutationObserver(function (records) {
            records.forEach(function (record) {
                record.addedNodes.forEach(function (node) {
                    if (!(node instanceof HTMLElement)) {
                        return;
                    }

                    if (node.matches(selector)) {
                        initializeAlert(node);
                    }

                    node.querySelectorAll(selector).forEach(initializeAlert);
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
