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

    document.querySelectorAll('.ciit-content-form-card').forEach(form => {
        const videoInput = form.querySelector('input[name="videoFile"]');
        const titleInput = form.querySelector('input[name="Title"]');
        const contentIdInput = form.querySelector('input[name="Id"]');
        const submitButton = form.querySelector('button[type="submit"]');
        const videoUrlInput = form.querySelector('.js-vimeo-video-url');
        const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');

        if (!videoInput || !titleInput || !submitButton || !videoUrlInput || !tokenInput) return;

        const showUploadOverlay = () => {
            const existingOverlay = document.querySelector('.ciit-upload-overlay');
            if (existingOverlay) return existingOverlay;

            const overlay = document.createElement('div');
            overlay.className = 'ciit-upload-overlay';
            overlay.setAttribute('role', 'dialog');
            overlay.setAttribute('aria-modal', 'true');
            overlay.setAttribute('aria-live', 'polite');
            overlay.innerHTML = `
                <div class="ciit-upload-dialog">
                    <div class="ciit-upload-spinner" aria-hidden="true"></div>
                    <div class="ciit-upload-copy">
                        <strong class="ciit-upload-title">Uploading video</strong>
                        <span class="ciit-upload-description">Your browser is uploading the video directly to Vimeo. Keep this page open until the upload finishes.</span>
                    </div>
                    <div class="ciit-upload-progress" aria-hidden="true">
                        <div class="ciit-upload-progress-bar"></div>
                    </div>
                    <span class="ciit-upload-status">Preparing secure upload...</span>
                </div>
            `;
            document.body.appendChild(overlay);
            document.body.classList.add('ciit-uploading');
            return overlay;
        };

        const updateOverlay = (overlay, title, description, status, progress = null, processing = false) => {
            const titleElement = overlay.querySelector('.ciit-upload-title');
            const descriptionElement = overlay.querySelector('.ciit-upload-description');
            const statusElement = overlay.querySelector('.ciit-upload-status');
            const progressBar = overlay.querySelector('.ciit-upload-progress-bar');

            if (titleElement) titleElement.textContent = title;
            if (descriptionElement) descriptionElement.textContent = description;
            if (statusElement) statusElement.textContent = status;

            if (progressBar) {
                if (progress === null) {
                    progressBar.classList.add('is-indeterminate');
                    progressBar.style.removeProperty('width');
                } else {
                    progressBar.classList.remove('is-indeterminate');
                    progressBar.style.width = `${progress}%`;
                }
            }

            overlay.classList.toggle('is-processing', processing);
        };

        const showUploadError = (overlay, message) => {
            const titleElement = overlay.querySelector('.ciit-upload-title');
            const descriptionElement = overlay.querySelector('.ciit-upload-description');
            const statusElement = overlay.querySelector('.ciit-upload-status');
            const progressBar = overlay.querySelector('.ciit-upload-progress-bar');

            if (titleElement) titleElement.textContent = 'Video upload failed';
            if (descriptionElement) descriptionElement.textContent = 'The video was not attached to this content record.';
            if (statusElement) statusElement.textContent = message;
            if (progressBar) progressBar.classList.remove('is-indeterminate');
            overlay.classList.add('is-error');
        };

        const requestDirectUpload = async file => {
            const formData = new URLSearchParams();
            formData.set('fileName', file.name);
            formData.set('fileSize', String(file.size));
            formData.set('title', titleInput.value.trim());
            formData.set('contentId', contentIdInput?.value || '0');

            const response = await fetch('/Admin/Content/CreateVideoUpload', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': tokenInput.value,
                    'Accept': 'application/json',
                    'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8'
                },
                body: formData.toString()
            });

            let payload = null;
            try {
                payload = await response.json();
            } catch {
                // Keep the generic error below if the server did not return JSON.
            }

            if (!response.ok || !payload?.videoId || !payload?.uploadUrl || !payload?.playerUrl) {
                throw new Error(payload?.message || 'Unable to prepare the Vimeo upload.');
            }

            return payload;
        };

        const getUploadOffset = async uploadUrl => {
            const response = await fetch(uploadUrl, {
                method: 'HEAD',
                cache: 'no-store',
                headers: {
                    'Tus-Resumable': '1.0.0',
                    'Accept': 'application/vnd.vimeo.*+json;version=3.4'
                }
            });

            if (!response.ok) {
                throw new Error(`Unable to resume the Vimeo upload (HTTP ${response.status}).`);
            }

            const offset = Number(response.headers.get('Upload-Offset'));
            if (!Number.isFinite(offset) || offset < 0) {
                throw new Error('Vimeo returned an invalid upload offset.');
            }

            return offset;
        };

        const uploadChunk = (uploadUrl, file, offset, overlay) => new Promise((resolve, reject) => {
            const end = Math.min(offset + (128 * 1024 * 1024), file.size);
            const chunk = file.slice(offset, end);
            const xhr = new XMLHttpRequest();

            xhr.open('PATCH', uploadUrl, true);
            xhr.setRequestHeader('Tus-Resumable', '1.0.0');
            xhr.setRequestHeader('Upload-Offset', String(offset));
            xhr.setRequestHeader('Content-Type', 'application/offset+octet-stream');

            xhr.upload.addEventListener('progress', event => {
                if (!event.lengthComputable) {
                    updateOverlay(
                        overlay,
                        'Uploading video',
                        'Your browser is uploading the video directly to Vimeo.',
                        'Uploading video...',
                        null);
                    return;
                }

                const uploadedBytes = offset + event.loaded;
                const percent = Math.min(100, Math.round((uploadedBytes / file.size) * 85));

                updateOverlay(
                    overlay,
                    'Uploading video',
                    'Your browser is uploading the video directly to Vimeo.',
                    `Uploading video... ${percent}%`,
                    percent);
            });

            xhr.addEventListener('load', () => {
                if (xhr.status === 409) {
                    reject(new Error('Vimeo upload offset conflict.'));
                    return;
                }

                if (xhr.status < 200 || xhr.status >= 300) {
                    reject(new Error(`Vimeo upload failed with HTTP ${xhr.status}.`));
                    return;
                }

                const nextOffset = Number(xhr.getResponseHeader('Upload-Offset'));
                if (!Number.isFinite(nextOffset) || nextOffset <= offset) {
                    reject(new Error('Vimeo returned an invalid upload offset.'));
                    return;
                }

                resolve(nextOffset);
            });

            xhr.addEventListener('error', () => reject(new Error('The connection to Vimeo was interrupted.')));
            xhr.addEventListener('abort', () => reject(new Error('The video upload was cancelled.')));
            xhr.send(chunk);
        });

        const uploadToVimeo = async (uploadUrl, file, overlay) => {
            let offset = 0;
            let retryCount = 0;

            while (offset < file.size) {
                try {
                    offset = await uploadChunk(uploadUrl, file, offset, overlay);
                    retryCount = 0;
                } catch (error) {
                    if (retryCount >= 2) throw error;

                    retryCount++;
                    updateOverlay(
                        overlay,
                        'Resuming upload',
                        'The connection was interrupted. Checking Vimeo for the latest upload position.',
                        `Resuming upload... attempt ${retryCount} of 2`,
                        null,
                        true);

                    offset = await getUploadOffset(uploadUrl);
                }
            }

            updateOverlay(
                overlay,
                'Upload complete',
                'Vimeo received the complete video file. Processing will begin shortly.',
                'Upload complete — 85%',
                85,
                true);
        };

        const waitForVimeoReady = async (videoId, overlay) => {
            const deadline = Date.now() + (15 * 60 * 1000);
            const processingStartedAt = Date.now();

            while (Date.now() < deadline) {
                const response = await fetch(
                    `/Admin/Vimeo/VideoUploadStatus?videoId=${encodeURIComponent(videoId)}`,
                    {
                        cache: 'no-store',
                        headers: {
                            'Accept': 'application/json',
                            'Cache-Control': 'no-cache'
                        }
                    });

                let payload = null;
                try {
                    payload = await response.json();
                } catch {
                    // Keep the generic error below if the server did not return JSON.
                }

                if (!response.ok) {
                    throw new Error(payload?.message || 'Unable to check the Vimeo video status.');
                }

                if (payload?.stage === 'error') {
                    throw new Error(payload.message || 'Vimeo could not process the uploaded video.');
                }

                if (payload?.stage === 'ready' && payload?.playerUrl) {
                    updateOverlay(
                        overlay,
                        'Video ready',
                        'Vimeo has finished processing the video. Saving the content now.',
                        'Video ready — 100%',
                        100,
                        true);
                    return payload;
                }

                const elapsedSeconds = Math.max(0, (Date.now() - processingStartedAt) / 1000);
                const fallbackProgress = Math.min(99, 85 + Math.floor(elapsedSeconds / 7));

                updateOverlay(
                    overlay,
                    'Processing video',
                    payload?.message || 'Vimeo is processing the video and preparing playback.',
                    `Processing video... ${fallbackProgress}%`,
                    fallbackProgress,
                    true);

                await new Promise(resolve => setTimeout(resolve, 2500));
            }

            throw new Error('Vimeo is taking longer than expected to process this video. Please try again shortly.');
        };

        form.addEventListener('submit', async event => {
            if (!videoInput.files || videoInput.files.length === 0) return;
            if (videoUrlInput.value) return;
            if (!form.checkValidity()) return;

            event.preventDefault();

            const file = videoInput.files[0];
            const overlay = showUploadOverlay();

            submitButton.disabled = true;
            submitButton.classList.add('is-uploading');
            submitButton.setAttribute('aria-busy', 'true');
            submitButton.innerHTML = '<span class="ciit-upload-button-spinner" aria-hidden="true"></span> Uploading...';

            try {
                const upload = await requestDirectUpload(file);

                updateOverlay(
                    overlay,
                    'Uploading video',
                    'Your browser is uploading the video directly to Vimeo.',
                    'Uploading video... 0%',
                    0);

                await uploadToVimeo(upload.uploadUrl, file, overlay);
                await waitForVimeoReady(upload.videoId, overlay);

                videoUrlInput.value = upload.playerUrl;
                videoInput.disabled = true;

                HTMLFormElement.prototype.submit.call(form);
            } catch (error) {
                showUploadError(
                    overlay,
                    error instanceof Error ? error.message : 'Unable to upload the video.');

                submitButton.disabled = false;
                submitButton.classList.remove('is-uploading');
                submitButton.removeAttribute('aria-busy');
                submitButton.innerHTML = '<i class="bi bi-check2-circle"></i> Try Again';
                document.body.classList.remove('ciit-uploading');
            }
        });
    });
});
