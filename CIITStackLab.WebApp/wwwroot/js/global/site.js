document.addEventListener("DOMContentLoaded", function () {
    const topics = document.getElementById("ciitTopicsScroll");
    const previousTopic = document.getElementById("topicsPrev");
    const nextTopic = document.getElementById("topicsNext");
    if (topics && previousTopic && nextTopic) {
        previousTopic.addEventListener("click", () => topics.scrollBy({ left: -350, behavior: "smooth" }));
        nextTopic.addEventListener("click", () => topics.scrollBy({ left: 350, behavior: "smooth" }));
    }

    const slides = document.querySelectorAll(".ciit-hero-slide");
    const dots = document.querySelectorAll(".ciit-slider-dot");
    if (!slides.length || slides.length !== dots.length) {
        return;
    }

    let current = 0;
    let timer;

    function loadImage(index, priority) {
        const image = slides[index].querySelector("img");
        if (image) {
            image.fetchPriority = priority;
            if (image.dataset.src) {
                image.src = image.dataset.src;
                delete image.dataset.src;
            }
        }
    }

    function show(index) {
        slides[current].classList.remove("active");
        dots[current].classList.remove("active");
        current = index;
        loadImage(current, "high");
        slides[current].classList.add("active");
        dots[current].classList.add("active");
        const next = (current + 1) % slides.length;
        if (next !== current) {
            loadImage(next, "low");
        }
    }

    function restart() {
        clearInterval(timer);
        timer = setInterval(() => show((current + 1) % slides.length), 4000);
    }

    dots.forEach((dot, index) => {
        dot.addEventListener("click", () => {
            show(index);
            restart();
        });
    });
    show(0);
    restart();
});

document.addEventListener("DOMContentLoaded", function () {
    if (!document.body.classList.contains("ciit-course-environment-body")) {
        return;
    }

    // The Learn page loads the player when the Content tab is opened below.
    // Do not parse sidebar video links and download the Mux bundle on every Notes/Assessment visit.
    if (document.querySelector(".ciit-learning-page")) {
        return;
    }

    const hasVideoResource = document.querySelector(
        '.ciit-learning-video-frame iframe[src], a[href*="/uploads/videos/"], .ciit-notes-reader-media a, .ciit-mux-watch-player'
    );
    if (!hasVideoResource || document.querySelector("script[data-ciit-mux-player]")) {
        return;
    }

    const playerScript = document.createElement("script");
    playerScript.src = "/js/mux-video-player.js?v=3";
    playerScript.async = true;
    playerScript.dataset.ciitMuxPlayer = "true";
    document.body.appendChild(playerScript);
});

document.addEventListener("DOMContentLoaded", function () {
    const transition = document.getElementById("ciitHubTransition");
    if (!transition) return;

    let activeLink = null;
    document.querySelectorAll('a[data-learning-hub-transition="true"]').forEach(function (link) {
        link.addEventListener("click", function (event) {
            if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
                return;
            }

            if (activeLink) {
                event.preventDefault();
                return;
            }

            activeLink = link;
            link.setAttribute("aria-disabled", "true");
            transition.classList.add("is-active");
            transition.setAttribute("aria-hidden", "false");
            document.body.classList.add("ciit-hub-transitioning");
        });
    });

    window.addEventListener("pageshow", function () {
        if (!activeLink) return;
        activeLink.removeAttribute("aria-disabled");
        activeLink = null;
        transition.classList.remove("is-active");
        transition.setAttribute("aria-hidden", "true");
        document.body.classList.remove("ciit-hub-transitioning");
    });
});

document.addEventListener("DOMContentLoaded", function () {
    const paymentForm = document.getElementById("ciit-demo-payment-form");
    const paymentTrigger = document.querySelector("[data-ciit-pay-now]");

    const getServerPaymentMessage = () => {
        const icon = document.querySelector("main .container.mt-3 .alert .bi-credit-card");
        const alert = icon?.closest(".alert");
        if (!alert) {
            return null;
        }

        const message = alert.querySelector("span")?.textContent?.trim();
        const container = alert.closest(".container");
        container?.remove();

        return message || null;
    };

    const getToastType = (message) => {
        const normalized = message.toLowerCase();
        if (normalized.includes("successful")) return "success";
        if (normalized.includes("still") || normalized.includes("verify") || normalized.includes("processing")) return "warning";
        if (normalized.includes("failed") || normalized.includes("could not") || normalized.includes("no course access")) return "danger";
        return "info";
    };

    let toastTimer;

    const showPaymentToast = (message, type = "danger") => {
        if (!message) return;

        clearTimeout(toastTimer);
        document.getElementById("ciit-payment-toast")?.remove();

        const toast = document.createElement("div");
        toast.id = "ciit-payment-toast";
        toast.className = `ciit-payment-toast ciit-payment-toast-${type}`;
        toast.setAttribute("role", type === "danger" ? "alert" : "status");
        toast.setAttribute("aria-live", "polite");

        const icon = type === "success"
            ? "bi-check-circle-fill"
            : type === "warning"
                ? "bi-exclamation-circle-fill"
                : type === "info"
                    ? "bi-info-circle-fill"
                    : "bi-x-circle-fill";

        toast.innerHTML = `
            <span class="ciit-payment-toast-icon" aria-hidden="true"><i class="bi ${icon}"></i></span>
            <span class="ciit-payment-toast-content">
                <strong>${type === "danger" ? "Payment Failed" : type === "success" ? "Payment Successful" : "Payment Update"}</strong>
                <span></span>
            </span>
            <button type="button" class="ciit-payment-toast-close" aria-label="Close notification">&times;</button>`;

        toast.querySelector(".ciit-payment-toast-content > span").textContent = message;
        toast.querySelector(".ciit-payment-toast-close").addEventListener("click", () => {
            toast.classList.remove("is-visible");
            window.setTimeout(() => toast.remove(), 180);
        });

        document.body.appendChild(toast);
        requestAnimationFrame(() => toast.classList.add("is-visible"));

        toastTimer = window.setTimeout(() => {
            toast.classList.remove("is-visible");
            window.setTimeout(() => toast.remove(), 180);
        }, 5000);
    };

    const clearPaymentToast = () => {
        clearTimeout(toastTimer);
        document.getElementById("ciit-payment-toast")?.remove();
    };

    const serverPaymentMessage = getServerPaymentMessage();
    if (serverPaymentMessage) {
        showPaymentToast(serverPaymentMessage, getToastType(serverPaymentMessage));
    }

    if (!paymentForm || !paymentTrigger) {
        return;
    }

    const paymentButton = document.getElementById("ciit-demo-payment-button");
    const paymentNote = paymentForm.nextElementSibling;
    const processingState = document.getElementById("ciit-payment-processing-state");
    const successState = document.getElementById("ciit-payment-success-state");
    const formState = document.getElementById("ciit-payment-form-state");
    const modalEyebrow = document.querySelector(".ciit-payment-modal-eyebrow");
    const modalSubtitle = document.querySelector(".ciit-payment-modal-subtitle");
    let merchantOrderId;
    let paymentOverlay;

    const defaultPayNowHtml = paymentTrigger.innerHTML;
    paymentTrigger.innerHTML = '<i class="bi bi-credit-card me-2"></i>Pay Now';

    if (modalEyebrow) {
        modalEyebrow.textContent = "Secure Razorpay checkout";
    }

    if (modalSubtitle) {
        modalSubtitle.textContent = "Complete your payment securely. Course access is granted only after server-side verification.";
    }

    if (paymentNote) {
        paymentNote.textContent = "Razorpay checkout opens securely. Course access is granted only after server-side payment verification.";
    }

    const defaultPaymentButtonHtml = paymentButton?.innerHTML
        || '<i class="bi bi-lock-fill me-2"></i>Pay securely with Razorpay';

    const removePaymentOverlay = () => {
        paymentOverlay?.remove();
        paymentOverlay = null;
    };

    const showPaymentOverlay = (title, message, state = "processing", action = null) => {
        removePaymentOverlay();

        const overlay = document.createElement("div");
        overlay.className = `ciit-payment-overlay ciit-payment-overlay-${state}`;
        overlay.setAttribute("role", "dialog");
        overlay.setAttribute("aria-modal", "true");
        overlay.setAttribute("aria-live", "polite");

        if (state === "success") {
            overlay.innerHTML = `
                <div class="ciit-payment-overlay-card">
                    <div class="ciit-payment-success-ring" aria-hidden="true">
                        <i class="bi bi-check-lg"></i>
                    </div>
                    <strong>${title}</strong>
                    <span>${message}</span>
                    <div class="ciit-payment-overlay-progress" aria-hidden="true"><span></span></div>
                </div>`;
        } else {
            overlay.innerHTML = `
                <div class="ciit-payment-overlay-card">
                    <div class="ciit-payment-overlay-spinner" aria-hidden="true">
                        <span></span>
                        <i class="bi bi-shield-check"></i>
                    </div>
                    <strong>${title}</strong>
                    <span class="ciit-payment-overlay-message"></span>
                    <div class="ciit-payment-overlay-dots" aria-hidden="true"><span></span><span></span><span></span></div>
                    ${action ? '<div class="ciit-payment-overlay-action"></div>' : ""}
                </div>`;
            overlay.querySelector(".ciit-payment-overlay-message").textContent = message;

            if (action) {
                const actionHost = overlay.querySelector(".ciit-payment-overlay-action");
                const button = document.createElement("button");
                button.type = "button";
                button.className = "btn btn-outline-primary btn-sm";
                button.textContent = action.label;
                button.addEventListener("click", action.handler, { once: true });
                actionHost.appendChild(button);
            }
        }

        document.body.appendChild(overlay);
        requestAnimationFrame(() => overlay.classList.add("is-visible"));
        paymentOverlay = overlay;
    };

    const showPagePaymentMessage = (message, type = "danger") => {
        showPaymentToast(message, type);
    };

    const clearPagePaymentMessage = () => {
        clearPaymentToast();
    };

    const loadRazorpayCheckout = () => {
        if (window.CiitRazorpayCheckout?.transact) {
            return Promise.resolve();
        }

        return Promise.reject(new Error(
            "Razorpay Checkout could not be initialized. Refresh the page and try again."
        ));
    };

    const getCheckoutUrl = async () => {
        const formData = new FormData(paymentForm);
        const response = await fetch(paymentForm.action, {
            method: "POST",
            body: formData,
            headers: {
                Accept: "application/json"
            },
            credentials: "same-origin"
        });

        if (!response.ok) {
            let message = "The payment gateway could not be reached. Please try again.";

            try {
                const error = await response.json();
                if (error?.message) {
                    message = error.message;
                }
            } catch {
                // Keep the generic message when the response is not JSON.
            }

            throw new Error(message);
        }

        const result = await response.json();
        merchantOrderId = result?.merchantOrderId;

        if (!result?.checkoutUrl || !merchantOrderId) {
            throw new Error("Razorpay did not return the required checkout details.");
        }

        return result.checkoutUrl;
    };

    const showProcessing = (message) => {
        clearPagePaymentMessage();
        showPaymentOverlay("Verifying your payment", message || "Checking the payment status securely with Razorpay...");

        if (processingState) processingState.hidden = false;
        if (formState) formState.hidden = true;
        if (successState) successState.hidden = true;
    };

    const showFailure = (message) => {
        removePaymentOverlay();
        showPagePaymentMessage(message || "Payment could not be completed. No course access was granted.", "danger");

        if (processingState) processingState.hidden = true;
        if (formState) formState.hidden = false;
        if (successState) successState.hidden = true;

        if (paymentButton) {
            paymentButton.disabled = false;
            paymentButton.innerHTML = defaultPaymentButtonHtml;
        }

        paymentTrigger.disabled = false;
        paymentTrigger.innerHTML = defaultPayNowHtml;
    };

    function resetPaymentForm() {
        removePaymentOverlay();
        clearPagePaymentMessage();

        if (processingState) processingState.hidden = true;
        if (successState) successState.hidden = true;
        if (formState) formState.hidden = false;

        if (paymentButton) {
            paymentButton.disabled = false;
            paymentButton.innerHTML = defaultPaymentButtonHtml;
        }

        paymentTrigger.disabled = false;
        paymentTrigger.innerHTML = defaultPayNowHtml;

        if (processingState) {
            processingState.innerHTML = `
                <div class="ciit-payment-processing-icon" aria-hidden="true">
                    <span class="ciit-payment-spinner"></span>
                    <i class="bi bi-credit-card-2-front"></i>
                </div>
                <strong>Verifying payment</strong>
                <span>Checking the payment status securely with Razorpay...</span>
                <div class="ciit-payment-processing-steps" aria-label="Payment processing status">
                    <div class="ciit-payment-processing-step" data-payment-step="gateway"><i class="bi bi-check-lg" aria-hidden="true"></i><span>Connecting to payment gateway...</span></div>
                    <div class="ciit-payment-processing-step" data-payment-step="verify"><i class="bi bi-check-lg" aria-hidden="true"></i><span>Verifying payment details...</span></div>
                    <div class="ciit-payment-processing-step" data-payment-step="process"><i class="bi bi-check-lg" aria-hidden="true"></i><span>Confirming course access...</span></div>
                </div>`;
        }
    }

    const completeCoursePayment = async () => {
        if (!merchantOrderId) {
            showFailure("We could not identify the payment order. Please start the payment again.");
            return;
        }

        showProcessing("Checking the payment result securely with Razorpay...");

        try {
            const returnUrl = `/Courses/PhonePeReturn?merchantOrderId=${encodeURIComponent(merchantOrderId)}`;
            const response = await fetch(returnUrl, {
                method: "GET",
                credentials: "same-origin",
                headers: {
                    Accept: "application/json"
                }
            });

            let result = null;
            try {
                result = await response.json();
            } catch {
                result = null;
            }

            if (!response.ok) {
                throw new Error(result?.message || "Payment verification could not be completed.");
            }

            if (result?.state === "COMPLETED") {
                clearPagePaymentMessage();

                if (processingState) processingState.hidden = true;
                if (successState) successState.hidden = false;

                showPaymentOverlay("Payment verified", "Your course access is ready. Opening your learning page...", "success");

                window.setTimeout(() => {
                    window.location.assign(`/Courses/Course/${result.courseId}`);
                }, 650);
                return;
            }

            if (result?.state === "FAILED") {
                showFailure(result.message || "Payment failed. No course access was granted.");
                return;
            }

            const pendingMessage = result?.message || "Payment is still being verified. No course access has been granted yet.";
            showPaymentOverlay("Payment still processing", pendingMessage, "processing", {
                label: "Check payment status again",
                handler: completeCoursePayment
            });
        } catch (error) {
            console.error("Razorpay payment verification error:", error);
            const message = error instanceof Error
                ? error.message
                : "We could not confirm the payment yet. Your order remains pending.";

            showPaymentOverlay("Verification needs another check", message, "processing", {
                label: "Retry verification",
                handler: completeCoursePayment
            });
        }
    };

    const openRazorpayCheckout = async () => {
        clearPagePaymentMessage();
        removePaymentOverlay();

        paymentTrigger.disabled = true;
        paymentTrigger.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Opening Razorpay...';

        if (paymentButton) {
            paymentButton.disabled = true;
            paymentButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Opening Razorpay...';
        }

        try {
            const checkoutUrl = await getCheckoutUrl();
            await loadRazorpayCheckout();

            if (!window.CiitRazorpayCheckout?.transact) {
                throw new Error("Razorpay Checkout is not available yet. Please try again.");
            }

            window.CiitRazorpayCheckout.transact({
                tokenUrl: checkoutUrl,
                type: "IFRAME",
                callback: function (response) {
                    if (response === "CONCLUDED") {
                        completeCoursePayment();
                    }

                    if (response === "USER_CANCEL") {
                        resetPaymentForm();
                    }
                }
            });
        } catch (error) {
            console.error("Razorpay checkout error:", error);
            showFailure(error instanceof Error ? error.message : "Unable to start Razorpay checkout.");
        }
    };

    paymentTrigger.addEventListener("click", function (event) {
        event.preventDefault();
        resetPaymentForm();
        openRazorpayCheckout();
    });

    paymentForm.addEventListener("submit", function (event) {
        event.preventDefault();
        event.stopPropagation();
        resetPaymentForm();
        openRazorpayCheckout();
    });
});


document.addEventListener("DOMContentLoaded", function () {
    const learningPage = document.querySelector(".ciit-learning-page");

    if (learningPage) {
        const panelButtons = Array.from(document.querySelectorAll("[data-learning-page]"));
        const panelContents = Array.from(document.querySelectorAll("[data-learning-panel-content]"));
        const extraPanels = document.querySelector(".ciit-learning-extra-panels");
        // Keep the selected tab, pagination and scroll position across topic/content URLs
        // within the same course, while isolating state between different courses.
        const learningStateKey = "ciit-learning-state:" + window.location.pathname;
        let savedLearningState = {};
        try {
            savedLearningState = JSON.parse(window.sessionStorage.getItem(learningStateKey) || "{}");
        } catch (error) {
            savedLearningState = {};
        }
        if (!savedLearningState || typeof savedLearningState !== "object") savedLearningState = {};

        let activeContentPage = savedLearningState.contentPage
            || (document.querySelector('[data-learning-content-page="video"]') ? "video" : "overview");

        function saveLearningState() {
            try {
                savedLearningState.panel = savedLearningState.panel || "notes";
                savedLearningState.contentPage = activeContentPage;
                window.sessionStorage.setItem(learningStateKey, JSON.stringify(savedLearningState));
            } catch (error) {
                // Learning navigation should remain usable when browser storage is unavailable.
            }
        }

        if ("scrollRestoration" in window.history) {
            window.history.scrollRestoration = "manual";
        }

        window.addEventListener("pagehide", function () {
            savedLearningState.scrollY = window.scrollY;
            saveLearningState();
        });

        function buildPager(container, pageCount, label, onChange, options) {
            if (pageCount <= 1) return null;

            const pager = document.createElement("nav");
            pager.className = "ciit-learning-subpage-pager";
            pager.setAttribute("aria-label", label);

            const previous = document.createElement("button");
            previous.type = "button";
            previous.className = "ciit-learning-subpage-arrow";
            previous.innerHTML = '<i class="bi bi-chevron-left" aria-hidden="true"></i><span>Previous</span>';
            previous.addEventListener("click", function () {
                onChange("previous");
            });

            const numbers = document.createElement("div");
            numbers.className = "ciit-learning-subpage-numbers";

            const next = document.createElement("button");
            next.type = "button";
            next.className = "ciit-learning-subpage-arrow";
            next.innerHTML = '<span>Next</span><i class="bi bi-chevron-right" aria-hidden="true"></i>';
            next.addEventListener("click", function () {
                onChange("next");
            });

            pager.append(previous, numbers, next);

            let markRead = null;
            if (options && options.markRead) {
                markRead = document.createElement("button");
                markRead.type = "button";
                markRead.className = "ciit-learning-subpage-mark-read";
                markRead.textContent = "Mark as Read";
                markRead.addEventListener("click", function () {
                    options.onMarkRead();
                });
                pager.appendChild(markRead);
            }

            const sectionHeading = container.querySelector(":scope > h5, :scope > h4, :scope > h3");
            if (sectionHeading) {
                container.insertBefore(pager, sectionHeading.nextSibling);
            } else {
                container.insertBefore(pager, container.firstChild);
            }

            pager.update = function (activeIndex) {
                numbers.replaceChildren();
                for (let index = 0; index < pageCount; index += 1) {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "ciit-learning-subpage-number";
                    button.textContent = String(index + 1);
                    button.setAttribute("aria-label", "Page " + (index + 1) + " of " + pageCount);
                    button.setAttribute("aria-current", index === activeIndex ? "page" : "false");
                    button.classList.toggle("is-active", index === activeIndex);
                    button.addEventListener("click", function () {
                        onChange(index);
                    });
                    numbers.appendChild(button);
                }

                previous.disabled = activeIndex === 0;
                next.disabled = activeIndex === pageCount - 1;

                if (markRead && options && typeof options.getLastReadIndex === "function") {
                    const lastReadIndex = options.getLastReadIndex();
                    const isRead = activeIndex <= lastReadIndex;
                    markRead.textContent = isRead ? "Read" : "Mark as Read";
                    markRead.classList.toggle("is-read", isRead);
                }
            };

            return pager;
        }

        function paginateItems(parent, items, pageSize, label, options) {
            if (!parent || items.length <= pageSize || parent.dataset.ciitPaginated === "true") {
                return;
            }

            const firstItem = items[0];
            const pages = [];
            for (let offset = 0; offset < items.length; offset += pageSize) {
                const page = document.createElement("div");
                page.className = "ciit-learning-subpage";
                page.setAttribute("data-learning-subpage", "");
                parent.insertBefore(page, firstItem);
                pages.push(page);
            }

            items.forEach(function (item, index) {
                pages[Math.floor(index / pageSize)].appendChild(item);
            });

            const pageStateKey = [
                parent.closest(".ciit-learning-chapter-assessment")?.id || "",
                parent.id || "",
                Array.from(parent.classList).sort().join("."),
                label
            ].join(":");
            const readProgressKey = "ciit-learning-read:" + window.location.pathname + ":" + pageStateKey;
            const supportsReadProgress = Boolean(options && options.markRead);
            function getLastReadIndex() {
                if (!supportsReadProgress) return -1;
                try {
                    const storedValue = window.localStorage.getItem(readProgressKey);
                    if (storedValue === null) return -1;
                    const storedIndex = Number(storedValue);
                    return Number.isInteger(storedIndex) && storedIndex >= 0
                        ? storedIndex
                        : -1;
                } catch (error) {
                    return -1;
                }
            }

            let activeIndex = Number.isInteger(savedLearningState.pages?.[pageStateKey])
                ? savedLearningState.pages[pageStateKey]
                : 0;
            const lastReadIndex = getLastReadIndex();
            if (supportsReadProgress && lastReadIndex >= 0) {
                activeIndex = Math.min(lastReadIndex + 1, pages.length - 1);
            }
            activeIndex = Math.max(0, Math.min(activeIndex, pages.length - 1));

            function showPage(nextIndex) {
                if (nextIndex === "previous") nextIndex = activeIndex - 1;
                if (nextIndex === "next") nextIndex = activeIndex + 1;
                if (!Number.isInteger(nextIndex) || nextIndex < 0 || nextIndex >= pages.length) return;

                activeIndex = nextIndex;
                pages.forEach(function (page, index) {
                    page.hidden = index !== activeIndex;
                    page.classList.toggle("is-entering", index === activeIndex);
                });
                pager.update(activeIndex);
                if (!savedLearningState.pages) savedLearningState.pages = {};
                savedLearningState.pages[pageStateKey] = activeIndex;
                saveLearningState();
                if (options && typeof options.onChange === "function") {
                    options.onChange(activeIndex);
                }
            }

            const pagerOptions = supportsReadProgress
                ? {
                    markRead: true,
                    getLastReadIndex: getLastReadIndex,
                    onMarkRead: function () {
                        const currentReadIndex = activeIndex;
                        try {
                            const latestReadIndex = getLastReadIndex();
                            window.localStorage.setItem(
                                readProgressKey,
                                String(Math.max(latestReadIndex, currentReadIndex))
                            );
                        } catch (error) {
                            // Continue navigation even if browser storage is unavailable.
                        }

                        if (activeIndex < pages.length - 1) {
                            showPage(activeIndex + 1);
                        } else {
                            showPage(activeIndex);
                        }
                    }
                }
                : null;
            const pager = buildPager(parent, pages.length, label, showPage, pagerOptions);
            if (!pager) return;
            parent.dataset.ciitPaginated = "true";
            showPage(activeIndex);

            return {
                showPage: showPage,
                getActiveIndex: function () { return activeIndex; },
                pages: pages
            };
        }

        function showLearningPanel(name, options) {
            const settings = options || {};
            const initializePagination = pendingPagination.get(name);
            if (initializePagination) {
                pendingPagination.delete(name);
                initializePagination();
            }

            panelContents.forEach(function (panel) {
                panel.hidden = panel.dataset.learningPanelContent !== name;
            });
            panelButtons.forEach(function (button) {
                const active = button.dataset.learningPage === name;
                button.classList.toggle("is-active", active);
                button.setAttribute("aria-current", active ? "page" : "false");
            });

            const contentPager = document.querySelector("[data-learning-content-pager]");
            if (contentPager) contentPager.hidden = name !== "content";

            if (name === "content") {
                document.querySelectorAll("[data-learning-content-page]").forEach(function (panel) {
                    panel.hidden = panel.dataset.learningContentPage !== activeContentPage;
                });
                document.querySelectorAll("[data-learning-content-page-button]").forEach(function (button) {
                    const active = button.dataset.learningContentPageButton === activeContentPage;
                    button.classList.toggle("is-active", active);
                    button.setAttribute("aria-current", active ? "page" : "false");
                });

                const videoPanel = document.querySelector(".ciit-learning-video-section");
                if (videoPanel) {
                    const muxPlayer = videoPanel.querySelector(".ciit-mux-watch-player");
                    if (muxPlayer && !muxPlayer.dataset.ciitMuxReady) {
                        const existingScript = document.querySelector("script[data-ciit-mux-player]");
                        if (!existingScript) {
                            const playerScript = document.createElement("script");
                            playerScript.src = "/js/mux-video-player.js?v=3";
                            playerScript.async = true;
                            playerScript.dataset.ciitMuxPlayer = "true";
                            document.body.appendChild(playerScript);
                        }
                    }
                }
            }

            savedLearningState.panel = name;
            saveLearningState();

            if (settings.scroll) {
                const target = document.querySelector(".ciit-learning-main-card");
                if (target) target.scrollIntoView({ behavior: "smooth", block: "start" });
            }
        }

        panelButtons.forEach(function (button) {
            button.addEventListener("click", function () {
                showLearningPanel(button.dataset.learningPage, { scroll: true });
            });
        });

        document.querySelectorAll("[data-learning-panel='notes']").forEach(function (button) {
            button.addEventListener("click", function () {
                showLearningPanel("notes", { scroll: true });
            });
        });

        document.querySelectorAll(".ciit-learning-assessment-links a[data-learning-panel='assessment']").forEach(function (link) {
            link.addEventListener("click", function () {
                showLearningPanel("assessment", { scroll: false });
                const targetId = link.getAttribute("href");
                const target = targetId && document.querySelector(targetId);
                if (target) {
                    window.setTimeout(function () {
                        target.scrollIntoView({ behavior: "smooth", block: "start" });
                    }, 100);
                }
            });
        });

        // Each content resource has its own short page. The top-level Notes /
        // Content / Assessments navigation remains independent.
        const contentContainer = document.querySelector(".ciit-learning-main-card");
        if (contentContainer) {
            const overview = contentContainer.querySelector('[data-learning-content-page="overview"]');
            const contentPager = document.createElement("nav");
            contentPager.className = "ciit-learning-content-pager";
            contentPager.setAttribute("aria-label", "Content resources");
            contentPager.dataset.learningPanelContent = "content";
            contentPager.dataset.learningContentPager = "";
            contentPager.hidden = true;

            const resourcePages = Array.from(contentContainer.querySelectorAll("[data-learning-content-page]"));
            const pageLabels = { overview: "Overview", video: "Video", slides: "Slides" };
            resourcePages.forEach(function (page) {
                page.hidden = page.dataset.learningContentPage !== activeContentPage;
            });

            const contentButtons = document.createElement("div");
            contentButtons.className = "ciit-learning-content-pager-buttons";
            resourcePages.forEach(function (page) {
                const key = page.dataset.learningContentPage;
                const button = document.createElement("button");
                button.type = "button";
                button.dataset.learningContentPageButton = key;
                button.textContent = pageLabels[key] || key;
                button.addEventListener("click", function () {
                    activeContentPage = key;
                    savedLearningState.contentPage = key;
                    saveLearningState();
                    showLearningPanel("content");
                    const activePage = resourcePages.find(function (item) {
                        return item.dataset.learningContentPage === activeContentPage;
                    });
                    if (activePage) activePage.scrollIntoView({ behavior: "smooth", block: "nearest" });
                });
                contentButtons.appendChild(button);
            });
            contentPager.appendChild(contentButtons);
            contentPager.hidden = resourcePages.length <= 1;
            if (overview) contentContainer.insertBefore(contentPager, overview);
            else contentContainer.insertBefore(contentPager, contentContainer.firstChild);
        }

        const pendingPagination = new Map([
            ["notes", function () {
                document.querySelectorAll(".ciit-learning-notes-content").forEach(function (notes) {
                    let contentContainer = notes;
                    let contentBlocks = Array.from(contentContainer.children);

                    // Paginate imported wrapper content by its original semantic blocks.
                    while (contentBlocks.length <= 20) {
                        const nestedContainer = contentBlocks.find(function (block) {
                            return block.children.length > 20
                                && !block.matches("pre, table, ul, ol, figure, blockquote");
                        });
                        if (!nestedContainer) break;
                        contentContainer = nestedContainer;
                        contentBlocks = Array.from(contentContainer.children);
                    }

                    paginateItems(contentContainer, contentBlocks, 40, "Notes pages", { markRead: true });
                });
            }],
            ["assessment", function () {
                document.querySelectorAll(".ciit-learning-chapter-assessment .ciit-learning-assessment-subsection").forEach(function (subsection) {
                    paginateItems(
                        subsection,
                        Array.from(subsection.querySelectorAll(":scope > .ciit-learning-assessment-question")),
                        5,
                        subsection.querySelector("h5")?.textContent?.trim() + " pages"
                    );
                });
            }]
        ]);

        document.querySelectorAll("[data-video-src]").forEach(function (button) {
            button.addEventListener("click", function () {
                const frame = button.parentElement && button.parentElement.querySelector("video[data-deferred-video]");
                if (!frame) return;
                const source = document.createElement("source");
                source.src = button.dataset.videoSrc;
                if (button.dataset.videoType) source.type = button.dataset.videoType;
                frame.appendChild(source);
                frame.load();
                button.hidden = true;
                frame.play().catch(function () { });
            });
        });

        const validPanels = panelContents
            .map(function (panel) { return panel.dataset.learningPanelContent; })
            .filter(Boolean);
        const initialPanel = validPanels.includes(savedLearningState.panel)
            ? savedLearningState.panel
            : "notes";
        showLearningPanel(initialPanel);

        const savedScrollY = savedLearningState.scrollY;
        if (Number.isFinite(savedScrollY) && savedScrollY > 0) {
            window.requestAnimationFrame(function () {
                if (window.scrollY === 0) {
                    window.scrollTo({ top: savedScrollY, left: 0, behavior: "instant" });
                }
            });
        }
    }
});
