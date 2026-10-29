(function () {
    function initModernLearningPage() {
        const learningPage = document.querySelector('.ciit-learning-page');
        if (!learningPage) {
            return;
        }

        const lessonTitle = document.querySelector('.ciit-learning-lesson-heading .ciit-learning-chapter-title');
        const headerTitle = document.querySelector('.ciit-learning-course-intro h1');
        const headerDescription = document.querySelector('.ciit-learning-course-intro > p');
        const topicEyebrow = document.querySelector('.ciit-learning-lesson-heading .ciit-learning-eyebrow');
        const mainCard = document.querySelector('.ciit-learning-main-card');
        const lessonHeader = document.querySelector('.ciit-learning-lesson-header');
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

        // Keep only Notes in the lesson header. Assessments are opened from the global sidebar.
        if (panelPager && mainCard && lessonHeader) {
            lessonHeader.insertAdjacentElement('afterend', panelPager);
        }

        const contentButton = document.querySelector('[data-learning-page="content"]');
        const assessmentButton = document.querySelector('[data-learning-page="assessment"]');
        const contentPanel = document.querySelector('[data-learning-panel-content="content"]');
        const notesPanel = document.querySelector('[data-learning-panel-content="notes"]');
        const assessmentPanel = document.querySelector('[data-learning-panel-content="assessment"]');

        if (contentButton) {
            contentButton.remove();
        }

        if (assessmentButton) {
            assessmentButton.remove();
        }

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

        // The legacy learning-path sidebar is intentionally not moved into the modern Notes view.
        // Its chapter accordion duplicates the Notes/Resources/Assessment experience and leaves
        // legacy layout spacing behind. The modern page uses the global course navigation instead.
        const chapterSidebar = document.querySelector('.ciit-learning-shell > .ciit-learning-sidebar');
        if (chapterSidebar) {
            chapterSidebar.remove();
        }

        const assessmentLink = document.querySelector('[data-learning-assessment-link]');
        const learningPathLink = document.querySelector('[data-learning-path-link]');
        const notesButton = document.querySelector('[data-learning-page="notes"]');

        const showAssessment = function (updateHash) {
            if (!assessmentPanel) {
                return;
            }

            if (notesPanel) {
                notesPanel.hidden = true;
            }
            assessmentPanel.hidden = false;

            notesButton?.classList.remove('is-active');
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
        } else if (notesButton) {
            window.setTimeout(function () {
                notesButton.click();
                assessmentLink?.classList.remove('active');
                learningPathLink?.classList.add('active');
            }, 0);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initModernLearningPage, { once: true });
    } else {
        initModernLearningPage();
    }
})();
