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

        // Always schedule final removal. Visual animation is best-effort and must never
        // leave an alert stuck if the browser cancels or rejects a Web Animation.
        const fallbackRemoval = window.setTimeout(function () {
            removeAlert(alert);
        }, DISMISS_ANIMATION_MS + 100);

        const reduceMotion = window.matchMedia
            && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

        if (reduceMotion) {
            window.clearTimeout(fallbackRemoval);
            removeAlert(alert);
            return;
        }

        try {
            const computed = window.getComputedStyle(alert);
            const height = Math.ceil(alert.getBoundingClientRect().height);

            alert.classList.add("ciit-alert-auto-dismiss");
            alert.style.overflow = "hidden";

            if (typeof alert.animate === "function") {
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
                    window.clearTimeout(fallbackRemoval);
                    removeAlert(alert);
                };
                animation.oncancel = function () {
                    window.clearTimeout(fallbackRemoval);
                    removeAlert(alert);
                };
            } else {
                alert.classList.add("is-dismissing");
            }
        } catch (error) {
            // Fallback timer above guarantees removal even if the animation API fails.
            alert.classList.add("is-dismissing");
        }
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
