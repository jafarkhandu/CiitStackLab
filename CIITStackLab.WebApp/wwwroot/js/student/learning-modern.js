(function () {
    const sidebarStateKey = 'ciit-learning-global-sidebar-collapsed';
    let chapterNavigationController = null;

    function ensureReferenceStyles() {
        const styleId = 'ciit-learning-layout-fixes';
        if (document.getElementById(styleId)) return;

        const style = document.createElement('style');
        style.id = styleId;
        style.textContent = `
            .ciit-learning-mode .ciit-learning-shell {
                width: 100%; max-width: none; margin: 0; padding-inline: 0;
            }
            .ciit-learning-mode .ciit-learning-main { min-width: 0; width: 100%; }
            .ciit-learning-mode .ciit-learning-main-card {
                width: 100%; max-width: 1080px; margin: 0 auto;
                padding: 36px 56px 40px; border-radius: 22px;
                background: rgba(255,255,255,.88);
                backdrop-filter: none; -webkit-backdrop-filter: none;
            }
            .ciit-learning-mode .ciit-learning-resource,
            .ciit-learning-mode .ciit-learning-notes-panel,
            .ciit-learning-mode .ciit-learning-notes-content {
                margin: 0; padding: 0; border: 0; border-radius: 0;
                background: none; box-shadow: none;
            }
            .ciit-learning-mode .ciit-learning-resource-title { display: none; }
            .ciit-learning-mode .ciit-learning-main-card::before,
            .ciit-learning-mode .ciit-learning-lesson-heading::before,
            .ciit-learning-mode .ciit-learning-lesson-heading::after {
                display: none; content: none;
            }
            .ciit-learning-mode .ciit-learning-lesson-heading {
                margin: 0; padding: 0; border: 0; background: transparent; box-shadow: none;
            }
            .ciit-learning-mode .ciit-learning-breadcrumb {
                margin: 0 0 12px; color: var(--ciit-learn-muted); font-size: 13px;
                line-height: 1.4; font-weight: 500;
            }
            .ciit-learning-mode .ciit-learning-breadcrumb a { color: inherit; text-decoration: none; }
            .ciit-learning-mode .ciit-learning-breadcrumb a:hover { text-decoration: underline; }
            .ciit-learning-mode .ciit-learning-lesson-heading .ciit-learning-eyebrow {
                margin: 0 0 6px; color: var(--ciit-learn-blue); font-size: 12px; line-height: 1.4;
            }
            .ciit-learning-mode .ciit-learning-lesson-heading .ciit-learning-chapter-title {
                margin: 0 0 20px; color: var(--ciit-learn-ink); font-size: 32px;
                line-height: 1.2; font-weight: 800; letter-spacing: -.7px;
            }
            .ciit-learning-mode .ciit-learning-topic-step-progress,
            .ciit-learning-mode .ciit-learning-lesson-heading .ciit-learning-topic-progress { display: none; }
            .ciit-learning-mode .ciit-learning-notes-content {
                max-width: none; color: #425472; font-size: 16.5px; line-height: 1.78;
                overflow-wrap: anywhere;
            }
            .ciit-learning-mode .ciit-learning-notes-content p,
            .ciit-learning-mode .ciit-learning-notes-content ul,
            .ciit-learning-mode .ciit-learning-notes-content ol {
                margin-top: 0; margin-bottom: 12px;
            }
            .ciit-learning-mode .ciit-learning-notes-content li { margin-bottom: 5px; }
            .ciit-learning-mode .ciit-learning-notes-content strong,
            .ciit-learning-mode .ciit-learning-notes-content b { color: #1a2b4f; }
            .ciit-learning-mode .ciit-learning-notes-content h2,
            .ciit-learning-mode .ciit-learning-notes-content h3,
            .ciit-learning-mode .ciit-learning-notes-content h4 {
                max-width: none; color: var(--ciit-learn-ink); border-top: 0; border-bottom: 0;
                box-shadow: none; scroll-margin-top: 98px;
            }
            .ciit-learning-mode .ciit-learning-notes-content h2 {
                margin: 28px 0 10px; padding-top: 0; font-size: 22px; line-height: 1.35;
            }
            .ciit-learning-mode .ciit-learning-notes-content > h2:first-child,
            .ciit-learning-mode .ciit-learning-notes-content > *:first-child + h2 { margin-top: 20px; }
            .ciit-learning-mode .ciit-learning-notes-content h3 {
                margin: 24px 0 8px; font-size: 18px; line-height: 1.4;
            }
            .ciit-learning-mode .ciit-learning-notes-content h4 { margin: 20px 0 8px; }
            .ciit-learning-mode .ciit-learning-notes-content a { text-decoration: none; }
            .ciit-learning-mode .ciit-learning-notes-content a:hover { text-decoration: underline; }
            .ciit-learning-mode .ciit-learning-notes-content pre {
                margin: 20px 0; border-top: 0; border-bottom: 0; box-shadow: none;
                max-width: 100%; overflow-x: auto;
            }
            .ciit-learning-mode .ciit-learning-sidebar { max-height: none; height: auto; }
            .ciit-learning-mode .ciit-learning-sidebar-head {
                margin-bottom: 12px; padding: 0; background: transparent; border: 0; box-shadow: none;
            }
            .ciit-learning-mode .ciit-learning-current-chapters { gap: 8px; }
            .ciit-learning-mode .ciit-learning-subtopics { gap: 2px; }
            .ciit-learning-mode .ciit-learning-exam-apply { margin-top: 16px; }

            /* Learn uses the exact shared course sidebar; only the Learn-specific collapse remains. */
            .ciit-learning-mode .ciit-course-global-sidebar {
                position: sticky !important; top: 72px !important; align-self: start;
                height: auto !important; min-height: 0 !important; max-height: none !important;
                overflow: visible !important;
            }
            .ciit-learning-mode .ciit-course-global-sidebar .ciit-course-sidebar-art {
                display: block !important;
            }

            @media (min-width: 1200px) {
                .ciit-learning-mode .ciit-course-environment-workspace { gap: 16px; }
                .ciit-learning-mode .ciit-course-global-sidebar { width: 100%; }
                .ciit-learning-mode .ciit-learning-shell {
                    display: grid; grid-template-columns: minmax(0,1fr) 330px !important;
                    grid-template-areas: "main sidebar"; align-items: start;
                    justify-content: stretch; gap: 16px !important; padding: 14px 16px 40px !important;
                }
                .ciit-learning-mode .ciit-learning-main {
                    grid-area: main; display: flex; justify-content: center;
                    min-width: 0; max-width: none;
                }
                .ciit-learning-mode .ciit-learning-shell > .ciit-learning-sidebar {
                    grid-area: sidebar; width: 330px;
                }
            }
            @media (min-width: 768px) and (max-width: 1199.98px) {
                .ciit-learning-mode .ciit-learning-shell {
                    display: grid; grid-template-columns: minmax(0,1fr);
                    grid-template-areas: "sidebar" "main"; width: 100%; max-width: none; gap: 16px;
                }
                .ciit-learning-mode .ciit-learning-shell > .ciit-learning-sidebar {
                    grid-area: sidebar; position: relative; top: auto; width: 100%; max-height: none;
                }
                .ciit-learning-mode .ciit-learning-main { grid-area: main; }
                .ciit-learning-mode .ciit-learning-main-card { max-width: 1080px; padding: 32px 40px 36px; }
            }
            @media (max-width: 767.98px) {
                .ciit-learning-mode .ciit-learning-main-card {
                    max-width: none; padding: 24px 20px 30px; border-radius: 18px;
                }
                .ciit-learning-mode .ciit-learning-notes-content { font-size: 16px; line-height: 1.7; }
            }
            @media (prefers-reduced-motion: reduce) {
                html:has(.ciit-learning-mode) { scroll-behavior: auto; }
                .ciit-learning-mode * { scroll-behavior: auto !important; }
            }
            html:has(.ciit-learning-mode) { scroll-behavior: smooth; }
        `;
        document.head.appendChild(style);
    }

    function syncSidebarToggle() {
        const sidebarToggle = document.querySelector('[data-learning-global-sidebar-toggle]');
        if (!sidebarToggle) return;
        const collapsedClass = 'ciit-learning-global-sidebar-collapsed';
        const isCollapsed = document.body.classList.contains(collapsedClass);
        const label = isCollapsed ? 'Show main sidebar' : 'Hide main sidebar';
        const icon = sidebarToggle.querySelector('i');
        const accessibleLabel = sidebarToggle.querySelector('.visually-hidden');
        sidebarToggle.setAttribute('aria-expanded', String(!isCollapsed));
        sidebarToggle.setAttribute('aria-label', label);
        sidebarToggle.title = label;
        if (icon) icon.className = isCollapsed ? 'bi bi-chevron-right' : 'bi bi-chevron-left';
        if (accessibleLabel) accessibleLabel.textContent = label;
    }

    function restoreSidebarState() {
        let collapsed = false;
        try { collapsed = window.localStorage.getItem(sidebarStateKey) === '1'; } catch (_) { }
        document.body.classList.toggle('ciit-learning-global-sidebar-collapsed', collapsed);
        syncSidebarToggle();
    }

    function initSidebarToggle() {
        const sidebarToggle = document.querySelector('[data-learning-global-sidebar-toggle]');
        if (!sidebarToggle || sidebarToggle.dataset.initialized === 'true') return;
        sidebarToggle.dataset.initialized = 'true';
        sidebarToggle.addEventListener('click', function () {
            const collapsedClass = 'ciit-learning-global-sidebar-collapsed';
            const collapsed = document.body.classList.toggle(collapsedClass);
            try { window.localStorage.setItem(sidebarStateKey, collapsed ? '1' : '0'); } catch (_) { }
            syncSidebarToggle();
        });
        restoreSidebarState();
    }

    function enhanceLearningShell() {
        const learningPage = document.querySelector('.ciit-learning-page');
        if (!learningPage) return;

        const lessonTitle = document.querySelector('.ciit-learning-lesson-heading .ciit-learning-chapter-title');
        const headerTitle = document.querySelector('.ciit-learning-course-intro h1');
        const headerDescription = document.querySelector('.ciit-learning-course-intro > p');
        const topicEyebrow = document.querySelector('.ciit-learning-lesson-heading .ciit-learning-eyebrow');
        const panelPager = document.querySelector('.ciit-learning-panel-pager');

        if (headerTitle) {
            const topicTitle = lessonTitle?.textContent?.trim();
            if (topicTitle) headerTitle.textContent = topicTitle;
        }
        if (topicEyebrow && headerDescription) {
            headerDescription.textContent = topicEyebrow.textContent.trim().replace(/\s+/g, ' ');
        }
        panelPager?.remove();

        document.querySelector('[data-learning-page="content"]')?.remove();
        document.querySelector('[data-learning-page="assessment"]')?.remove();

        const contentPanel = document.querySelector('[data-learning-panel-content="content"]');
        const notesPanel = document.querySelector('[data-learning-panel-content="notes"]');
        const assessmentPanel = document.querySelector('[data-learning-panel-content="assessment"]');

        if (contentPanel && notesPanel) {
            contentPanel.classList.add('ciit-learning-embedded-content');
            contentPanel.removeAttribute('data-learning-panel-content');
            contentPanel.removeAttribute('data-learning-content-page');
            contentPanel.style.display = '';
            const title = contentPanel.querySelector('.ciit-learning-resource-title h3');
            const description = contentPanel.querySelector('.ciit-learning-resource-title p');
            if (title) title.textContent = 'Lesson Resources';
            if (description) description.textContent = 'Video, slides and supporting material for this topic.';
            notesPanel.appendChild(contentPanel);
        }

        const assessmentLink = document.querySelector('[data-learning-assessment-link]');
        const learningPathLink = document.querySelector('.ciit-course-global-nav a.active');
        if (assessmentLink && assessmentLink.dataset.ciitAssessmentBound !== 'true') {
            assessmentLink.dataset.ciitAssessmentBound = 'true';
            assessmentLink.addEventListener('click', function (event) {
                event.preventDefault();
                if (!assessmentPanel) return;
                if (notesPanel) notesPanel.hidden = true;
                assessmentPanel.hidden = false;
                learningPathLink?.classList.remove('active');
                assessmentLink.classList.add('active');
                const target = learningPage.querySelector('[id^="lesson-assessment-"]');
                if (target) target.scrollIntoView({
                    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
                    block: 'start'
                });
                const targetId = target?.id || 'lesson-assessment';
                history.replaceState(null, '', `${window.location.pathname}${window.location.search}#${targetId}`);
            });
        }

        document.querySelectorAll('.exam-timezone-offset').forEach(function (input) {
            input.value = new Date().getTimezoneOffset();
        });

        const examApplicationModal = document.getElementById('topicExamApplicationModal');
        if (examApplicationModal && window.bootstrap) {
            window.bootstrap.Modal.getOrCreateInstance(examApplicationModal).show();
        }

        if (window.location.hash.startsWith('#lesson-assessment') && assessmentPanel) {
            if (notesPanel) notesPanel.hidden = true;
            assessmentPanel.hidden = false;
        }
    }

    async function loadChapterWithoutReload(link) {
        const href = link.href;
        if (!href || link.target === '_blank' || link.hasAttribute('download')) return;
        if (chapterNavigationController) chapterNavigationController.abort();
        chapterNavigationController = new AbortController();

        document.documentElement.classList.add('ciit-learning-navigating');
        try {
            const response = await fetch(href, {
                method: 'GET',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin',
                signal: chapterNavigationController.signal
            });
            if (!response.ok) throw new Error(`Chapter request failed: ${response.status}`);

            const html = await response.text();
            const parser = new DOMParser();
            const nextDocument = parser.parseFromString(html, 'text/html');
            const nextShell = nextDocument.querySelector('.ciit-learning-shell');
            const currentShell = document.querySelector('.ciit-learning-shell');
            if (!nextShell || !currentShell) throw new Error('Learning shell was not returned.');

            currentShell.replaceWith(nextShell);
            history.pushState({ ciitLearningChapter: true }, '', href);
            document.title = nextDocument.title || document.title;
            enhanceLearningShell();
            syncSidebarToggle();
            window.scrollTo({ top: 0, behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
        } catch (error) {
            if (error?.name !== 'AbortError') {
                window.location.assign(href);
            }
        } finally {
            document.documentElement.classList.remove('ciit-learning-navigating');
        }
    }

    function initChapterNavigation() {
        if (document.documentElement.dataset.ciitLearningChapterNav === 'true') return;
        document.documentElement.dataset.ciitLearningChapterNav = 'true';

        document.addEventListener('click', function (event) {
            const link = event.target.closest('.ciit-learning-current-chapter-link');
            if (!link || event.defaultPrevented) return;
            if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
            event.preventDefault();
            loadChapterWithoutReload(link);
        });

        window.addEventListener('popstate', function () {
            const currentUrl = window.location.href;
            const link = document.querySelector(`.ciit-learning-current-chapter-link[href="${CSS.escape(currentUrl)}"]`);
            if (link) return;
            window.location.reload();
        });
    }

    function initModernLearningPage() {
        ensureReferenceStyles();
        restoreSidebarState();
        initSidebarToggle();
        enhanceLearningShell();
        initChapterNavigation();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initModernLearningPage, { once: true });
    } else {
        initModernLearningPage();
    }
})();
