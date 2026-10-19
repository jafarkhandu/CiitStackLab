document.addEventListener("DOMContentLoaded", function () {
    const topicsScroll = document.getElementById("ciitTopicsScroll");
    const topicsPrev = document.getElementById("topicsPrev");
    const topicsNext = document.getElementById("topicsNext");

    if (topicsScroll && topicsPrev && topicsNext) {
        const amount = 350;
        topicsPrev.addEventListener("click", function () {
            topicsScroll.scrollBy({ left: -amount, behavior: "smooth" });
        });
        topicsNext.addEventListener("click", function () {
            topicsScroll.scrollBy({ left: amount, behavior: "smooth" });
        });
    }

    const slides = document.querySelectorAll(".ciit-hero-slide");
    const dots = document.querySelectorAll(".ciit-slider-dot");

    if (slides.length && dots.length) {
        let current = 0;
        let timer;

        function show(index) {
            slides.forEach((slide) => slide.classList.remove("active"));
            dots.forEach((dot) => dot.classList.remove("active"));

            if (slides[index] && dots[index]) {
                slides[index].classList.add("active");
                dots[index].classList.add("active");
            }
        }

        function restart() {
            clearInterval(timer);
            timer = setInterval(function () {
                current = (current + 1) % slides.length;
                show(current);
            }, 4000);
        }

        dots.forEach((dot, index) => {
            dot.addEventListener("click", function () {
                current = index;
                show(current);
                restart();
            });
        });

        show(0);
        restart();
    }
});

document.addEventListener("DOMContentLoaded", function () {
    const hubLink = document.querySelector("[data-learning-hub-transition]");
    const transition = document.getElementById("ciitHubTransition");

    if (!hubLink || !transition) {
        return;
    }

    hubLink.addEventListener("click", function (event) {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        event.preventDefault();

        if (hubLink.dataset.transitioning === "true") {
            return;
        }

        hubLink.dataset.transitioning = "true";
        hubLink.setAttribute("aria-disabled", "true");
        transition.classList.remove("is-complete");
        transition.classList.add("is-active");
        transition.setAttribute("aria-hidden", "false");
        document.body.classList.add("ciit-hub-transitioning");

        const progress = transition.querySelector(".ciit-hub-transition-progress span");
        const caption = transition.querySelector(".ciit-hub-transition-caption");
        const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        const loadingDuration = reducedMotion ? 80 : 1500;

        if (progress) {
            progress.style.transitionDuration = loadingDuration + "ms";
            progress.style.width = "0%";
            progress.getBoundingClientRect();
            progress.style.width = "100%";
        }

        if (caption) {
            caption.textContent = "Loading your courses and learning progress…";
        }

        window.setTimeout(function () {
            transition.classList.add("is-complete");

            if (caption) {
                caption.textContent = "Your learning space is ready";
            }

            window.setTimeout(function () {
                window.location.assign(hubLink.href);
            }, reducedMotion ? 0 : 260);
        }, loadingDuration);
    });

    window.addEventListener("pageshow", function () {
        transition.classList.remove("is-active");
        transition.setAttribute("aria-hidden", "true");
        document.body.classList.remove("ciit-hub-transitioning");
        hubLink.dataset.transitioning = "false";
        hubLink.removeAttribute("aria-disabled");
    });
});

document.addEventListener("DOMContentLoaded", function () {
    const paymentForm = document.getElementById("ciit-demo-payment-form");
    const paymentTrigger = document.querySelector('[data-bs-target="#coursePaymentModal"]');

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
    const phonePeScriptUrl = "https://mercury.phonepe.com/web/bundle/checkout.js";
    let phonePeScriptPromise;
    let merchantOrderId;
    let paymentOverlay;

    paymentTrigger.removeAttribute("data-bs-toggle");
    paymentTrigger.removeAttribute("data-bs-target");
    paymentTrigger.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with PhonePe';

    if (modalEyebrow) {
        modalEyebrow.textContent = "Secure PhonePe checkout";
    }

    if (modalSubtitle) {
        modalSubtitle.textContent = "Complete your payment securely. Course access is granted only after server-side verification.";
    }

    if (paymentNote) {
        paymentNote.textContent = "PhonePe checkout opens securely on this page. Course access is granted only after server-side payment verification.";
    }

    if (paymentButton) {
        paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with PhonePe';
    }

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

    const loadPhonePeCheckout = () => {
        if (window.PhonePeCheckout?.transact) {
            return Promise.resolve();
        }

        if (phonePeScriptPromise) {
            return phonePeScriptPromise;
        }

        phonePeScriptPromise = new Promise((resolve, reject) => {
            const existingScript = document.querySelector(`script[src="${phonePeScriptUrl}"]`);

            if (existingScript) {
                existingScript.addEventListener("load", resolve, { once: true });
                existingScript.addEventListener("error", reject, { once: true });
                return;
            }

            const script = document.createElement("script");
            script.src = phonePeScriptUrl;
            script.async = true;
            script.onload = resolve;
            script.onerror = () => reject(new Error("PhonePe Checkout could not be loaded."));
            document.head.appendChild(script);
        });

        return phonePeScriptPromise;
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
            throw new Error("PhonePe did not return the required checkout details.");
        }

        return result.checkoutUrl;
    };

    const showProcessing = (message) => {
        clearPagePaymentMessage();
        showPaymentOverlay("Verifying your payment", message || "Checking the payment status securely with PhonePe...");

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
            paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with PhonePe';
        }
    };

    function resetPaymentForm() {
        removePaymentOverlay();
        clearPagePaymentMessage();

        if (processingState) processingState.hidden = true;
        if (successState) successState.hidden = true;
        if (formState) formState.hidden = false;

        if (paymentButton) {
            paymentButton.disabled = false;
            paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with PhonePe';
        }

        if (processingState) {
            processingState.innerHTML = `
                <div class="ciit-payment-processing-icon" aria-hidden="true">
                    <span class="ciit-payment-spinner"></span>
                    <i class="bi bi-credit-card-2-front"></i>
                </div>
                <strong>Verifying payment</strong>
                <span>Checking the payment status securely with PhonePe...</span>
                <div class="ciit-payment-processing-steps" aria-label="Payment processing status">
                    <div class="ciit-payment-processing-step" data-payment-step="gateway"><i class="bi bi-check-lg" aria-hidden="true"></i><span>Connecting to payment gateway...</span></div>
                    <div class="ciit-payment-processing-step" data-payment-step="verify"><i class="bi bi-check-lg" aria-hidden="true"></i><span>Verifying payment details...</span></div>
                    <div class="ciit-payment-processing-step" data-payment-step="process"><i class="bi bi-check-lg" aria-hidden="true"></i><span>Confirming course access...</span></div>
                </div>`;
        }
    }

    const completePhonePePayment = async () => {
        if (!merchantOrderId) {
            showFailure("We could not identify the payment order. Please start the payment again.");
            return;
        }

        showProcessing("Checking the payment result securely with PhonePe...");

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
                handler: completePhonePePayment
            });
        } catch (error) {
            console.error("PhonePe payment verification error:", error);
            const message = error instanceof Error
                ? error.message
                : "We could not confirm the payment yet. Your order remains pending.";

            showPaymentOverlay("Verification needs another check", message, "processing", {
                label: "Retry verification",
                handler: completePhonePePayment
            });
        }
    };

    const openPhonePeCheckout = async () => {
        clearPagePaymentMessage();
        removePaymentOverlay();

        if (paymentButton) {
            paymentButton.disabled = true;
            paymentButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Opening PhonePe...';
        }

        try {
            const checkoutUrl = await getCheckoutUrl();
            await loadPhonePeCheckout();

            if (!window.PhonePeCheckout?.transact) {
                throw new Error("PhonePe Checkout is not available yet. Please try again.");
            }

            window.PhonePeCheckout.transact({
                tokenUrl: checkoutUrl,
                type: "IFRAME",
                callback: function (response) {
                    if (response === "CONCLUDED") {
                        completePhonePePayment();
                    }

                    if (response === "USER_CANCEL") {
                        resetPaymentForm();
                    }
                }
            });
        } catch (error) {
            console.error("PhonePe checkout error:", error);
            showFailure(error instanceof Error ? error.message : "Unable to start PhonePe checkout.");
        }
    };

    paymentTrigger.addEventListener("click", function (event) {
        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();
        resetPaymentForm();
        openPhonePeCheckout();
    }, true);
});
