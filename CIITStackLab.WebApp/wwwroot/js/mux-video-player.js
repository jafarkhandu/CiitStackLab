(() => {
    const HLS_URL_BASE = 'https://stream.mux.com/';
    const HLS_SCRIPT_URL = 'https://cdn.jsdelivr.net/npm/hls.js@1.7.3/dist/hls.min.js';
    const MUX_PATH_PREFIX = '/uploads/videos/';

    let hlsLoaderPromise;
    let activeModal;

    function loadHls() {
        if (window.Hls) return Promise.resolve(window.Hls);
        if (hlsLoaderPromise) return hlsLoaderPromise;
        hlsLoaderPromise = new Promise((resolve, reject) => {
            const existing = document.querySelector('script[data-ciit-hls]');
            if (existing) {
                existing.addEventListener('load', () => resolve(window.Hls));
                existing.addEventListener('error', reject);
                return;
            }
            const script = document.createElement('script');
            script.src = HLS_SCRIPT_URL;
            script.async = true;
            script.dataset.ciitHls = 'true';
            script.onload = () => window.Hls ? resolve(window.Hls) : reject(new Error('HLS.js did not load.'));
            script.onerror = () => reject(new Error('HLS.js could not be loaded.'));
            document.head.appendChild(script);
        });
        return hlsLoaderPromise;
    }

    function isLikelyPlaybackId(value) {
        return /^[A-Za-z0-9_-]{20,100}$/.test(value || '');
    }

    function extractPlaybackId(value) {
        if (!value) return null;
        try {
            const url = new URL(value, window.location.origin);
            const path = decodeURIComponent(url.pathname);
            if (path.toLowerCase().startsWith(MUX_PATH_PREFIX)) {
                const id = path.slice(MUX_PATH_PREFIX.length).replace(/\/$/, '');
                return isLikelyPlaybackId(id) ? id : null;
            }
            if (url.hostname === 'player.mux.com') {
                const id = path.split('/').filter(Boolean).pop();
                return isLikelyPlaybackId(id) ? id : null;
            }
            if (url.hostname === 'stream.mux.com') {
                const id = path.split('/').filter(Boolean)[0]?.replace(/\.m3u8$/i, '');
                return isLikelyPlaybackId(id) ? id : null;
            }
        } catch {
            // Fall through to bare playback ID handling.
        }
        const bareValue = value.trim().replace(/^\/+|\/+$/g, '');
        return !bareValue.includes('/') && !bareValue.includes('.') && isLikelyPlaybackId(bareValue)
            ? bareValue
            : null;
    }

    function formatTime(seconds) {
        if (!Number.isFinite(seconds) || seconds < 0) return '00:00';
        const total = Math.floor(seconds);
        const hours = Math.floor(total / 3600);
        const minutes = Math.floor((total % 3600) / 60);
        const secs = total % 60;
        return hours > 0
            ? `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(secs).padStart(2, '0')}`
            : `${String(minutes).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
    }

    function createPlayer(playbackId, title = 'Video lesson') {
        const wrapper = document.createElement('div');
        wrapper.className = 'ciit-mux-player is-paused';
        wrapper.tabIndex = -1;
        wrapper.setAttribute('role', 'region');
        wrapper.setAttribute('aria-label', title);
        wrapper.innerHTML = `
            <video playsinline preload="metadata"></video>
            <div class="ciit-mux-player-loading"><div class="ciit-mux-player-loading-inner"><div class="ciit-mux-player-spinner" aria-hidden="true"></div><div>Loading video…</div></div></div>
            <div class="ciit-mux-player-error" hidden><div class="ciit-mux-player-error-inner"><strong>Video could not be loaded</strong><span>Please refresh the page and try again.</span></div></div>
            <div class="ciit-mux-controls">
                <input class="ciit-mux-progress" type="range" min="0" max="100" value="0" step="0.1" aria-label="Video progress" />
                <div class="ciit-mux-control-row">
                    <button type="button" class="ciit-mux-play" aria-label="Play video"><i class="bi bi-play-fill"></i></button>
                    <button type="button" class="ciit-mux-mute" aria-label="Mute video"><i class="bi bi-volume-up-fill"></i></button>
                    <input class="ciit-mux-volume" type="range" min="0" max="1" value="1" step="0.05" aria-label="Volume" />
                    <span class="ciit-mux-time">00:00 / 00:00</span>
                    <span class="ciit-mux-spacer"></span>
                    <select class="ciit-mux-speed" aria-label="Playback speed"><option value="0.75">0.75×</option><option value="1" selected>1×</option><option value="1.25">1.25×</option><option value="1.5">1.5×</option><option value="2">2×</option></select>
                    <button type="button" class="ciit-mux-fullscreen" aria-label="Enter fullscreen"><i class="bi bi-fullscreen"></i></button>
                </div>
            </div>`;

        const video = wrapper.querySelector('video');
        const loading = wrapper.querySelector('.ciit-mux-player-loading');
        const error = wrapper.querySelector('.ciit-mux-player-error');
        const playButton = wrapper.querySelector('.ciit-mux-play');
        const muteButton = wrapper.querySelector('.ciit-mux-mute');
        const progress = wrapper.querySelector('.ciit-mux-progress');
        const volume = wrapper.querySelector('.ciit-mux-volume');
        const time = wrapper.querySelector('.ciit-mux-time');
        const speed = wrapper.querySelector('.ciit-mux-speed');
        const fullscreenButton = wrapper.querySelector('.ciit-mux-fullscreen');
        let hls;
        let hideControlsTimer;

        function setLoading(isLoading) { loading.hidden = !isLoading; }
        function setError(message) {
            error.querySelector('span').textContent = message;
            error.hidden = false;
            setLoading(false);
        }
        function updatePlayButton() {
            const playing = !video.paused && !video.ended;
            wrapper.classList.toggle('is-paused', !playing);
            playButton.innerHTML = playing ? '<i class="bi bi-pause-fill"></i>' : '<i class="bi bi-play-fill"></i>';
            playButton.setAttribute('aria-label', playing ? 'Pause video' : 'Play video');
        }
        function updateVolumeButton() {
            if (video.muted || video.volume === 0) {
                muteButton.innerHTML = '<i class="bi bi-volume-mute-fill"></i>';
                muteButton.setAttribute('aria-label', 'Unmute video');
            } else if (video.volume < 0.5) {
                muteButton.innerHTML = '<i class="bi bi-volume-down-fill"></i>';
                muteButton.setAttribute('aria-label', 'Mute video');
            } else {
                muteButton.innerHTML = '<i class="bi bi-volume-up-fill"></i>';
                muteButton.setAttribute('aria-label', 'Mute video');
            }
        }
        function updateProgress() {
            const duration = Number.isFinite(video.duration) ? video.duration : 0;
            const current = Number.isFinite(video.currentTime) ? video.currentTime : 0;
            progress.value = duration > 0 ? String((current / duration) * 100) : '0';
            time.textContent = `${formatTime(current)} / ${formatTime(duration)}`;
        }
        function showControls() {
            wrapper.classList.remove('is-idle');
            clearTimeout(hideControlsTimer);
            if (!video.paused) hideControlsTimer = setTimeout(() => wrapper.classList.add('is-idle'), 2200);
        }

        playButton.addEventListener('click', () => {
            if (video.paused || video.ended) video.play().catch(() => setError('Playback was blocked. Press play again to start the video.'));
            else video.pause();
        });
        muteButton.addEventListener('click', () => {
            video.muted = !video.muted;
            if (!video.muted && video.volume === 0) { video.volume = 0.8; volume.value = '0.8'; }
            updateVolumeButton();
        });
        volume.addEventListener('input', () => { video.volume = Number(volume.value); video.muted = video.volume === 0; updateVolumeButton(); });
        progress.addEventListener('input', () => { if (Number.isFinite(video.duration) && video.duration > 0) video.currentTime = video.duration * (Number(progress.value) / 100); });
        speed.addEventListener('change', () => { video.playbackRate = Number(speed.value); });
        fullscreenButton.addEventListener('click', async () => {
            try {
                if (document.fullscreenElement) await document.exitFullscreen();
                else if (wrapper.requestFullscreen) await wrapper.requestFullscreen();
            } catch { /* Browser denied or does not support fullscreen. */ }
        });

        video.addEventListener('play', () => { updatePlayButton(); showControls(); });
        video.addEventListener('pause', updatePlayButton);
        video.addEventListener('ended', updatePlayButton);
        video.addEventListener('loadedmetadata', () => { setLoading(false); updateProgress(); });
        video.addEventListener('timeupdate', updateProgress);
        video.addEventListener('progress', updateProgress);
        video.addEventListener('waiting', () => setLoading(true));
        video.addEventListener('playing', () => { setLoading(false); showControls(); });
        video.addEventListener('canplay', () => setLoading(false));
        video.addEventListener('error', () => setError('The video stream is unavailable. Please try again later.'));
        wrapper.addEventListener('mousemove', showControls);
        wrapper.addEventListener('touchstart', showControls, { passive: true });

        loadHls().then(Hls => {
            const source = `${HLS_URL_BASE}${encodeURIComponent(playbackId)}.m3u8`;
            if (video.canPlayType('application/vnd.apple.mpegurl')) {
                video.src = source;
                return;
            }
            if (!Hls || !Hls.isSupported()) throw new Error('This browser does not support HLS playback.');
            hls = new Hls({ enableWorker: true, lowLatencyMode: false, backBufferLength: 90 });
            hls.loadSource(source);
            hls.attachMedia(video);
            hls.on(Hls.Events.MANIFEST_PARSED, () => setLoading(false));
            hls.on(Hls.Events.ERROR, (_event, data) => {
                if (data?.fatal) setError('The video stream could not be loaded. Please refresh and try again.');
            });
        }).catch(() => setError('The video player could not initialize. Please refresh and try again.'));

        wrapper.destroyPlayer = () => {
            clearTimeout(hideControlsTimer);
            if (hls) hls.destroy();
            video.pause();
            video.removeAttribute('src');
            video.load();
        };
        return wrapper;
    }

    function ensureModal() {
        if (activeModal) return activeModal;
        const modal = document.createElement('div');
        modal.className = 'ciit-mux-modal';
        modal.hidden = true;
        modal.innerHTML = '<div class="ciit-mux-modal-card" role="dialog" aria-modal="true" aria-label="Video player"><button type="button" class="ciit-mux-modal-close" aria-label="Close video player"><i class="bi bi-x-lg"></i></button></div>';
        const close = () => {
            const player = modal.querySelector('.ciit-mux-player');
            player?.destroyPlayer?.();
            player?.remove();
            modal.hidden = true;
            document.body.classList.remove('ciit-mux-modal-open');
            activeModal = null;
        };
        modal.querySelector('.ciit-mux-modal-close').addEventListener('click', close);
        modal.addEventListener('click', event => { if (event.target === modal) close(); });
        document.addEventListener('keydown', event => { if (event.key === 'Escape' && activeModal === modal) close(); });
        document.body.appendChild(modal);
        activeModal = modal;
        return modal;
    }

    function openModal(playbackId, title) {
        const modal = ensureModal();
        const card = modal.querySelector('.ciit-mux-modal-card');
        const close = card.querySelector('.ciit-mux-modal-close');
        const existing = card.querySelector('.ciit-mux-player');
        existing?.destroyPlayer?.();
        existing?.remove();
        card.appendChild(createPlayer(playbackId, title));
        modal.hidden = false;
        document.body.classList.add('ciit-mux-modal-open');
        close.focus();
    }

    function isMuxVideoLink(link) { return Boolean(link?.href && extractPlaybackId(link.href)); }

    function replaceInlineMuxIframe() {
        document.querySelectorAll('.ciit-learning-video-frame iframe').forEach(iframe => {
            const playbackId = extractPlaybackId(iframe.getAttribute('src'));
            if (!playbackId) return;
            const title = iframe.getAttribute('title') || 'Video lesson';
            const frame = iframe.parentElement;
            if (!frame || frame.dataset.ciitMuxReady === 'true') return;
            frame.dataset.ciitMuxReady = 'true';
            const player = createPlayer(playbackId, title);
            frame.classList.remove('ratio', 'ratio-16x9');
            frame.replaceChildren(player);
        });
    }

    function wireMuxLinks() {
        document.addEventListener('click', event => {
            const link = event.target.closest('a');
            if (!link || !isMuxVideoLink(link) || link.closest('.ciit-mux-modal')) return;
            const playbackId = extractPlaybackId(link.href);
            if (!playbackId) return;
            const title = link.getAttribute('aria-label') || link.closest('.ciit-notes-reader-media-row')?.querySelector('.ciit-notes-reader-media-copy strong')?.textContent?.trim() || 'Video lesson';
            event.preventDefault();
            openModal(playbackId, title);
        });
    }

    function initialize() {
        if (!document.body.classList.contains('ciit-course-environment-body')) return;
        const hasPotentialMuxVideo = document.querySelector('.ciit-learning-video-frame iframe, a[href*="/uploads/videos/"], .ciit-notes-reader-media a');
        if (!hasPotentialMuxVideo) return;
        const stylesheet = document.createElement('link');
        stylesheet.rel = 'stylesheet';
        stylesheet.href = '/css/mux-video-player.css?v=1';
        document.head.appendChild(stylesheet);
        replaceInlineMuxIframe();
        wireMuxLinks();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true });
    else initialize();
})();