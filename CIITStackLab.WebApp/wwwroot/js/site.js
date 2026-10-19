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

    const showPagePaymentMessage = (message, type = "danger") => {
        if (!message) return;

        const existing = document.getElementById("ciit-payment-page-message");
        if (existing) {
            existing.remove();
        }

        const container = document.createElement("div");
        container.id = "ciit-payment-page-message";
        container.className = "container mt-3";

        const alert = document.createElement("div");
        alert.className = `alert alert-${type} d-flex align-items-start gap-2`;
        alert.setAttribute("role", "alert");
        alert.setAttribute("aria-live", "polite");
        alert.innerHTML = '<i class="bi bi-credit-card mt-1" aria-hidden="true"></i><span></span>';
        alert.querySelector("span").textContent = message;

        container.appendChild(alert);
        const main = document.querySelector("main");
        const courseSection = document.querySelector(".ciit-course-details-section");

        if (main && courseSection) {
            main.insertBefore(container, courseSection);
        } else if (main) {
            main.prepend(container);
        }
    };

    const clearPagePaymentMessage = () => {
        document.getElementById("ciit-payment-page-message")?.remove();
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

        if (!processingState) return;
        const spans = processingState.querySelectorAll("span:not(.ciit-payment-spinner)");
        if (spans.length > 0 && message) {
            spans[spans.length - 1].textContent = message;
        }
        processingState.hidden = false;
        if (formState) formState.hidden = true;
        if (successState) successState.hidden = true;
    };

    const showFailure = (message) => {
        showPagePaymentMessage(message || "Payment could not be completed. No course access was granted.", "danger");

        if (!processingState) {
            window.alert(message);
            return;
        }

        processingState.hidden = false;
        if (formState) formState.hidden = true;
        if (successState) successState.hidden = true;

        const icon = processingState.querySelector(".ciit-payment-processing-icon");
        const title = processingState.querySelector("strong");
        const description = processingState.querySelector(".ciit-payment-processing-icon + strong + span");
        const steps = processingState.querySelector(".ciit-payment-processing-steps");

        if (icon) {
            icon.innerHTML = '<i class="bi bi-x-lg ciit-payment-failure-mark" aria-hidden="true"></i>';
        }
        if (title) {
            title.textContent = "Payment Failed";
        }
        if (description) {
            description.textContent = message;
        }
        if (steps) {
            steps.innerHTML = '<button type="button" class="btn btn-outline-primary btn-sm mt-3" id="ciit-payment-retry-button">Try payment again</button>';
        }

        const retryButton = document.getElementById("ciit-payment-retry-button");
        if (retryButton) {
            retryButton.addEventListener("click", resetPaymentForm, { once: true });
        }
    };

    function resetPaymentForm() {
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

                const successText = successState?.querySelector(".ciit-payment-success-note");
                if (successText) {
                    successText.textContent = "Payment verified successfully. Your course access is ready.";
                }

                const successSmall = successState?.querySelector("small");
                if (successSmall) {
                    successSmall.textContent = "Opening your course...";
                }

                processingState.hidden = true;
                successState.hidden = false;

                window.setTimeout(() => {
                    window.location.assign(`/Courses/Course/${result.courseId}`);
                }, 900);
                return;
            }

            if (result?.state === "FAILED") {
                showFailure(result.message || "Payment failed. No course access was granted.");
                return;
            }

            const pendingMessage = result?.message || "Payment is still being verified. No course access has been granted yet.";
            showPagePaymentMessage(pendingMessage, "warning");
            showProcessing(pendingMessage);
            const steps = processingState.querySelector(".ciit-payment-processing-steps");
            if (steps) {
                steps.innerHTML = '<button type="button" class="btn btn-outline-primary btn-sm mt-3" id="ciit-payment-pending-retry">Check payment status again</button>';
                const retry = document.getElementById("ciit-payment-pending-retry");
                if (retry) {
                    retry.addEventListener("click", completePhonePePayment, { once: true });
                }
            }
        } catch (error) {
            console.error("PhonePe payment verification error:", error);
            const message = error instanceof Error
                ? error.message
                : "We could not confirm the payment yet. Your order remains pending.";
            showPagePaymentMessage(message, "warning");
            showProcessing(message);

            const steps = processingState?.querySelector(".ciit-payment-processing-steps");
            if (steps) {
                steps.innerHTML = '<button type="button" class="btn btn-outline-primary btn-sm mt-3" id="ciit-payment-verify-retry">Retry verification</button>';
                const retry = document.getElementById("ciit-payment-verify-retry");
                if (retry) {
                    retry.addEventListener("click", completePhonePePayment, { once: true });
                }
            }
        }
    };

    const openPhonePeCheckout = async () => {
        clearPagePaymentMessage();

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
