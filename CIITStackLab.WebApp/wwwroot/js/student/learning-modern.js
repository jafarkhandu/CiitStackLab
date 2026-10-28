(function () {
    function initModernLearningPage() {
        const learningPage = document.querySelector('.ciit-learning-page');
        if (!learningPage) {
            return;
        }

        const activeTopicTitle = document.querySelector('.ciit-learning-topic.is-active-topic > .ciit-learning-topic-heading strong');
        const lessonTitle = document.querySelector('.ciit-learning-lesson-heading .ciit-learning-chapter-title');
        const headerTitle = document.querySelector('.ciit-learning-course-intro h1');
        const headerDescription = document.querySelector('.ciit-learning-course-intro > p');
        const topicEyebrow = document.querySelector('.ciit-learning-lesson-heading .ciit-learning-eyebrow');

        if (headerTitle) {
            const topicTitle = activeTopicTitle?.textContent?.trim() || lessonTitle?.textContent?.trim();
            if (topicTitle) {
                headerTitle.textContent = topicTitle;
            }
        }

        if (topicEyebrow && headerDescription) {
            headerDescription.textContent = topicEyebrow.textContent.trim().replace(/\s+/g, ' ');
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

        const chapterSidebar = document.querySelector('.ciit-learning-shell > .ciit-learning-sidebar');
        if (chapterSidebar && notesPanel) {
            const navigation = document.createElement('section');
            navigation.className = 'ciit-learning-chapter-navigation';
            navigation.setAttribute('aria-label', 'Topic chapters');
            navigation.appendChild(chapterSidebar);
            notesPanel.insertBefore(navigation, notesPanel.firstChild);
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
