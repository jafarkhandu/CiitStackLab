(function () {
    function ensureReferenceStyles() {
        const styleId = 'ciit-learn-reference-styles';
        if (document.getElementById(styleId)) {
            return;
        }

        const stylesheet = document.createElement('link');
        stylesheet.id = styleId;
        stylesheet.rel = 'stylesheet';
        stylesheet.href = '/css/student/learn-reference.css?v=1';
        document.head.appendChild(stylesheet);
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
            assessmentTarget?.scrollIntoView({ behavior: 'smooth', block: 'start' });
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
