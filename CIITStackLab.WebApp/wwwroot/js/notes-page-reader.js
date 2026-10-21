(() => {
    const content = document.querySelector('.ciit-notes-reader-content');
    const reader = document.querySelector('.ciit-notes-page-reader');

    if (!content || !reader) {
        return;
    }

    const progressKey = reader.dataset.progressKey;
    const controls = reader.querySelector('.ciit-notes-page-controls');
    const status = reader.querySelector('.ciit-notes-page-status');
    const previousButton = reader.querySelector('[data-page-previous]');
    const nextButton = reader.querySelector('[data-page-next]');
    const markReadButton = reader.querySelector('[data-page-mark-read]');
    const originalNodes = Array.from(content.childNodes);
    const pageHeight = () => Math.max(420, Math.floor(window.innerHeight * 0.68));

    let pages = [];
    let currentPageIndex = 0;
    let resizeFrame = null;

    const readProgress = () => {
        try {
            const storedValue = window.localStorage.getItem(progressKey);
            if (storedValue === null) {
                return -1;
            }

            const value = Number(storedValue);
            return Number.isFinite(value) && value >= 0 ? value : -1;
        } catch {
            return -1;
        }
    };

    const saveProgress = endIndex => {
        try {
            window.localStorage.setItem(progressKey, String(endIndex));
        } catch {
            // Keep the reader usable when browser storage is unavailable.
        }
    };

    const createPage = startIndex => {
        const page = document.createElement('div');
        page.className = 'ciit-notes-page';
        page.dataset.startIndex = String(startIndex);
        page.dataset.endIndex = String(startIndex);
        return page;
    };

    const buildPages = () => {
        content.replaceChildren();

        if (originalNodes.length === 0) {
            controls.hidden = true;
            return;
        }

        const maxHeight = pageHeight();
        let page = createPage(0);
        let pageHasContent = false;
        pages = [];
        content.appendChild(page);

        originalNodes.forEach((node, index) => {
            page.appendChild(node);
            pageHasContent = true;
            page.dataset.endIndex = String(index);

            if (page.scrollHeight > maxHeight && page.childNodes.length > 1) {
                page.removeChild(node);
                page.dataset.endIndex = String(index - 1);
                pages.push(page);

                page = createPage(index);
                content.appendChild(page);
                page.appendChild(node);
                page.dataset.endIndex = String(index);
            }
        });

        if (pageHasContent) {
            pages.push(page);
        }

        if (pages.length <= 1) {
            content.replaceChildren(...originalNodes);
            controls.hidden = true;
            return;
        }

        controls.hidden = false;

        const lastReadEndIndex = readProgress();
        const resumeIndex = pages.findIndex(
            item => Number(item.dataset.startIndex) > lastReadEndIndex);

        if (resumeIndex >= 0) {
            currentPageIndex = resumeIndex;
        } else if (lastReadEndIndex >= 0) {
            currentPageIndex = pages.length - 1;
        } else {
            currentPageIndex = 0;
        }

        showPage(currentPageIndex);
    };

    const showPage = index => {
        if (pages.length === 0) {
            return;
        }

        currentPageIndex = Math.max(0, Math.min(index, pages.length - 1));

        pages.forEach((page, pageIndex) => {
            page.hidden = pageIndex !== currentPageIndex;
        });

        status.textContent = `Page ${currentPageIndex + 1} of ${pages.length}`;
        previousButton.disabled = currentPageIndex === 0;
        nextButton.disabled = currentPageIndex === pages.length - 1;

        const lastReadEndIndex = readProgress();
        const pageEndIndex = Number(pages[currentPageIndex].dataset.endIndex);
        markReadButton.textContent = pageEndIndex <= lastReadEndIndex
            ? 'Read'
            : currentPageIndex === pages.length - 1
                ? 'Mark Final Page as Read'
                : 'Mark as Read';

        markReadButton.classList.toggle(
            'is-read',
            pageEndIndex <= lastReadEndIndex);

        window.scrollTo({
            top: content.getBoundingClientRect().top + window.scrollY - 24,
            behavior: 'auto'
        });
    };

    previousButton.addEventListener('click', () => {
        showPage(currentPageIndex - 1);
    });

    nextButton.addEventListener('click', () => {
        showPage(currentPageIndex + 1);
    });

    markReadButton.addEventListener('click', () => {
        const currentPage = pages[currentPageIndex];
        const endIndex = Number(currentPage.dataset.endIndex);
        saveProgress(Math.max(readProgress(), endIndex));

        if (currentPageIndex < pages.length - 1) {
            showPage(currentPageIndex + 1);
        } else {
            showPage(currentPageIndex);
        }
    });

    const rebuildOnResize = () => {
        if (resizeFrame !== null) {
            window.cancelAnimationFrame(resizeFrame);
        }

        resizeFrame = window.requestAnimationFrame(() => {
            resizeFrame = null;
            buildPages();
        });
    };

    if (document.readyState === 'complete') {
        buildPages();
    } else {
        window.addEventListener('load', buildPages, { once: true });
    }

    window.addEventListener('resize', rebuildOnResize);
})();
