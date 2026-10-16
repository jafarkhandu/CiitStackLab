document.addEventListener("DOMContentLoaded",function(){const t=document.getElementById("ciitTopicsScroll"),p=document.getElementById("topicsPrev"),n=document.getElementById("topicsNext");if(t&&p&&n){const a=350;p.addEventListener("click",function(){t.scrollBy({left:-a,behavior:"smooth"})});n.addEventListener("click",function(){t.scrollBy({left:a,behavior:"smooth"})})}const s=document.querySelectorAll(".ciit-hero-slide"),d=document.querySelectorAll(".ciit-slider-dot");if(!s.length||!d.length)return;let c=0,timer;function show(i){s.forEach(x=>x.classList.remove("active"));d.forEach(x=>x.classList.remove("active"));if(s[i]&&d[i]){s[i].classList.add("active");d[i].classList.add("active")}}function restart(){clearInterval(timer);timer=setInterval(function(){c=(c+1)%s.length;show(c)},4000)}d.forEach(function(x,i){x.addEventListener("click",function(){c=i;show(c);restart()})});show(0);restart()});

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

    if (!paymentForm) {
        return;
    }

    const modalEyebrow = document.querySelector(".ciit-payment-modal-eyebrow");
    const modalSubtitle = document.querySelector(".ciit-payment-modal-subtitle");
    const paymentButton = document.getElementById("ciit-demo-payment-button");
    const paymentNote = paymentForm.nextElementSibling;
    const processingTitle = document.querySelector("#ciit-payment-processing-state > strong");
    const processingDescription = document.querySelector("#ciit-payment-processing-state > span");
    const processingStep = document.querySelector('[data-payment-step="process"] span');
    const successNote = document.querySelector(".ciit-payment-success-note");

    if (modalEyebrow) {
        modalEyebrow.textContent = "Secure checkout";
    }

    if (modalSubtitle) {
        modalSubtitle.textContent = "You will be securely redirected to PhonePe to complete your payment.";
    }

    if (paymentButton) {
        paymentButton.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Continue to PhonePe';
    }

    if (paymentNote) {
        paymentNote.textContent = "You will complete the payment on PhonePe. Your course is activated only after the payment is verified server-side.";
    }

    if (processingTitle) {
        processingTitle.textContent = "Connecting to PhonePe";
    }

    if (processingDescription) {
        processingDescription.textContent = "Preparing your secure payment session. Please wait...";
    }

    if (processingStep) {
        processingStep.textContent = "Redirecting to PhonePe checkout...";
    }

    if (successNote) {
        successNote.textContent = "Payment verification will be completed by the server before access is granted.";
    }
});
