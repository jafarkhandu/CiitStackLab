(() => {
    const initialize = () => {
        const page = document.querySelector('.ciit-learning-page');
        const slideSection = page?.querySelector('[data-learning-content-page="slides"]');
        const sidebar = page?.querySelector('.ciit-learning-sidebar');

        if (!page || !slideSection || !sidebar || slideSection.dataset.slideViewerInitialized === 'true') {
            return;
        }

        const currentLesson = sidebar.querySelector('.ciit-learning-open-lesson.is-current, .ciit-learning-additional-link.is-current');
        const currentContentItem = currentLesson?.closest('.ciit-learning-content-item, .ciit-learning-additional-row');
        const currentSlideLink = currentContentItem?.querySelector('[data-slide-file-url], a[title="Open slides"]');
        const fallbackSlideLink = slideSection.querySelector('.ciit-learning-open-resource');
        const slideUrl = currentSlideLink?.dataset.slideFileUrl || fallbackSlideLink?.href;

        if (!slideUrl) {
            return;
        }

        slideSection.dataset.slideViewerInitialized = 'true';
        fallbackSlideLink?.remove();

        const resourceTitle = slideSection.querySelector('.ciit-learning-resource-title');
        if (resourceTitle) {
            const heading = resourceTitle.querySelector('h3');
            const description = resourceTitle.querySelector('p');
            if (heading) heading.textContent = 'Presentation';
            if (description) description.textContent = 'Follow the lesson slides without leaving this learning page.';
        }

        const reader = document.createElement('div');
        reader.className = 'ciit-slide-reader ciit-learning-embedded-slide-reader';
        reader.dataset.slideUrl = slideUrl;
        reader.dataset.slideTitle = currentLesson?.textContent?.trim() || 'Lesson Presentation';
        reader.innerHTML = `
            <header class="ciit-slide-reader-header">
                <div class="ciit-slide-reader-header-inner">
                    <div class="ciit-slide-reader-title">
                        <span>LESSON PRESENTATION</span>
                        <h1>${escapeHtml(reader.dataset.slideTitle)}</h1>
                    </div>
                    <div class="ciit-slide-reader-page" aria-live="polite">
                        <span id="slideCurrent">1</span>
                        <span class="ciit-slide-reader-page-divider">/</span>
                        <span id="slideTotal">—</span>
                    </div>
                </div>
            </header>

            <div class="ciit-slide-reader-shell">
                <aside class="ciit-slide-reader-thumbnails" aria-label="Presentation slide thumbnails">
                    <div id="slideThumbnails" class="ciit-slide-reader-thumbnail-list">
                        <div class="ciit-slide-reader-loading">Loading slides…</div>
                    </div>
                </aside>

                <main class="ciit-slide-reader-main">
                    <div id="slideStage" class="ciit-slide-reader-stage" tabindex="0">
                        <div id="slideLoading" class="ciit-slide-reader-loading-panel">
                            <div class="spinner-border" aria-hidden="true"></div>
                            <strong>Opening presentation</strong>
                            <span>Your slides are rendered securely in the browser.</span>
                        </div>
                    </div>
                </main>
            </div>

            <footer class="ciit-slide-reader-controls" aria-label="Presentation controls">
                <button type="button" class="ciit-slide-control" id="slidePrevious" title="Previous slide">
                    <i class="bi bi-chevron-left"></i>
                    <span>Previous</span>
                </button>

                <div class="ciit-slide-reader-counter">
                    <span id="slideControlCurrent">1</span>
                    <span>/</span>
                    <span id="slideControlTotal">—</span>
                </div>

                <button type="button" class="ciit-slide-control" id="slideNext" title="Next slide">
                    <span>Next</span>
                    <i class="bi bi-chevron-right"></i>
                </button>

                <div class="ciit-slide-reader-divider"></div>

                <button type="button" class="ciit-slide-icon-control" id="slideZoomOut" title="Zoom out" aria-label="Zoom out">
                    <i class="bi bi-dash-lg"></i>
                </button>
                <button type="button" class="ciit-slide-zoom-label" id="slideZoomLabel" title="Reset zoom">100%</button>
                <button type="button" class="ciit-slide-icon-control" id="slideZoomIn" title="Zoom in" aria-label="Zoom in">
                    <i class="bi bi-plus-lg"></i>
                </button>
                <button type="button" class="ciit-slide-icon-control" id="slideFit" title="Fit slide" aria-label="Fit slide">
                    <i class="bi bi-arrows-angle-contract"></i>
                </button>
                <button type="button" class="ciit-slide-icon-control" id="slideFullscreen" title="Fullscreen" aria-label="Fullscreen">
                    <i class="bi bi-fullscreen"></i>
                </button>
            </footer>
        `;

        slideSection.appendChild(reader);

        const stylesheet = document.createElement('link');
        stylesheet.rel = 'stylesheet';
        stylesheet.href = '/css/student/slide-reader.css?v=1';
        document.head.appendChild(stylesheet);

        const embeddedStyles = document.createElement('style');
        embeddedStyles.textContent = `
            .ciit-learning-embedded-slide-reader {
                min-height: 720px;
                height: 720px;
                border: 1px solid #dfe5ec;
                border-radius: 16px;
                overflow: hidden;
                box-shadow: 0 14px 40px rgba(23, 32, 51, 0.08);
            }
            .ciit-learning-embedded-slide-reader .ciit-slide-reader-header-inner {
                min-height: 62px;
                grid-template-columns: minmax(0, 1fr) auto;
                padding: 10px 18px;
            }
            .ciit-learning-embedded-slide-reader .ciit-slide-reader-title {
                text-align: left;
            }
            .ciit-learning-embedded-slide-reader .ciit-slide-reader-title h1 {
                font-size: .98rem;
            }
            .ciit-learning-embedded-slide-reader .ciit-slide-reader-shell {
                min-height: 0;
            }
            .ciit-learning-embedded-slide-reader .ciit-slide-reader-main {
                padding: 14px;
            }
            .ciit-learning-embedded-slide-reader .ciit-slide-reader-stage {
                min-height: 0;
            }
            @media (max-width: 900px) {
                .ciit-learning-embedded-slide-reader {
                    height: 640px;
                    min-height: 640px;
                }
            }
            @media (max-width: 600px) {
                .ciit-learning-embedded-slide-reader {
                    height: 620px;
                    min-height: 620px;
                }
                .ciit-learning-embedded-slide-reader .ciit-slide-reader-header-inner {
                    min-height: 58px;
                    padding: 8px 12px;
                }
            }
            @media (prefers-reduced-motion: reduce) {
                .ciit-learning-embedded-slide-reader .ciit-slide-thumbnail {
                    transition: none;
                }
            }
        `;
        document.head.appendChild(embeddedStyles);

        const slideScript = document.createElement('script');
        slideScript.type = 'module';
        slideScript.src = '/js/student/slide-reader.js?v=1';
        slideScript.dataset.ciitLearningSlideReader = 'true';
        document.body.appendChild(slideScript);

        if (currentSlideLink) {
            currentSlideLink.addEventListener('click', event => {
                event.preventDefault();
                reader.scrollIntoView({ behavior: 'smooth', block: 'start' });
                window.setTimeout(() => document.getElementById('slideStage')?.focus(), 350);
            });
        }
    };

    const escapeHtml = value => String(value || '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize, { once: true });
    } else {
        initialize();
    }
})();
