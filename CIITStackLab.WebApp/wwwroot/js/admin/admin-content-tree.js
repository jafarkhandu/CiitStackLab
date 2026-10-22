document.addEventListener('DOMContentLoaded', () => {
    const search = document.getElementById('contentSearch');
    const tree = document.getElementById('contentTree');
    const empty = document.getElementById('contentSearchEmpty');
    const visibleCount = document.getElementById('contentVisibleCount');

    if (!search || !tree) return;

    const nodes = Array.from(tree.querySelectorAll('[data-tree-node]'));
    const contentItems = Array.from(tree.querySelectorAll('[data-content-item]'));

    const closeSiblingNodes = (node) => {
        const parent = node.parentElement;
        if (!parent) return;

        Array.from(parent.children).forEach(child => {
            if (child !== node && child.matches('details[data-tree-node]')) {
                child.open = false;
            }
        });
    };

    nodes.forEach(node => {
        node.addEventListener('toggle', () => {
            if (node.open && !tree.classList.contains('is-searching')) {
                closeSiblingNodes(node);
            }
        });
    });

    const nodeMatches = (node, term) =>
        (node.dataset.search || '').toLowerCase().includes(term);

    const updateSearch = () => {
        const term = search.value.trim().toLowerCase();

        if (!term) {
            contentItems.forEach(item => {
                item.hidden = false;
            });

            nodes.forEach(node => {
                node.hidden = false;
                node.open = false;
            });

            if (visibleCount) visibleCount.textContent = contentItems.length;
            if (empty) empty.hidden = contentItems.length !== 0;
            tree.classList.remove('is-searching');
            return;
        }

        tree.classList.add('is-searching');

        let matchedCount = 0;
        const contentMatches = new Map();

        contentItems.forEach(item => {
            const match = (item.dataset.search || '').toLowerCase().includes(term);
            contentMatches.set(item, match);
            item.hidden = !match;
            if (match) matchedCount++;
        });

        // Work from the deepest level upward so a matching child keeps every
        // parent in the visible search path.
        [...nodes].reverse().forEach(node => {
            const directMatch = nodeMatches(node, term);
            const descendantMatch = Array.from(node.querySelectorAll('[data-content-item]'))
                .some(item => contentMatches.get(item) === true);
            const childNodeMatch = Array.from(node.children)
                .some(child => child.matches('details[data-tree-node]') && !child.hidden);

            const visible = directMatch || descendantMatch || childNodeMatch;
            node.hidden = !visible;

            if (visible) {
                node.open = true;
            }
        });

        // If a parent itself matches, keep its complete branch visible.
        nodes.forEach(node => {
            if (nodeMatches(node, term)) {
                node.hidden = false;
                node.open = true;
                node.querySelectorAll('[data-tree-node], [data-content-item]').forEach(child => {
                    child.hidden = false;
                });
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
