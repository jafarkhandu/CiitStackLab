document.addEventListener('DOMContentLoaded', () => {
    const topicSelect = document.getElementById('contentTopic');
    const noteSelect = document.getElementById('contentNote');

    if (topicSelect && noteSelect) {
        const loadNotes = async (topicId, selectedNoteId = '') => {
            noteSelect.innerHTML = '<option value="0">Loading note chapters...</option>';
            noteSelect.disabled = true;

            if (!topicId || topicId === '0') {
                noteSelect.innerHTML = '<option value="0">Select note chapter</option>';
                return;
            }

            try {
                const response = await fetch(`/Admin/Content/Notes?topicId=${encodeURIComponent(topicId)}`, {
                    headers: { 'Accept': 'application/json' }
                });
                if (!response.ok) throw new Error('Unable to load note chapters.');

                const notes = await response.json();
                noteSelect.innerHTML = '<option value="0">Select note chapter</option>';
                notes.forEach(note => {
                    const option = document.createElement('option');
                    option.value = note.id;
                    option.textContent = `Chapter ${note.sortOrder}: ${note.title}`;
                    option.selected = String(note.id) === String(selectedNoteId);
                    noteSelect.appendChild(option);
                });

                noteSelect.disabled = notes.length === 0;
                if (notes.length === 0) {
                    noteSelect.innerHTML = '<option value="0">No notes available for this topic</option>';
                }
            } catch {
                noteSelect.innerHTML = '<option value="0">Unable to load notes</option>';
                noteSelect.disabled = true;
            }
        };

        topicSelect.addEventListener('change', () => loadNotes(topicSelect.value));
        if (topicSelect.value && topicSelect.value !== '0') {
            loadNotes(topicSelect.value, noteSelect.value);
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
            if (!window.confirm(`Move "${name}" to the archive?`)) event.preventDefault();
        });
    });
});
