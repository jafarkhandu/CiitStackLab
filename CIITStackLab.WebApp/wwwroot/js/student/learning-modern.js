(function () {
    function ensureReferenceStyles() {
        const styleId = 'ciit-learning-layout-fixes';
        if (document.getElementById(styleId)) {
            return;
        }

        const style = document.createElement('style');
        style.id = styleId;
        style.textContent = `
            .ciit-learning-mode .ciit-learning-shell {
                width: 100%;
                max-width: none;
                margin: 0;
                padding-inline: 0;
            }

            .ciit-learning-mode .ciit-learning-main {
                min-width: 0;
                width: 100%;
            }

            .ciit-learning-mode .ciit-learning-main-card {
                width: 100%;
                max-width: 1080px;
                margin: 0 auto;
                padding: 36px 56px 40px;
                border-radius: 22px;
                background: rgba(255, 255, 255, .88);
                backdrop-filter: none;
                -webkit-backdrop-filter: none;
            }

            .ciit-learning-mode .ciit-learning-resource,
            .ciit-learning-mode .ciit-learning-notes-panel,
            .ciit-learning-mode .ciit-learning-notes-content {
                margin: 0;
                padding: 0;
                border: 0;
                border-radius: 0;
                background: none;
                box-shadow: none;
            }

            .ciit-learning-mode .ciit-learning-resource-title {
                display: none;
            }

            .ciit-learning-mode .ciit-learning-main-card::before,
            .ciit-learning-mode .ciit-learning-lesson-heading::before,
            .ciit-learning-mode .ciit-learning-lesson-heading::after {
                display: none;
                content: none;
            }

            .ciit-learning-mode .ciit-learning-lesson-heading {
                margin: 0;
                padding: 0;
                border: 0;
                background: transparent;
                box-shadow: none;
            }

            .ciit-learning-mode .ciit-learning-breadcrumb {
                margin: 0 0 12px;
                color: var(--ciit-learn-muted);
                font-size: 13px;
                line-height: 1.4;
                font-weight: 500;
            }

            .ciit-learning-mode .ciit-learning-breadcrumb a {
                color: inherit;
                text-decoration: none;
            }

            .ciit-learning-mode .ciit-learning-breadcrumb a:hover {
                text-decoration: underline;
            }

            .ciit-learning-mode .ciit-learning-lesson-heading .ciit-learning-eyebrow {
                margin: 0 0 6px;
                color: var(--ciit-learn-blue);
                font-size: 12px;
                line-height: 1.4;
            }

            .ciit-learning-mode .ciit-learning-lesson-heading .ciit-learning-chapter-title {
                margin: 0 0 20px;
                color: var(--ciit-learn-ink);
                font-size: 32px;
                line-height: 1.2;
                font-weight: 800;
                letter-spacing: -.7px;
            }

            .ciit-learning-mode .ciit-learning-topic-step-progress,
            .ciit-learning-mode .ciit-learning-lesson-heading .ciit-learning-topic-progress {
                display: none;
            }

            .ciit-learning-mode .ciit-learning-notes-content {
                max-width: none;
                color: var(--ciit-learn-text);
                font-size: 16px;
                line-height: 1.7;
            }

            .ciit-learning-mode .ciit-learning-notes-content p,
            .ciit-learning-mode .ciit-learning-notes-content ul,
            .ciit-learning-mode .ciit-learning-notes-content ol {
                margin-top: 0;
                margin-bottom: 12px;
            }

            .ciit-learning-mode .ciit-learning-notes-content li {
                margin-bottom: 4px;
            }

            .ciit-learning-mode .ciit-learning-notes-content h2,
            .ciit-learning-mode .ciit-learning-notes-content h3,
            .ciit-learning-mode .ciit-learning-notes-content h4 {
                max-width: none;
                color: var(--ciit-learn-ink);
                border-top: 0;
                border-bottom: 0;
                box-shadow: none;
                scroll-margin-top: 98px;
            }

            .ciit-learning-mode .ciit-learning-notes-content h2 {
                margin: 28px 0 10px;
                padding-top: 0;
                font-size: 22px;
                line-height: 1.35;
            }

            .ciit-learning-mode .ciit-learning-notes-content > h2:first-child,
            .ciit-learning-mode .ciit-learning-notes-content > *:first-child + h2 {
                margin-top: 20px;
            }

            .ciit-learning-mode .ciit-learning-notes-content h3 {
                margin: 24px 0 8px;
                font-size: 18px;
                line-height: 1.4;
            }

            .ciit-learning-mode .ciit-learning-notes-content h4 {
                margin: 20px 0 8px;
            }

            .ciit-learning-mode .ciit-learning-notes-content a {
                text-decoration: none;
            }

            .ciit-learning-mode .ciit-learning-notes-content a:hover {
                text-decoration: underline;
            }

            .ciit-learning-mode .ciit-learning-notes-content pre {
                margin: 20px 0;
                border-top: 0;
                border-bottom: 0;
                box-shadow: none;
            }

            .ciit-learning-mode .ciit-learning-sidebar {
                max-height: none;
                height: auto;
            }

            .ciit-learning-mode .ciit-learning-sidebar-head {
                margin-bottom: 12px;
                padding: 0;
                background: transparent;
                border: 0;
                box-shadow: none;
            }

            .ciit-learning-mode .ciit-learning-current-chapters {
                gap: 8px;
            }

            .ciit-learning-mode .ciit-learning-subtopics {
                gap: 2px;
            }

            .ciit-learning-mode .ciit-learning-exam-apply {
                margin-top: 16px;
            }

            @media (min-width: 1200px) {
                .ciit-learning-mode .ciit-course-environment-workspace {
                    gap: 16px;
                }

                .ciit-learning-mode .ciit-course-global-sidebar {
                    height: auto;
                    min-height: 0;
                    align-self: start;
                }

                .ciit-learning-mode .ciit-learning-shell {
                    display: grid;
                    grid-template-columns: minmax(0, 1fr) 330px !important;
                    grid-template-areas: "main sidebar";
                    align-items: start;
                    justify-content: stretch;
                    gap: 16px !important;
                    padding: 14px 16px 40px !important;
                }

                .ciit-learning-mode .ciit-learning-main {
                    grid-area: main;
                    display: flex;
                    justify-content: center;
                    min-width: 0;
                    max-width: none;
                }

                .ciit-learning-mode .ciit-learning-shell > .ciit-learning-sidebar {
                    grid-area: sidebar;
                    width: 330px;
                }
            }

            @media (min-width: 768px) and (max-width: 1199.98px) {
                .ciit-learning-mode .ciit-learning-shell {
                    display: grid;
                    grid-template-columns: minmax(0, 1fr);
                    grid-template-areas: "sidebar" "main";
                    width: 100%;
                    max-width: none;
                    gap: 16px;
                }

                .ciit-learning-mode .ciit-learning-shell > .ciit-learning-sidebar {
                    grid-area: sidebar;
                    position: relative;
                    top: auto;
                    width: 100%;
                    max-height: none;
                }

                .ciit-learning-mode .ciit-learning-main {
                    grid-area: main;
                }

                .ciit-learning-mode .ciit-learning-main-card {
                    max-width: 1080px;
                    padding: 32px 40px 36px;
                }
            }

            @media (max-width: 767.98px) {
                .ciit-learning-mode .ciit-learning-main-card {
                    max-width: none;
                    padding: 24px 20px 30px;
                    border-radius: 18px;
                }

                .ciit-learning-mode .ciit-learning-notes-content {
                    font-size: 16px;
                    line-height: 1.7;
                }
            }

            html:has(.ciit-learning-mode) {
                scroll-behavior: smooth;
            }

            @media (prefers-reduced-motion: reduce) {
                html:has(.ciit-learning-mode) {
                    scroll-behavior: auto;
                }
            }
        `;

        document.head.appendChild(style);
    }

    function initModernLearningPage() {
        ensureReferenceStyles();

        const sidebarToggle = document.querySelector('[data-learning-global-sidebar-toggle]');

        if (sidebarToggle) {
            const collapsedClass = 'ciit-learning-global-sidebar-collapsed';
            const icon = sidebarToggle.querySelector('i');
            const accessibleLabel = sidebarToggle.querySelector('.visually-hidden');

            const syncSidebarToggle = function () {
                const isCollapsed = document.body.classList.contains(collapsedClass);
                const label = isCollapsed ? 'Show main sidebar' : 'Hide main sidebar';

                sidebarToggle.setAttribute('aria-expanded', String(!isCollapsed));
                sidebarToggle.setAttribute('aria-label', label);
                sidebarToggle.title = label;

                if (icon) {
                    icon.className = isCollapsed ? 'bi bi-chevron-right' : 'bi bi-chevron-left';
                }
                if (accessibleLabel) {
                    accessibleLabel.textContent = label;
                }
            };

            sidebarToggle.addEventListener('click', function () {
                document.body.classList.toggle(collapsedClass);
                syncSidebarToggle();
            });

            syncSidebarToggle();
        }

        const learningPage = document.querySelector('.ciit-learning-page');
        if (!learningPage) {
            return;
        }

        const lessonTitle = document.querySelector('.ciit-learning-lesson-heading .ciit-learning-chapter-title');
        const headerTitle = document.querySelector('.ciit-learning-course-intro h1');
        const headerDescription = document.querySelector('.ciit-learning-course-intro > p');
        const topicEyebrow = document.querySelector('.ciit-learning-lesson-heading .ciit-learning-eyebrow');
        const panelPager = document.querySelector('.ciit-learning-panel-pager');

        if (headerTitle) {
            const topicTitle = lessonTitle?.textContent?.trim();
            if (topicTitle) {
                headerTitle.textContent = topicTitle;
            }
        }

        if (topicEyebrow && headerDescription) {
            headerDescription.textContent = topicEyebrow.textContent.trim().replace(/\s+/g, ' ');
        }

        panelPager?.remove();

        const contentButton = document.querySelector('[data-learning-page="content"]');
        const assessmentButton = document.querySelector('[data-learning-page="assessment"]');
        const contentPanel = document.querySelector('[data-learning-panel-content="content"]');
        const notesPanel = document.querySelector('[data-learning-panel-content="notes"]');
        const assessmentPanel = document.querySelector('[data-learning-panel-content="assessment"]');

        contentButton?.remove();
        assessmentButton?.remove();

        if (contentPanel && notesPanel) {
            contentPanel.classList.add('ciit-learning-embedded-content');
            contentPanel.removeAttribute('data-learning-panel-content');
            contentPanel.removeAttribute('data-learning-content-page');
            contentPanel.style.display = '';

            const title = contentPanel.querySelector('.ciit-learning-resource-title h3');
            const description = contentPanel.querySelector('.ciit-learning-resource-title p');

            if (title) {
                title.textContent = 'Lesson Resources';
            }
            if (description) {
                description.textContent = 'Video, slides and supporting material for this topic.';
            }

            notesPanel.appendChild(contentPanel);
        }

        const assessmentLink = document.querySelector('[data-learning-assessment-link]');
        const learningPathLink = document.querySelector('.ciit-course-global-nav a.active');

        const showAssessment = function (updateHash) {
            if (!assessmentPanel) {
                return;
            }

            if (notesPanel) {
                notesPanel.hidden = true;
            }
            assessmentPanel.hidden = false;

            learningPathLink?.classList.remove('active');
            assessmentLink?.classList.add('active');

            if (updateHash) {
                const assessmentTarget = learningPage.querySelector('[id^="lesson-assessment-"]');
                const targetId = assessmentTarget?.id || 'lesson-assessment';
                history.replaceState(null, '', `${window.location.pathname}${window.location.search}#${targetId}`);
            }

            const assessmentTarget = learningPage.querySelector('[id^="lesson-assessment-"]');
            assessmentTarget?.scrollIntoView({
                behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
                block: 'start'
            });
        };

        if (assessmentLink) {
            assessmentLink.addEventListener('click', function (event) {
                event.preventDefault();
                showAssessment(true);
            });
        }

        if (window.location.hash && window.location.hash.startsWith('#lesson-assessment')) {
            showAssessment(false);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initModernLearningPage, { once: true });
    } else {
        initModernLearningPage();
    }
})();
