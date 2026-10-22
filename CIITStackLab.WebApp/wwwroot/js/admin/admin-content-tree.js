document.addEventListener('DOMContentLoaded', () => {
    const loadContentModalAssets = () => {
        if (!document.querySelector('link[data-ciit-content-modal-css]')) {
            const stylesheet = document.createElement('link');
            stylesheet.rel = 'stylesheet';
            stylesheet.href = '/css/admin/admin-content-modal.css?v=1';
            stylesheet.dataset.ciitContentModalCss = 'true';
            document.head.appendChild(stylesheet);
        }

        if (!document.querySelector('script[data-ciit-content-modal-js]')) {
            const script = document.createElement('script');
            script.src = '/js/admin/admin-content-modal.js?v=1';
            script.dataset.ciitContentModalJs = 'true';
            document.body.appendChild(script);
        }
    };

    loadContentModalAssets();

    const search = document.getElementById('contentSearch');
    const tree = document.getElementById('contentTree');
    const empty = document.getElementById('contentSearchEmpty');
    const visibleCount = document.getElementById('contentVisibleCount');

    if (!search || !tree) return;

    const nodes = Array.from(tree.querySelectorAll('[data-tree-node]'));
    const contentItems = Array.from(tree.querySelectorAll('[data-content-item]'));

    const closeSiblingNodes = node => {
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

    const initializeSummaryCards = () => {
        const cards = Array.from(document.querySelectorAll('.ciit-content-overview-card'));
        if (!cards.length) return;

        if (!document.querySelector('#ciitContentSummaryModalStyles')) {
            const style = document.createElement('style');
            style.id = 'ciitContentSummaryModalStyles';
            style.textContent = `
                .ciit-content-overview {
                    display: grid !important;
                    grid-template-columns: repeat(3, minmax(0, 1fr)) !important;
                    gap: 14px !important;
                }
                .ciit-content-overview-card {
                    min-height: 86px;
                    cursor: pointer;
                    user-select: none;
                    transition: transform .18s ease, box-shadow .18s ease, border-color .18s ease;
                }
                .ciit-content-overview-card:hover {
                    transform: translateY(-2px);
                    border-color: #d7dee8;
                    box-shadow: 0 12px 28px rgba(17,24,39,.08);
                }
                .ciit-content-overview-card:focus-visible {
                    outline: 3px solid rgba(37,99,235,.18);
                    outline-offset: 2px;
                }
                .ciit-content-summary-modal {
                    position: fixed;
                    inset: 0;
                    z-index: 3000;
                    display: grid;
                    place-items: center;
                    padding: 24px;
                    background: rgba(15,23,42,.56);
                    backdrop-filter: blur(6px);
                    opacity: 0;
                    visibility: hidden;
                    pointer-events: none;
                    transition: opacity .2s ease, visibility 0s linear .2s;
                }
                .ciit-content-summary-modal.is-open {
                    opacity: 1;
                    visibility: visible;
                    pointer-events: auto;
                    transition: opacity .2s ease;
                }
                .ciit-content-summary-dialog {
                    width: min(900px, 100%);
                    max-height: min(760px, calc(100vh - 48px));
                    display: flex;
                    flex-direction: column;
                    overflow: hidden;
                    background: #fff;
                    border: 1px solid #e2e8f0;
                    border-radius: 18px;
                    box-shadow: 0 30px 90px rgba(15,23,42,.28);
                    transform: translateY(18px) scale(.98);
                    transition: transform .22s cubic-bezier(.2,.8,.2,1);
                }
                .ciit-content-summary-modal.is-open .ciit-content-summary-dialog {
                    transform: translateY(0) scale(1);
                }
                .ciit-content-summary-header {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    gap: 16px;
                    padding: 20px 22px;
                    border-bottom: 1px solid #edf0f3;
                }
                .ciit-content-summary-kicker {
                    display: block;
                    margin-bottom: 5px;
                    color: #64748b;
                    font: 600 10px/1.2 'Fira Code', monospace;
                    letter-spacing: .1em;
                    text-transform: uppercase;
                }
                .ciit-content-summary-title {
                    margin: 0;
                    color: #111827;
                    font-size: 21px;
                    font-weight: 800;
                }
                .ciit-content-summary-close {
                    width: 38px;
                    height: 38px;
                    display: grid;
                    place-items: center;
                    border: 0;
                    border-radius: 10px;
                    background: #f1f5f9;
                    color: #475569;
                    cursor: pointer;
                }
                .ciit-content-summary-body {
                    overflow-y: auto;
                    overscroll-behavior: contain;
                    padding: 16px 20px 22px;
                }
                .ciit-content-summary-list { display: grid; gap: 8px; }
                .ciit-content-summary-row {
                    display: grid;
                    grid-template-columns: minmax(0,1fr) auto;
                    align-items: center;
                    gap: 16px;
                    padding: 13px 14px;
                    border: 1px solid #e8edf2;
                    border-radius: 11px;
                    background: #fbfcfe;
                    transition: background .15s ease, border-color .15s ease, transform .15s ease;
                }
                .ciit-content-summary-row:hover {
                    background: #fff;
                    border-color: #d9e2ec;
                    transform: translateX(2px);
                }
                .ciit-content-summary-row-main {
                    display: flex;
                    align-items: center;
                    gap: 11px;
                    min-width: 0;
                }
                .ciit-content-summary-row-icon {
                    width: 36px;
                    height: 36px;
                    display: grid;
                    place-items: center;
                    flex: 0 0 36px;
                    border-radius: 9px;
                    background: #eef2ff;
                    color: #3b4acb;
                }
                .ciit-content-summary-row-icon.is-course { background: #eff6ff; color: #2563eb; }
                .ciit-content-summary-row-icon.is-topic { background: #f5f3ff; color: #7c3aed; }
                .ciit-content-summary-row-copy { display: grid; gap: 3px; min-width: 0; }
                .ciit-content-summary-row-copy strong,
                .ciit-content-summary-row-copy span {
                    overflow: hidden;
                    text-overflow: ellipsis;
                    white-space: nowrap;
                }
                .ciit-content-summary-row-copy strong { color: #1f2937; font-size: 13px; }
                .ciit-content-summary-row-copy span { color: #7b8491; font-size: 11px; }
                .ciit-content-summary-row-meta {
                    display: flex;
                    align-items: center;
                    justify-content: flex-end;
                    flex-wrap: wrap;
                    gap: 6px;
                }
                .ciit-content-summary-count {
                    min-width: 30px;
                    height: 26px;
                    display: grid;
                    place-items: center;
                    padding: 0 8px;
                    border-radius: 999px;
                    background: #f1f5f9;
                    color: #475569;
                    font-size: 11px;
                    font-weight: 700;
                }
                .ciit-content-summary-badge {
                    display: inline-flex;
                    align-items: center;
                    gap: 5px;
                    padding: 5px 8px;
                    border-radius: 7px;
                    font-size: 10px;
                    font-weight: 700;
                }
                .ciit-content-summary-badge.is-video { background: #eff6ff; color: #2563eb; }
                .ciit-content-summary-badge.is-slides { background: #f5f3ff; color: #6d28d9; }
                .ciit-content-summary-empty { padding: 45px 20px; color: #64748b; text-align: center; }
                .ciit-content-summary-empty i { display: block; margin-bottom: 9px; font-size: 30px; opacity: .45; }
                .ciit-content-summary-empty strong { display: block; color: #334155; font-size: 15px; }
                .ciit-content-summary-empty span { display: block; margin-top: 4px; font-size: 12px; }
                body.ciit-content-summary-open { overflow: hidden; }
                @media (max-width: 900px) {
                    .ciit-content-overview { grid-template-columns: 1fr !important; }
                    .ciit-content-summary-dialog { max-height: calc(100vh - 28px); }
                }
                @media (max-width: 640px) {
                    .ciit-content-summary-modal { padding: 12px; }
                    .ciit-content-summary-dialog { max-height: calc(100vh - 24px); border-radius: 15px; }
                    .ciit-content-summary-header { padding: 16px; }
                    .ciit-content-summary-body { padding: 12px 12px 16px; }
                    .ciit-content-summary-row { grid-template-columns: 1fr; gap: 10px; }
                    .ciit-content-summary-row-meta { justify-content: flex-start; padding-left: 47px; }
                }
                @media (prefers-reduced-motion: reduce) {
                    .ciit-content-overview-card,
                    .ciit-content-summary-dialog,
                    .ciit-content-summary-row { transition: none; }
                }
            `;
            document.head.appendChild(style);
        }

        const modal = document.createElement('div');
        modal.className = 'ciit-content-summary-modal';
        modal.setAttribute('aria-hidden', 'true');
        modal.innerHTML = `
            <div class="ciit-content-summary-dialog" role="dialog" aria-modal="true" aria-labelledby="ciitContentSummaryTitle">
                <header class="ciit-content-summary-header">
                    <div>
                        <span class="ciit-content-summary-kicker">Content overview</span>
                        <h2 id="ciitContentSummaryTitle" class="ciit-content-summary-title"></h2>
                    </div>
                    <button type="button" class="ciit-content-summary-close" aria-label="Close summary">
                        <i class="bi bi-x-lg" aria-hidden="true"></i>
                    </button>
                </header>
                <div class="ciit-content-summary-body">
                    <div class="ciit-content-summary-list"></div>
                </div>
            </div>
        `;
        document.body.appendChild(modal);

        const title = modal.querySelector('.ciit-content-summary-title');
        const list = modal.querySelector('.ciit-content-summary-list');
        const closeButton = modal.querySelector('.ciit-content-summary-close');
        let activeCard = null;

        const text = value => document.createTextNode(value ?? '');

        const addRow = (icon, iconClass, name, secondary, metaNodes) => {
            const row = document.createElement('article');
            row.className = 'ciit-content-summary-row';

            const main = document.createElement('div');
            main.className = 'ciit-content-summary-row-main';
            const iconBox = document.createElement('div');
            iconBox.className = `ciit-content-summary-row-icon ${iconClass || ''}`;
            const iconElement = document.createElement('i');
            iconElement.className = `bi ${icon}`;
            iconBox.appendChild(iconElement);

            const copy = document.createElement('div');
            copy.className = 'ciit-content-summary-row-copy';
            const strong = document.createElement('strong');
            strong.appendChild(text(name));
            const span = document.createElement('span');
            span.appendChild(text(secondary));
            copy.append(strong, span);
            main.append(iconBox, copy);

            const meta = document.createElement('div');
            meta.className = 'ciit-content-summary-row-meta';
            metaNodes.forEach(node => meta.appendChild(node));
            row.append(main, meta);
            list.appendChild(row);
        };

        const countBadge = value => {
            const badge = document.createElement('span');
            badge.className = 'ciit-content-summary-count';
            badge.appendChild(text(String(value)));
            return badge;
        };

        const mediaBadge = (icon, label, className) => {
            const badge = document.createElement('span');
            badge.className = `ciit-content-summary-badge ${className}`;
            const iconElement = document.createElement('i');
            iconElement.className = `bi ${icon}`;
            badge.append(iconElement, text(label));
            return badge;
        };

        const showEmpty = message => {
            list.innerHTML = '';
            const emptyState = document.createElement('div');
            emptyState.className = 'ciit-content-summary-empty';
            const icon = document.createElement('i');
            icon.className = 'bi bi-inbox';
            const strong = document.createElement('strong');
            strong.appendChild(text('Nothing to show'));
            const span = document.createElement('span');
            span.appendChild(text(message));
            emptyState.append(icon, strong, span);
            list.appendChild(emptyState);
        };

        const buildActiveContent = () => {
            list.innerHTML = '';
            if (!contentItems.length) {
                showEmpty('There is no active content in the current hierarchy.');
                return;
            }

            contentItems.forEach(item => {
                const titleElement = item.querySelector('.ciit-content-item-copy strong');
                const idElement = item.querySelector('.ciit-content-item-copy span');
                const media = Array.from(item.querySelectorAll('.ciit-media-badge'));
                const badges = [];
                if (media.some(x => x.classList.contains('is-video'))) badges.push(mediaBadge('bi-play-circle', 'Video', 'is-video'));
                if (media.some(x => x.classList.contains('is-slides'))) badges.push(mediaBadge('bi-file-slides', 'Slides', 'is-slides'));
                if (!badges.length) badges.push(mediaBadge('bi-dash-circle', 'No media', 'is-slides'));

                const chapter = item.closest('.ciit-tree-chapter');
                const topic = chapter?.closest('.ciit-tree-topic');
                const course = topic?.closest('.ciit-tree-course');
                const chapterName = chapter?.querySelector(':scope > summary .ciit-tree-label strong')?.textContent?.trim() || 'General content';
                const topicName = topic?.querySelector(':scope > summary .ciit-tree-label strong')?.textContent?.trim() || 'Topic';
                const courseName = course?.querySelector(':scope > summary .ciit-tree-label strong')?.textContent?.trim() || 'Course';
                const secondary = `${courseName} / ${topicName} / ${chapterName} · ${idElement?.textContent?.trim() || 'Content'}`;
                addRow('bi-file-earmark-text', '', titleElement?.textContent?.trim() || 'Untitled content', secondary, badges);
            });
        };

        const buildCourses = () => {
            list.innerHTML = '';
            const courses = Array.from(tree.querySelectorAll(':scope > details.ciit-tree-course'));
            if (!courses.length) {
                showEmpty('No courses are available in the current content hierarchy.');
                return;
            }

            courses.forEach(course => {
                const name = course.querySelector(':scope > summary .ciit-tree-label strong')?.textContent?.trim() || 'Untitled course';
                const courseItems = Array.from(course.querySelectorAll('[data-content-item]'));
                const topics = course.querySelectorAll(':scope > .ciit-tree-children > details.ciit-tree-topic').length;
                addRow('bi-mortarboard', 'is-course', name, `${topics} topic${topics === 1 ? '' : 's'} · active learning content`, [countBadge(courseItems.length)]);
            });
        };

        const buildTopics = () => {
            list.innerHTML = '';
            const topics = Array.from(tree.querySelectorAll('details.ciit-tree-topic'));
            if (!topics.length) {
                showEmpty('No topics are available in the current content hierarchy.');
                return;
            }

            topics.forEach(topic => {
                const name = topic.querySelector(':scope > summary .ciit-tree-label strong')?.textContent?.trim() || 'Untitled topic';
                const course = topic.closest('.ciit-tree-course');
                const courseName = course?.querySelector(':scope > summary .ciit-tree-label strong')?.textContent?.trim() || 'Course';
                const topicItems = Array.from(topic.querySelectorAll('[data-content-item]'));
                const chapters = topic.querySelectorAll(':scope > .ciit-tree-children > details.ciit-tree-chapter').length;
                addRow('bi-diagram-3', 'is-topic', name, `${courseName} · ${chapters} chapter${chapters === 1 ? '' : 's'}`, [countBadge(topicItems.length)]);
            });
        };

        const openSummary = card => {
            activeCard = card;
            const label = card.querySelector('span')?.textContent?.trim() || 'Overview';
            title.textContent = label;
            const type = label.toLowerCase();
            if (type.includes('active')) buildActiveContent();
            else if (type.includes('course')) buildCourses();
            else buildTopics();

            modal.classList.add('is-open');
            modal.setAttribute('aria-hidden', 'false');
            document.body.classList.add('ciit-content-summary-open');
            closeButton.focus();
        };

        const closeSummary = () => {
            modal.classList.remove('is-open');
            modal.setAttribute('aria-hidden', 'true');
            document.body.classList.remove('ciit-content-summary-open');
            if (activeCard) activeCard.focus();
        };

        cards.forEach(card => {
            card.setAttribute('role', 'button');
            card.setAttribute('tabindex', '0');
            card.setAttribute('aria-haspopup', 'dialog');
            card.addEventListener('click', () => openSummary(card));
            card.addEventListener('keydown', event => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    openSummary(card);
                }
            });
        });

        closeButton.addEventListener('click', closeSummary);
        modal.addEventListener('click', event => {
            if (event.target === modal) closeSummary();
        });
        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && modal.classList.contains('is-open')) {
                event.preventDefault();
                closeSummary();
            }
        });
    };

    initializeSummaryCards();
});
