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
    const phonePeScriptUrl = "https://mercury.phonepe.com/web/bundle/checkout.js";
    let phonePeScriptPromise;

    paymentTrigger.removeAttribute("data-bs-toggle");
    paymentTrigger.removeAttribute("data-bs-target");
    paymentTrigger.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with PhonePe';

    if (paymentNote) {
        paymentNote.textContent = "PhonePe checkout will open securely on this page. Course access is granted only after server-side payment verification.";
    }

    if (paymentButton) {
        paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with PhonePe';
    }

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

        if (!result?.checkoutUrl) {
            throw new Error("PhonePe did not return a checkout URL.");
        }

        return result.checkoutUrl;
    };

    const openPhonePeCheckout = async () => {
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
                        window.location.reload();
                    }

                    if (response === "USER_CANCEL") {
                        if (paymentButton) {
                            paymentButton.disabled = false;
                            paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with PhonePe';
                        }
                    }
                }
            });
        } catch (error) {
            console.error("PhonePe checkout error:", error);
            window.alert(error instanceof Error ? error.message : "Unable to start PhonePe checkout.");

            if (paymentButton) {
                paymentButton.disabled = false;
                paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with PhonePe';
            }
        }
    };

    paymentTrigger.addEventListener("click", function (event) {
        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();
        openPhonePeCheckout();
    }, true);
});
