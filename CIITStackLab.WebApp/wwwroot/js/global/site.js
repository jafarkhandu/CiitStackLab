document.addEventListener("DOMContentLoaded",function(){const t=document.getElementById("ciitTopicsScroll"),p=document.getElementById("topicsPrev"),n=document.getElementById("topicsNext");if(t&&p&&n){const a=350;p.addEventListener("click",function(){t.scrollBy({left:-a,behavior:"smooth"})});n.addEventListener("click",function(){t.scrollBy({left:a,behavior:"smooth"})})}const s=document.querySelectorAll(".ciit-hero-slide"),d=document.querySelectorAll(".ciit-slider-dot");if(!s.length||!d.length)return;let c=0,timer;function show(i){s.forEach(x=>x.classList.remove("active"));d.forEach(x=>x.classList.remove("active"));if(s[i]&&d[i]){s[i].classList.add("active");d[i].classList.add("active")}}function restart(){clearInterval(timer);timer=setInterval(function(){c=(c+1)%s.length;show(c)},4000)}d.forEach(function(x,i){x.addEventListener("click",function(){c=i;show(c);restart()})});show(0);restart()});

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
            paymentButton.innerHTML = '<i class="bi bi-lock-fill me-2"></i>Pay securely with Razorpay';
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
            paymentButton.innerHTML = '<i class="bi bi-lock-fill me-2"></i>Pay securely with Razorpay';
        }

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

        let scrollSaveFrame = 0;
        window.addEventListener("scroll", function () {
            if (scrollSaveFrame) return;
            scrollSaveFrame = window.requestAnimationFrame(function () {
                scrollSaveFrame = 0;
                savedLearningState.scrollY = window.scrollY;
                saveLearningState();
            });
        }, { passive: true });

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
                    const storedIndex = Number(window.localStorage.getItem(readProgressKey));
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

        // Long notes are paginated by their existing semantic blocks, preserving
        // their original HTML, code samples, tables, links and images.
        document.querySelectorAll(".ciit-learning-notes-content").forEach(function (notes) {
            let contentContainer = notes;
            let contentBlocks = Array.from(contentContainer.children);

            // Notes imported inside one wrapper still paginate by their inner
            // paragraphs, headings, lists, tables and code blocks.
            while (contentBlocks.length <= 20) {
                const nestedContainer = contentBlocks.find(function (block) {
                    return block.children.length > 20
                        && !block.matches("pre, table, ul, ol, figure, blockquote");
                });
                if (!nestedContainer) break;
                contentContainer = nestedContainer;
                contentBlocks = Array.from(contentContainer.children);
            }

            paginateItems(
                contentContainer,
                contentBlocks,
                40,
                "Notes pages",
                { markRead: true }
            );
        });

        // MCQs, interview answers and practice programs each get their own short
        // pages while remaining inside the same chapter assessment card.
        document.querySelectorAll(".ciit-learning-chapter-assessment").forEach(function (assessment) {
            const mcqSection = assessment.querySelector("#chapter-mcqs-" + assessment.id.replace("chapter-assessment-", ""));
            if (mcqSection) {
                paginateItems(
                    mcqSection,
                    Array.from(mcqSection.querySelectorAll(":scope > .ciit-learning-assessment-question")),
                    5,
                    "MCQ pages"
                );
            }

            assessment.querySelectorAll(".ciit-learning-assessment-subsection").forEach(function (subsection) {
                paginateItems(
                    subsection,
                    Array.from(subsection.querySelectorAll(":scope > .ciit-learning-assessment-question")),
                    5,
                    subsection.querySelector("h5")?.textContent?.trim() + " pages"
                );
            });
        });

        // The graded quiz is also paginated. Validate all questions before POST,
        // including questions currently on hidden pages.
        document.querySelectorAll(".ciit-assessment-form").forEach(function (form) {
            const questions = Array.from(form.querySelectorAll(":scope > .ciit-learning-question"));
            const pagination = paginateItems(form, questions, 5, "Knowledge check pages");
            if (!pagination) return;

            form.querySelectorAll('input[type="radio"][required]').forEach(function (input) {
                input.removeAttribute("required");
            });

            form.addEventListener("submit", function (event) {
                const unanswered = questions.find(function (question) {
                    return !question.querySelector('input[type="radio"]:checked');
                });
                if (!unanswered) return;

                event.preventDefault();
                const pages = Array.from(form.querySelectorAll(":scope > [data-learning-subpage]"));
                const pageIndex = pages.findIndex(function (page) {
                    return page.contains(unanswered);
                });
                if (pageIndex >= 0) pagination.showPage(pageIndex);
                unanswered.scrollIntoView({ behavior: "smooth", block: "center" });
                unanswered.classList.add("ciit-learning-question-unanswered");
                window.setTimeout(function () {
                    unanswered.classList.remove("ciit-learning-question-unanswered");
                }, 1800);
            });
        });

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

        const validPanels = ["notes", "content", "assessment"];
        const initialPanel = validPanels.includes(savedLearningState.panel)
            ? savedLearningState.panel
            : "notes";
        showLearningPanel(initialPanel);

        if (Number.isFinite(savedLearningState.scrollY)) {
            const restoreScrollPosition = function () {
                window.scrollTo({ top: savedLearningState.scrollY, left: 0, behavior: "auto" });
            };
            window.requestAnimationFrame(function () {
                restoreScrollPosition();
                window.requestAnimationFrame(restoreScrollPosition);
            });
            window.setTimeout(restoreScrollPosition, 120);
            window.setTimeout(restoreScrollPosition, 360);
        }
    }

    const switchHub = document.querySelector("[data-course-hub-switch]");
    if (switchHub) {
        switchHub.addEventListener("click", function (event) {
            if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            if (switchHub.dataset.transitioning === "true") return;
            switchHub.dataset.transitioning = "true";
            switchHub.classList.add("is-switching");
            document.body.classList.add("ciit-hub-switching");
            window.setTimeout(function () {
                window.location.assign(switchHub.href);
            }, window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 420);
        });
    }
});
