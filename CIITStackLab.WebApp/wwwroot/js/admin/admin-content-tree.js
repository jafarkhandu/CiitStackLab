document.addEventListener('DOMContentLoaded', () => {
    const search = document.getElementById('contentSearch');
    const tree = document.getElementById('contentTree');
    const empty = document.getElementById('contentSearchEmpty');
    const visibleCount = document.getElementById('contentVisibleCount');

    if (!search || !tree) return;

    const nodes = Array.from(tree.querySelectorAll('[data-tree-node]'));
    const contentItems = Array.from(tree.querySelectorAll('[data-content-item]'));

    const nodeMatches = (node, term) => (node.dataset.search || '').toLowerCase().includes(term);

    const updateSearch = () => {
        const term = search.value.trim().toLowerCase();
        let matchedCount = 0;

        if (!term) {
            contentItems.forEach(item => {
                item.hidden = false;
            });

            nodes.forEach(node => {
                node.hidden = false;
            });

            if (visibleCount) visibleCount.textContent = contentItems.length;
            if (empty) empty.hidden = contentItems.length !== 0;
            tree.closest('.ciit-content-tree-card')?.classList.remove('is-searching');
            return;
        }

        tree.closest('.ciit-content-tree-card')?.classList.add('is-searching');

        contentItems.forEach(item => {
            const match = (item.dataset.search || '').toLowerCase().includes(term);
            item.hidden = !match;
            if (match) matchedCount++;
        });

        nodes.forEach(node => {
            const directMatch = nodeMatches(node, term);
            const descendantContentMatch = Array.from(node.querySelectorAll('[data-content-item]'))
                .some(item => !item.hidden);
            const visible = directMatch || descendantContentMatch;

            node.hidden = !visible;

            if (visible && (directMatch || descendantContentMatch)) {
                node.open = true;
            }
        });

        if (visibleCount) visibleCount.textContent = matchedCount;
        if (empty) empty.hidden = matchedCount !== 0;
    };

    search.addEventListener('input', updateSearch);

    search.addEventListener('keydown', event => {
        if (event.key === 'Escape' && search.value) {
            search.value = '';
            updateSearch();
            search.focus();
        }
    });
});
