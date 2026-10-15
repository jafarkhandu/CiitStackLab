document.addEventListener("DOMContentLoaded",function(){const t=document.getElementById("ciitTopicsScroll"),p=document.getElementById("topicsPrev"),n=document.getElementById("topicsNext");if(t&&p&&n){const a=350;p.addEventListener("click",function(){t.scrollBy({left:-a,behavior:"smooth"})});n.addEventListener("click",function(){t.scrollBy({left:a,behavior:"smooth"})})}const s=document.querySelectorAll(".ciit-hero-slide"),d=document.querySelectorAll(".ciit-slider-dot");if(!s.length||!d.length)return;let c=0,timer;function show(i){s.forEach(x=>x.classList.remove("active"));d.forEach(x=>x.classList.remove("active"));if(s[i]&&d[i]){s[i].classList.add("active");d[i].classList.add("active")}}function restart(){clearInterval(timer);timer=setInterval(function(){c=(c+1)%s.length;show(c)},4000)}d.forEach(function(x,i){x.addEventListener("click",function(){c=i;show(c);restart()})});show(0);restart()});

document.addEventListener("DOMContentLoaded",function(){if(!document.body.classList.contains("ciit-course-environment-body")){return}const t=document.querySelector('.ciit-learning-video-frame iframe[src], a[href*="/uploads/videos/"], .ciit-notes-reader-media a, .ciit-mux-watch-player');if(!t){return}const e=document.createElement("script");e.src="/js/mux-video-player.js?v=2";e.async=true;e.dataset.ciitMuxPlayer="true";document.body.appendChild(e)});
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
    const learningPage = document.querySelector(".ciit-learning-page");

    if (learningPage) {
        const panelButtons = Array.from(document.querySelectorAll("[data-learning-page]"));
        const panelContents = Array.from(document.querySelectorAll("[data-learning-panel-content]"));
        const extraPanels = document.querySelector(".ciit-learning-extra-panels");
        let activeContentPage = document.querySelector('[data-learning-content-page="video"]') ? "video" : "overview";

        function buildPager(container, pageCount, label, onChange) {
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

            let activeIndex = 0;
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
                if (options && typeof options.onChange === "function") {
                    options.onChange(activeIndex);
                }
            }

            const pager = buildPager(parent, pages.length, label, showPage);
            if (!pager) return;
            parent.dataset.ciitPaginated = "true";
            showPage(0);

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

                const videoPanel = document.querySelector('[data-learning-content-page="video"]');
                if (activeContentPage === "video" && videoPanel) {
                    const muxPlayer = videoPanel.querySelector(".ciit-mux-watch-player");
                    if (muxPlayer && !muxPlayer.dataset.ciitMuxReady) {
                        const existingScript = document.querySelector("script[data-ciit-mux-player]");
                        if (!existingScript) {
                            const playerScript = document.createElement("script");
                            playerScript.src = "/js/mux-video-player.js?v=2";
                            playerScript.async = true;
                            playerScript.dataset.ciitMuxPlayer = "true";
                            document.body.appendChild(playerScript);
                        }
                    }
                }
            }

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
                20,
                "Notes pages"
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
                    4,
                    "MCQ pages"
                );
            }

            assessment.querySelectorAll(".ciit-learning-assessment-subsection").forEach(function (subsection) {
                paginateItems(
                    subsection,
                    Array.from(subsection.querySelectorAll(":scope > .ciit-learning-assessment-question")),
                    4,
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

        showLearningPanel("notes");
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
