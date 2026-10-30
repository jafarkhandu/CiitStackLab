(function () {
    'use strict';

    function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text);
        }

        return new Promise(function (resolve, reject) {
            const textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.setAttribute('readonly', '');
            textarea.style.position = 'fixed';
            textarea.style.opacity = '0';
            document.body.appendChild(textarea);
            textarea.select();

            try {
                if (!document.execCommand('copy')) {
                    throw new Error('Clipboard copy was rejected.');
                }
                resolve();
            } catch (error) {
                reject(error);
            } finally {
                textarea.remove();
            }
        });
    }

    function initCodeCopyButtons(root) {
        root.querySelectorAll('pre > code').forEach(function (code) {
            const pre = code.parentElement;
            if (!pre || pre.querySelector('.ciit-learning-copy-code')) {
                return;
            }

            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'ciit-learning-copy-code';
            button.textContent = 'Copy';
            button.setAttribute('aria-label', 'Copy code block');

            button.addEventListener('click', function () {
                copyText(code.textContent || '')
                    .then(function () {
                        button.textContent = 'Copied';
                        window.setTimeout(function () {
                            button.textContent = 'Copy';
                        }, 1400);
                    })
                    .catch(function () {
                        button.textContent = 'Failed';
                        window.setTimeout(function () {
                            button.textContent = 'Copy';
                        }, 1400);
                    });
            });

            pre.appendChild(button);
        });
    }

    function getNotesRoot() {
        return document.querySelector('.ciit-learning-notes-content');
    }

    function createSubtopicId(heading, index) {
        const slug = (heading.textContent || '')
            .trim()
            .toLowerCase()
            .replace(/[^a-z0-9\s-]/g, '')
            .replace(/\s+/g, '-')
            .replace(/-+/g, '-')
            .replace(/^-|-$/g, '');

        return 'ciit-learning-subtopic-' + (slug || 'section') + '-' + index;
    }

    function buildSubtopics() {
        const notes = getNotesRoot();
        const activeChapter = document.querySelector('.ciit-learning-current-chapter-link.is-current');
        const sidebar = document.querySelector('.ciit-learning-sidebar');
        if (!notes || !activeChapter || !sidebar) {
            return;
        }

        const existing = activeChapter.nextElementSibling;
        if (existing && existing.matches('[data-learning-subtopics]')) {
            existing.remove();
        }

        const headings = Array.from(notes.querySelectorAll('h2, h3')).filter(function (heading) {
            return !heading.closest('.ciit-learning-resource-title')
                && !heading.classList.contains('ciit-learning-chapter-title');
        });

        if (!headings.length) {
            return;
        }

        const list = document.createElement('nav');
        list.className = 'ciit-learning-subtopics is-visible';
        list.setAttribute('data-learning-subtopics', '');
        list.setAttribute('aria-label', 'Chapter subtopics');

        headings.forEach(function (heading, index) {
            if (!heading.id) {
                heading.id = createSubtopicId(heading, index + 1);
            }

            const link = document.createElement('a');
            link.className = 'ciit-learning-subtopic-link';
            link.href = '#' + heading.id;
            link.textContent = heading.textContent.trim();
            link.dataset.subtopicTarget = heading.id;

            link.addEventListener('click', function (event) {
                event.preventDefault();
                heading.scrollIntoView({ behavior: 'smooth', block: 'start' });
                history.replaceState(null, '', window.location.pathname + window.location.search + '#' + heading.id);
            });

            list.appendChild(link);
        });

        activeChapter.insertAdjacentElement('afterend', list);
        observeSubtopics(headings, list);
    }

    function observeSubtopics(headings, list) {
        if (!('IntersectionObserver' in window) || !headings.length) {
            return;
        }

        const links = Array.from(list.querySelectorAll('[data-subtopic-target]'));
        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) {
                    return;
                }

                links.forEach(function (link) {
                    link.classList.toggle('is-active', link.dataset.subtopicTarget === entry.target.id);
                });
            });
        }, {
            root: null,
            rootMargin: '-96px 0px -60% 0px',
            threshold: 0
        });

        headings.forEach(function (heading) {
            observer.observe(heading);
        });
    }

    function removeEmptyResourceRows() {
        document.querySelectorAll('.ciit-learning-content-overview li').forEach(function (item) {
            const text = (item.textContent || '').trim().toLowerCase();
            if (text.includes('not provided for this lesson')) {
                item.remove();
            }
        });

        document.querySelectorAll('.ciit-learning-content-overview').forEach(function (section) {
            const list = section.querySelector('ul');
            if (list && !list.querySelector('li')) {
                section.remove();
            }
        });
    }

    function initResponsiveChapterPanel() {
        const sidebar = document.querySelector('.ciit-learning-sidebar');
        const head = sidebar?.querySelector('.ciit-learning-sidebar-head');
        if (!sidebar || !head) {
            return;
        }

        const media = window.matchMedia('(min-width: 768px) and (max-width: 1023px), (max-width: 767px)');
        if (!media.matches) {
            return;
        }

        head.setAttribute('role', 'button');
        head.setAttribute('tabindex', '0');
        head.setAttribute('aria-expanded', 'false');

        const toggle = function () {
            const expanded = sidebar.classList.toggle('is-expanded');
            head.setAttribute('aria-expanded', String(expanded));
        };

        head.addEventListener('click', toggle);
        head.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                toggle();
            }
        });
    }

    function init() {
        const page = document.querySelector('.ciit-learning-page');
        if (!page) {
            return;
        }

        initCodeCopyButtons(page);
        removeEmptyResourceRows();
        buildSubtopics();
        initResponsiveChapterPanel();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init, { once: true });
    } else {
        init();
    }
})();
