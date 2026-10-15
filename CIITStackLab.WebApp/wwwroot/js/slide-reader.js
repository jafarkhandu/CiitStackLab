const PDF_JS_URL = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.min.mjs';
const PDF_WORKER_URL = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.worker.min.mjs';

const root = document.querySelector('.ciit-slide-reader');

if (root) {
    const slideUrl = root.dataset.slideUrl;
    const title = root.dataset.slideTitle || 'Slides';
    const stage = document.getElementById('slideStage');
    const thumbnails = document.getElementById('slideThumbnails');
    const loading = document.getElementById('slideLoading');
    const current = document.getElementById('slideCurrent');
    const total = document.getElementById('slideTotal');
    const controlCurrent = document.getElementById('slideControlCurrent');
    const controlTotal = document.getElementById('slideControlTotal');
    const previous = document.getElementById('slidePrevious');
    const next = document.getElementById('slideNext');
    const zoomOut = document.getElementById('slideZoomOut');
    const zoomIn = document.getElementById('slideZoomIn');
    const zoomLabel = document.getElementById('slideZoomLabel');
    const fit = document.getElementById('slideFit');
    const fullscreen = document.getElementById('slideFullscreen');

    const state = {
        pdf: null,
        page: 0,
        zoom: 1,
        pdfCanvas: null,
        pdfContext: null,
        renderTask: null,
        renderVersion: 0
    };

    const setLoading = (visible, message = 'Opening PDF') => {
        if (!loading) return;
        loading.hidden = !visible;
        const strong = loading.querySelector('strong');
        if (strong) strong.textContent = message;
    };

    const setPageInfo = () => {
        const count = state.pdf?.numPages || 0;
        const page = count > 0 ? state.page + 1 : 0;

        current.textContent = page;
        total.textContent = count || '—';
        controlCurrent.textContent = page;
        controlTotal.textContent = count || '—';
        previous.disabled = state.page <= 0;
        next.disabled = count === 0 || state.page >= count - 1;
    };

    const setZoomLabel = () => {
        zoomLabel.textContent = `${Math.round(state.zoom * 100)}%`;
    };

    const cancelCurrentRender = () => {
        state.renderVersion += 1;

        if (state.renderTask) {
            try {
                state.renderTask.cancel();
            } catch {
                // The task may already have completed.
            }
            state.renderTask = null;
        }
    };

    const renderError = message => {
        cancelCurrentRender();
        setLoading(false);
        stage.innerHTML = `
            <div class="ciit-slide-reader-error" role="alert">
                <i class="bi bi-exclamation-triangle"></i>
                <strong>Unable to display this PDF page</strong>
                <span>${message}</span>
            </div>`;
    };

    const renderPdfPage = async () => {
        if (!state.pdf) return;

        const renderVersion = ++state.renderVersion;

        if (state.renderTask) {
            try {
                state.renderTask.cancel();
            } catch {
                // The previous task may already have completed.
            }
            state.renderTask = null;
        }

        const page = await state.pdf.getPage(state.page + 1);
        if (renderVersion !== state.renderVersion) return;

        const baseViewport = page.getViewport({ scale: 1 });
        const availableWidth = Math.max(320, stage.clientWidth - 56);
        const availableHeight = Math.max(240, stage.clientHeight - 56);
        const fitScale = Math.min(
            availableWidth / baseViewport.width,
            availableHeight / baseViewport.height);
        const scale = fitScale * state.zoom;
        const viewport = page.getViewport({ scale });

        let canvas = state.pdfCanvas;
        if (!canvas) {
            canvas = document.createElement('canvas');
            canvas.className = 'ciit-slide-reader-pdf-canvas';
            stage.innerHTML = '';
            stage.appendChild(canvas);
            state.pdfCanvas = canvas;
            state.pdfContext = canvas.getContext('2d', { alpha: false });
        } else if (!stage.contains(canvas)) {
            stage.innerHTML = '';
            stage.appendChild(canvas);
        }

        canvas.width = Math.ceil(viewport.width);
        canvas.height = Math.ceil(viewport.height);
        canvas.style.width = `${Math.ceil(viewport.width)}px`;
        canvas.style.height = `${Math.ceil(viewport.height)}px`;

        state.renderTask = page.render({
            canvasContext: state.pdfContext,
            viewport
        });

        try {
            await state.renderTask.promise;
        } catch (error) {
            if (error?.name === 'RenderingCancelledException') return;
            throw error;
        } finally {
            if (renderVersion === state.renderVersion) {
                state.renderTask = null;
            }
        }

        if (renderVersion !== state.renderVersion) return;

        setLoading(false);
        setZoomLabel();
    };

    const selectThumbnail = () => {
        thumbnails.querySelectorAll('[data-slide-index]').forEach(item => {
            item.classList.toggle(
                'is-active',
                Number(item.dataset.slideIndex) === state.page);
        });

        const active = thumbnails.querySelector(
            `[data-slide-index="${state.page}"]`);
        active?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    };

    const renderCurrent = async () => {
        try {
            setPageInfo();
            selectThumbnail();
            await renderPdfPage();
        } catch (error) {
            console.error(error);
            renderError('This PDF page could not be rendered. Please try the page again.');
        }
    };

    const buildPdfThumbnails = async () => {
        thumbnails.innerHTML = '';

        for (let index = 0; index < state.pdf.numPages; index++) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'ciit-slide-thumbnail';
            button.dataset.slideIndex = String(index);
            button.setAttribute('aria-label', `Open page ${index + 1}`);

            const canvas = document.createElement('canvas');
            const number = document.createElement('span');
            number.textContent = String(index + 1);
            button.append(canvas, number);
            button.addEventListener('click', () => {
                state.page = index;
                renderCurrent();
            });
            thumbnails.appendChild(button);

            try {
                const page = await state.pdf.getPage(index + 1);
                const viewport = page.getViewport({ scale: 0.18 });
                canvas.width = Math.ceil(viewport.width);
                canvas.height = Math.ceil(viewport.height);

                await page.render({
                    canvasContext: canvas.getContext('2d'),
                    viewport
                }).promise;
            } catch (error) {
                console.warn(`Unable to render PDF thumbnail ${index + 1}`, error);
            }
        }

        selectThumbnail();
    };

    const loadPdf = async response => {
        const pdfjs = await import(PDF_JS_URL);
        pdfjs.GlobalWorkerOptions.workerSrc = PDF_WORKER_URL;

        const bytes = new Uint8Array(await response.arrayBuffer());
        if (bytes.length === 0) {
            throw new Error('The PDF file is empty.');
        }

        state.pdf = await pdfjs.getDocument({ data: bytes }).promise;

        if (!state.pdf.numPages) {
            throw new Error('The PDF does not contain any pages.');
        }

        setPageInfo();
        await buildPdfThumbnails();
        await renderCurrent();
    };

    const load = async () => {
        if (!slideUrl) {
            renderError('No PDF slide file is attached to this resource.');
            return;
        }

        setLoading(true, 'Opening PDF');

        try {
            const response = await fetch(slideUrl, {
                credentials: 'same-origin',
                cache: 'no-store'
            });

            if (!response.ok) {
                throw new Error(`Slide file returned HTTP ${response.status}.`);
            }

            const contentType = (response.headers.get('content-type') || '').toLowerCase();
            if (!contentType.includes('application/pdf')) {
                throw new Error('The attached slide file is not a PDF.');
            }

            await loadPdf(response);
        } catch (error) {
            console.error(error);
            renderError(error instanceof Error && error.message.includes('HTTP')
                ? 'The PDF file could not be loaded. Please try again.'
                : error instanceof Error && error.message
                    ? error.message
                    : 'This PDF could not be opened in the browser.');
        }
    };

    const changeZoom = delta => {
        state.zoom = Math.max(
            0.5,
            Math.min(2.5, Number((state.zoom + delta).toFixed(2))));
        renderCurrent();
    };

    previous.addEventListener('click', () => {
        if (state.page > 0) {
            state.page--;
            renderCurrent();
        }
    });

    next.addEventListener('click', () => {
        const count = state.pdf?.numPages || 0;
        if (state.page < count - 1) {
            state.page++;
            renderCurrent();
        }
    });

    zoomOut.addEventListener('click', () => changeZoom(-0.1));
    zoomIn.addEventListener('click', () => changeZoom(0.1));

    zoomLabel.addEventListener('click', () => {
        state.zoom = 1;
        renderCurrent();
    });

    fit.addEventListener('click', () => {
        state.zoom = 1;
        renderCurrent();
    });

    fullscreen.addEventListener('click', async () => {
        try {
            if (document.fullscreenElement) {
                await document.exitFullscreen();
            } else {
                await root.requestFullscreen();
            }
        } catch (error) {
            console.warn('Fullscreen is not available.', error);
        }
    });

    root.addEventListener('fullscreenchange', () => {
        const icon = fullscreen.querySelector('i');
        if (icon) {
            icon.className = document.fullscreenElement
                ? 'bi bi-fullscreen-exit'
                : 'bi bi-fullscreen';
        }
        window.requestAnimationFrame(() => renderCurrent());
    });

    stage.addEventListener('keydown', event => {
        if (event.key === 'ArrowLeft') {
            event.preventDefault();
            previous.click();
        } else if (event.key === 'ArrowRight') {
            event.preventDefault();
            next.click();
        } else if (event.key === '+' || event.key === '=') {
            event.preventDefault();
            zoomIn.click();
        } else if (event.key === '-') {
            event.preventDefault();
            zoomOut.click();
        }
    });

    window.addEventListener('beforeunload', cancelCurrentRender);
    window.addEventListener('resize', () => {
        if (state.pdf) renderCurrent();
    });

    load();
}
