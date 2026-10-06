document.addEventListener('DOMContentLoaded', () => {
    const courseSelect = document.getElementById('contentCourse');
    const topicSelect = document.getElementById('contentTopic');

    if (courseSelect && topicSelect) {
        const loadTopics = async (courseId, selectedTopicId = '') => {
            topicSelect.innerHTML = '<option value="0">Loading topics...</option>';
            topicSelect.disabled = true;

            if (!courseId || courseId === '0') {
                topicSelect.innerHTML = '<option value="0">Select topic</option>';
                return;
            }

            try {
                const response = await fetch(`/Admin/AdminContent/Topics?courseId=${encodeURIComponent(courseId)}`, {
                    headers: { 'Accept': 'application/json' }
                });

                if (!response.ok) throw new Error('Unable to load topics.');

                const topics = await response.json();
                topicSelect.innerHTML = '<option value="0">Select topic</option>';

                topics.forEach(topic => {
                    const option = document.createElement('option');
                    option.value = topic.id;
                    option.textContent = topic.title;
                    option.selected = String(topic.id) === String(selectedTopicId);
                    topicSelect.appendChild(option);
                });

                topicSelect.disabled = topics.length === 0;
                if (topics.length === 0) {
                    topicSelect.innerHTML = '<option value="0">No mapped topics</option>';
                }
            } catch {
                topicSelect.innerHTML = '<option value="0">Unable to load topics</option>';
                topicSelect.disabled = true;
            }
        };

        courseSelect.addEventListener('change', () => loadTopics(courseSelect.value));

        if (courseSelect.value && courseSelect.value !== '0') {
            topicSelect.disabled = false;
        }
    }

    const search = document.getElementById('contentSearch');
    const rows = Array.from(document.querySelectorAll('[data-content-row]'));
    const empty = document.getElementById('contentSearchEmpty');
    const visibleCount = document.getElementById('contentVisibleCount');

    if (search) {
        const updateSearch = () => {
            const term = search.value.trim().toLowerCase();
            let count = 0;

            rows.forEach(row => {
                const match = !term || (row.dataset.search || '').toLowerCase().includes(term);
                row.hidden = !match;
                if (match) count++;
            });

            if (visibleCount) visibleCount.textContent = count;
            if (empty) empty.hidden = count !== 0 || rows.length === 0;
        };

        search.addEventListener('input', updateSearch);
    }

    document.querySelectorAll('.js-content-delete-form').forEach(form => {
        form.addEventListener('submit', event => {
            const name = form.dataset.contentName || 'this content';
            if (!window.confirm(`Move "${name}" to the archive?`)) {
                event.preventDefault();
            }
        });
    });
});
