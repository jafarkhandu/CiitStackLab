const PPT_CORE_URL = 'https://esm.sh/@web-ppt/core@0.4.5?bundle';
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
        type: null,
        presentation: null,
        pdf: null,
        page: 0,
        zoom: 1,
        fit: true,
        rendered: new Map(),
        objectUrls: [],
        pdfCanvas: null,
        pdfContext: null
    };

    const setLoading = (visible, message = 'Opening slides') => {
        if (!loading) return;
        loading.hidden = !visible;
        const strong = loading.querySelector('strong');
        if (strong) strong.textContent = message;
    };

    const setPageInfo = () => {
        const count = state.type === 'pdf'
            ? state.pdf?.numPages || 0
            : state.presentation?.slides?.length || 0;
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

    const revokeUrls = () => {
        state.objectUrls.forEach(url => URL.revokeObjectURL(url));
        state.objectUrls = [];
    };

    const renderError = (message) => {
        setLoading(false);
        stage.innerHTML = `
            <div class="ciit-slide-reader-error" role="alert">
                <i class="bi bi-exclamation-triangle"></i>
                <strong>Unable to open these slides</strong>
                <span>${message}</span>
            </div>`;
        thumbnails.innerHTML = '';
    };

    const renderPdfPage = async () => {
        if (!state.pdf) return;

        const page = await state.pdf.getPage(state.page + 1);
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
        }

        canvas.width = Math.ceil(viewport.width);
        canvas.height = Math.ceil(viewport.height);
        canvas.style.width = `${Math.ceil(viewport.width)}px`;
        canvas.style.height = `${Math.ceil(viewport.height)}px`;

        await page.render({
            canvasContext: state.pdfContext,
            viewport
        }).promise;

        setLoading(false);
        setZoomLabel();
    };

    const getPptSlideUrl = async index => {
        if (state.rendered.has(index)) return state.rendered.get(index);

        const { slideToSvgFile } = state.pptModule;
        const blob = await slideToSvgFile(state.presentation, state.presentation.slides[index]);
        const url = URL.createObjectURL(blob);
        state.objectUrls.push(url);
        state.rendered.set(index, url);
        return url;
    };

    const renderPptSlide = async () => {
        const url = await getPptSlideUrl(state.page);
        stage.innerHTML = '';

        const image = document.createElement('img');
        image.className = 'ciit-slide-reader-ppt-image';
        image.alt = `${title} — slide ${state.page + 1}`;
        image.src = url;
        image.style.transform = `scale(${state.zoom})`;
        stage.appendChild(image);

        setLoading(false);
        setZoomLabel();
    };

    const selectThumbnail = () => {
        thumbnails.querySelectorAll('[data-slide-index]').forEach(item => {
            item.classList.toggle('is-active', Number(item.dataset.slideIndex) === state.page);
        });

        const active = thumbnails.querySelector(`[data-slide-index="${state.page}"]`);
        active?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    };

    const renderCurrent = async () => {
        try {
            setPageInfo();
            selectThumbnail();

            if (state.type === 'pdf') {
                await renderPdfPage();
            } else {
                await renderPptSlide();
            }
        } catch (error) {
            console.error(error);
            renderError('This file could not be rendered in the browser. Try another presentation file.');
        }
    };

    const buildPptThumbnails = async () => {
        thumbnails.innerHTML = '';
        const slides = state.presentation.slides || [];

        for (let index = 0; index < slides.length; index++) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'ciit-slide-thumbnail';
            button.dataset.slideIndex = String(index);
            button.setAttribute('aria-label', `Open slide ${index + 1}`);

            const image = document.createElement('img');
            image.alt = '';
            image.loading = index < 6 ? 'eager' : 'lazy';

            const number = document.createElement('span');
            number.textContent = String(index + 1);

            button.append(image, number);
            button.addEventListener('click', () => {
                state.page = index;
                renderCurrent();
            });
            thumbnails.appendChild(button);

            try {
                image.src = await getPptSlideUrl(index);
            } catch (error) {
                console.warn(`Unable to render thumbnail ${index + 1}`, error);
            }
        }

        selectThumbnail();
    };

    const buildPdfThumbnails = async () => {
        thumbnails.innerHTML = '';

        for (let index = 0; index < state.pdf.numPages; index++) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'ciit-slide-thumbnail';
            button.dataset.slideIndex = String(index);
            button.setAttribute('aria-label', `Open slide ${index + 1}`);

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
        state.pdf = await pdfjs.getDocument({ data: bytes }).promise;
        state.type = 'pdf';
        setPageInfo();
        await buildPdfThumbnails();
        await renderCurrent();
    };

    const loadPowerPoint = async response => {
        const pptModule = await import(PPT_CORE_URL);
        const blob = await response.blob();
        state.pptModule = pptModule;
        state.presentation = await pptModule.parse(blob);
        state.type = 'ppt';
        setPageInfo();
        await buildPptThumbnails();
        await renderCurrent();
    };

    const load = async () => {
        if (!slideUrl) {
            renderError('No slide file is attached to this resource.');
            return;
        }

        setLoading(true, 'Opening slides');

        try {
            const response = await fetch(slideUrl, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok) throw new Error(`Slide file returned HTTP ${response.status}.`);

            const contentType = (response.headers.get('content-type') || '').toLowerCase();
            const extension = slideUrl.split('?')[0].split('.').pop()?.toLowerCase();

            if (extension === 'pdf' || contentType.includes('application/pdf')) {
                await loadPdf(response);
            } else if (extension === 'ppt' || extension === 'pptx') {
                await loadPowerPoint(response);
            } else {
                throw new Error('Unsupported slide format.');
            }
        } catch (error) {
            console.error(error);
            renderError(error instanceof Error && error.message.includes('HTTP')
                ? 'The slide file could not be loaded. Please try again.'
                : 'This file could not be rendered in the browser.');
        }
    };

    const changeZoom = delta => {
        state.fit = false;
        state.zoom = Math.max(0.5, Math.min(2.5, Number((state.zoom + delta).toFixed(2))));
        renderCurrent();
    };

    previous.addEventListener('click', () => {
        if (state.page > 0) {
            state.page--;
            renderCurrent();
        }
    });

    next.addEventListener('click', () => {
        const count = state.type === 'pdf'
            ? state.pdf?.numPages || 0
            : state.presentation?.slides?.length || 0;
        if (state.page < count - 1) {
            state.page++;
            renderCurrent();
        }
    });

    zoomOut.addEventListener('click', () => changeZoom(-0.1));
    zoomIn.addEventListener('click', () => changeZoom(0.1));
    zoomLabel.addEventListener('click', () => {
        state.zoom = 1;
        state.fit = false;
        renderCurrent();
    });

    fit.addEventListener('click', () => {
        state.zoom = 1;
        state.fit = true;
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

    window.addEventListener('beforeunload', revokeUrls);
    window.addEventListener('resize', () => {
        if (state.type === 'pdf' && state.pdf) renderCurrent();
    });

    load();
}
