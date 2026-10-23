document.addEventListener("DOMContentLoaded",function(){const t=document.getElementById("ciitTopicsScroll"),p=document.getElementById("topicsPrev"),n=document.getElementById("topicsNext");if(t&&p&&n){const a=350;p.addEventListener("click",function(){t.scrollBy({left:-a,behavior:"smooth"})});n.addEventListener("click",function(){t.scrollBy({left:a,behavior:"smooth"})})}const s=document.querySelectorAll(".ciit-hero-slide"),d=document.querySelectorAll(".ciit-slider-dot");if(!s.length||!d.length)return;let c=0,timer;function show(i){s.forEach(x=>x.classList.remove("active"));d.forEach(x=>x.classList.remove("active"));if(s[i]&&d[i]){s[i].classList.add("active");d[i].classList.add("active")}}function restart(){clearInterval(timer);timer=setInterval(function(){c=(c+1)%s.length;show(c)},4000)}d.forEach(function(x,i){x.addEventListener("click",function(){c=i;show(c);restart()})});show(0);restart()});

document.addEventListener("DOMContentLoaded", function () {
    if (!document.body.classList.contains("ciit-course-environment-body")) return;
    if (document.querySelector(".ciit-learning-page")) return;
    const hasVideoResource = document.querySelector('.ciit-learning-video-frame iframe[src], a[href*="/uploads/videos/"], .ciit-notes-reader-media a, .ciit-mux-watch-player');
    if (!hasVideoResource || document.querySelector("script[data-ciit-mux-player]")) return;
    const playerScript = document.createElement("script");
    playerScript.src = "/js/mux-video-player.js?v=3";
    playerScript.async = true;
    playerScript.dataset.ciitMuxPlayer = "true";
    document.body.appendChild(playerScript);
});

document.addEventListener("DOMContentLoaded", function () {
    const hubLink = document.querySelector("[data-learning-hub-transition]");
    const transition = document.getElementById("ciitHubTransition");
    if (!hubLink || !transition) return;
    hubLink.addEventListener("click", function (event) {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        if (hubLink.dataset.transitioning === "true") return;
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
        if (caption) caption.textContent = "Loading your courses and learning progress…";
        window.setTimeout(function () {
            transition.classList.add("is-complete");
            if (caption) caption.textContent = "Your learning space is ready";
            window.setTimeout(function () { window.location.assign(hubLink.href); }, reducedMotion ? 0 : 260);
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
        if (!alert) return null;
        const message = alert.querySelector("span")?.textContent?.trim();
        alert.closest(".container")?.remove();
        return message || null;
    };
    const getToastType = message => {
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
        const icon = type === "success" ? "bi-check-circle-fill" : type === "warning" ? "bi-exclamation-circle-fill" : type === "info" ? "bi-info-circle-fill" : "bi-x-circle-fill";
        toast.innerHTML = `<span class="ciit-payment-toast-icon" aria-hidden="true"><i class="bi ${icon}"></i></span><span class="ciit-payment-toast-content"><strong>${type === "danger" ? "Payment Failed" : type === "success" ? "Payment Successful" : "Payment Update"}</strong><span></span></span><button type="button" class="ciit-payment-toast-close" aria-label="Close notification">&times;</button>`;
        toast.querySelector(".ciit-payment-toast-content > span").textContent = message;
        toast.querySelector(".ciit-payment-toast-close").addEventListener("click", () => toast.remove());
        document.body.appendChild(toast);
        requestAnimationFrame(() => toast.classList.add("is-visible"));
        toastTimer = window.setTimeout(() => toast.remove(), 5000);
    };
    const serverPaymentMessage = getServerPaymentMessage();
    if (serverPaymentMessage) showPaymentToast(serverPaymentMessage, getToastType(serverPaymentMessage));
    if (!paymentForm || !paymentTrigger) return;
    const paymentButton = document.getElementById("ciit-demo-payment-button");
    const paymentNote = paymentForm.nextElementSibling;
    const processingState = document.getElementById("ciit-payment-processing-state");
    const successState = document.getElementById("ciit-payment-success-state");
    const formState = document.getElementById("ciit-payment-form-state");
    const modalEyebrow = document.querySelector(".ciit-payment-modal-eyebrow");
    const modalSubtitle = document.querySelector(".ciit-payment-modal-subtitle");
    let paymentOverlay;
    paymentTrigger.removeAttribute("data-bs-toggle");
    paymentTrigger.removeAttribute("data-bs-target");
    paymentTrigger.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with Razorpay';
    if (modalEyebrow) modalEyebrow.textContent = "Secure Razorpay checkout";
    if (modalSubtitle) modalSubtitle.textContent = "Complete your payment securely. Course access is granted only after server-side verification.";
    if (paymentNote) paymentNote.textContent = "Razorpay checkout opens securely on this page. Course access is granted only after server-side payment verification.";
    if (paymentButton) paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with Razorpay';
    const loadRazorpay = () => new Promise((resolve, reject) => {
        if (window.Razorpay) return resolve();
        const existing = document.querySelector('script[src="https://checkout.razorpay.com/v1/checkout.js"]');
        if (existing) { existing.addEventListener("load", resolve, { once: true }); existing.addEventListener("error", reject, { once: true }); return; }
        const script = document.createElement("script");
        script.src = "https://checkout.razorpay.com/v1/checkout.js";
        script.async = true;
        script.onload = resolve;
        script.onerror = () => reject(new Error("Razorpay Checkout could not be loaded."));
        document.head.appendChild(script);
    });
    const removePaymentOverlay = () => { paymentOverlay?.remove(); paymentOverlay = null; };
    const showPaymentOverlay = (title, message) => {
        removePaymentOverlay();
        const overlay = document.createElement("div");
        overlay.className = "ciit-payment-overlay ciit-payment-overlay-processing is-visible";
        overlay.setAttribute("role", "dialog");
        overlay.setAttribute("aria-modal", "true");
        overlay.innerHTML = `<div class="ciit-payment-overlay-card"><div class="ciit-payment-overlay-spinner" aria-hidden="true"><span></span><i class="bi bi-shield-check"></i></div><strong></strong><span class="ciit-payment-overlay-message"></span><div class="ciit-payment-overlay-dots" aria-hidden="true"><span></span><span></span><span></span></div></div>`;
        overlay.querySelector("strong").textContent = title;
        overlay.querySelector(".ciit-payment-overlay-message").textContent = message;
        document.body.appendChild(overlay);
        paymentOverlay = overlay;
    };
    const setProcessingState = active => {
        if (processingState) processingState.hidden = !active;
        if (formState) formState.hidden = active;
        if (successState) successState.hidden = true;
    };
    const showFailure = message => {
        removePaymentOverlay();
        setProcessingState(false);
        showPaymentToast(message || "Payment could not be completed. No course access was granted.", "danger");
        if (paymentButton) { paymentButton.disabled = false; paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Pay with Razorpay'; }
    };
    const verifyPayment = async (merchantOrderId, response) => {
        const token = paymentForm.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const verifyResponse = await fetch("/Payments/Razorpay/Verify", {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/json", "RequestVerificationToken": token || "" },
            body: JSON.stringify({ merchantOrderId, razorpayOrderId: response.razorpay_order_id, razorpayPaymentId: response.razorpay_payment_id, razorpaySignature: response.razorpay_signature })
        });
        const result = await verifyResponse.json().catch(() => ({}));
        if (!verifyResponse.ok || !result.success) throw new Error(result.message || "Payment verification failed.");
        return result;
    };
    paymentForm.addEventListener("submit", async function (event) {
        event.preventDefault();
        if (paymentButton) paymentButton.disabled = true;
        setProcessingState(true);
        showPaymentOverlay("Opening secure checkout", "Connecting to Razorpay securely…");
        try {
            const response = await fetch(paymentForm.action, { method: "POST", body: new FormData(paymentForm), headers: { Accept: "application/json" }, credentials: "same-origin" });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || "The payment gateway could not be reached. Please try again.");
            if (!result.checkoutUrl || !result.merchantOrderId) throw new Error("Razorpay did not return the required checkout details.");
            const checkoutUrl = new URL(result.checkoutUrl, window.location.origin);
            const orderId = checkoutUrl.searchParams.get("razorpayOrderId");
            const keyId = checkoutUrl.searchParams.get("keyId");
            const amountInPaise = Number(checkoutUrl.searchParams.get("amount"));
            if (!orderId || !keyId || !Number.isSafeInteger(amountInPaise) || amountInPaise <= 0) throw new Error("Razorpay order details are incomplete.");
            await loadRazorpay();
            removePaymentOverlay();
            setProcessingState(false);
            const checkout = new Razorpay({
                key: keyId,
                amount: amountInPaise,
                currency: "INR",
                order_id: orderId,
                name: "CIIT Stack Lab",
                description: "Course payment",
                handler: async function (paymentResponse) {
                    try {
                        setProcessingState(true);
                        showPaymentOverlay("Verifying your payment", "Checking the payment securely on the server…");
                        const verification = await verifyPayment(result.merchantOrderId, paymentResponse);
                        if (successState) successState.hidden = false;
                        removePaymentOverlay();
                        if (verification.redirectUrl) window.setTimeout(() => window.location.assign(verification.redirectUrl), 700);
                    } catch (error) { showFailure(error.message); }
                },
                modal: { ondismiss: function () { showFailure("Payment window was closed. No course access was granted."); } }
            });
            checkout.on("payment.failed", function (response) { showFailure(response?.error?.description || "Payment failed. Please try again."); });
            checkout.open();
        } catch (error) { showFailure(error.message); }
    });
});
