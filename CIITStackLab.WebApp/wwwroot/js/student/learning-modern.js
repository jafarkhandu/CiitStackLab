(function () {
    function ensureReferenceStyles() {
        const styleId = 'ciit-learning-layout-fixes';
        if (document.getElementById(styleId)) {
            return;
        }

        const style = document.createElement('style');
        style.id = styleId;
        style.textContent = `
            .ciit-learning-mode .ciit-learning-main-card {
                width: 100%;
                max-width: 780px;
                margin: 0 auto;
                padding: 32px 44px 36px;
                border-radius: 22px;
                background: rgba(255, 255, 255, .88);
                backdrop-filter: none;
                -webkit-backdrop-filter: none;
            }

            .ciit-learning-mode .ciit-learning-resource-title {
                display: none;
            }

            .ciit-learning-mode .ciit-learning-notes-panel,
            .ciit-learning-mode .ciit-learning-notes-content {
                margin: 0;
                padding: 0;
                border: 0;
                border-radius: 0;
                background: transparent;
                box-shadow: none;
            }

            .ciit-learning-mode .ciit-learning-lesson-heading {
                margin: 0 0 8px;
            }

            .ciit-learning-mode .ciit-learning-lesson-heading .ciit-learning-eyebrow {
                margin-bottom: 4px;
            }

            .ciit-learning-mode .ciit-learning-lesson-heading .ciit-learning-chapter-title {
                margin: 0;
                font-size: 32px;
                line-height: 1.2;
                font-weight: 800;
                letter-spacing: -.7px;
            }

            .ciit-learning-mode .ciit-learning-topic-step-progress {
                display: none;
            }

            .ciit-learning-mode .ciit-learning-notes-content h2,
            .ciit-learning-mode .ciit-learning-notes-content h3,
            .ciit-learning-mode .ciit-learning-notes-content h4 {
                scroll-margin-top: 98px;
            }

            html:has(.ciit-learning-mode) {
                scroll-behavior: smooth;
            }

            @media (min-width: 1200px) {
                .ciit-learning-mode .ciit-learning-shell {
                    display: grid !important;
                    grid-template-columns: minmax(0, 1fr) 310px !important;
                    grid-template-areas: "main sidebar";
                    width: min(1110px, 100%) !important;
                    max-width: 1110px !important;
                    margin-right: auto;
                    margin-left: auto;
                }

                .ciit-learning-mode .ciit-learning-main {
                    grid-area: main;
                    display: flex;
                    justify-content: center;
                    min-width: 0;
                }

                .ciit-learning-mode .ciit-learning-shell > .ciit-learning-sidebar {
                    grid-area: sidebar;
                    width: 310px;
                }
            }

            @media (min-width: 992px) and (max-width: 1199.98px) {
                .ciit-learning-mode .ciit-learning-shell {
                    display: grid !important;
                    grid-template-columns: minmax(0, 1fr) !important;
                    grid-template-areas: "main" "sidebar";
                    width: 100% !important;
                    max-width: 780px !important;
                    margin-right: auto;
                    margin-left: auto;
                }

                .ciit-learning-mode .ciit-learning-main {
                    grid-area: main;
                }

                .ciit-learning-mode .ciit-learning-shell > .ciit-learning-sidebar {
                    grid-area: sidebar;
                    position: static !important;
                    width: 100%;
                    height: auto !important;
                    max-height: 42vh !important;
                }

                .ciit-learning-mode .ciit-learning-main-card {
                    padding: 28px 32px;
                }
            }

            @media (max-width: 991.98px) {
                .ciit-learning-mode .ciit-learning-shell > .ciit-learning-sidebar {
                    grid-area: sidebar;
                }

                .ciit-learning-mode .ciit-learning-main {
                    grid-area: main;
                }
            }

            @media (max-width: 767.98px) {
                .ciit-learning-mode .ciit-learning-main-card {
                    padding: 22px 18px 26px;
                    border-radius: 18px;
                }
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

        // Notes are the primary learning surface. Remove the legacy section pager entirely
        // so the lesson flows directly from the module title into the exam action and notes.
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

        // The Learn sidebar is scoped to the selected topic and shows only its chapter links.
        // Topic switching stays in the existing learning flow; the sidebar avoids cross-topic clutter.

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
