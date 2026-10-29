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

        // The course-level hero is hidden on the modern lesson page. Keep the Notes /
        // Assessments navigation, but place it inside the lesson card after the topic heading
        // so the page starts directly with the selected topic instead of a duplicate course hero.
        if (panelPager && mainCard && lessonHeader) {
            lessonHeader.insertAdjacentElement('afterend', panelPager);
        }

        const contentButton = document.querySelector('[data-learning-page="content"]');
        const contentPanel = document.querySelector('[data-learning-panel-content="content"]');
        const notesPanel = document.querySelector('[data-learning-panel-content="notes"]');

        if (contentButton) {
            contentButton.remove();
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

        const notesButton = document.querySelector('[data-learning-page="notes"]');
        if (notesButton) {
            window.setTimeout(function () {
                notesButton.click();
            }, 0);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initModernLearningPage, { once: true });
    } else {
        initModernLearningPage();
    }
})();
