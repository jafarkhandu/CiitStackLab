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
        const submitButton = form.querySelector('button[type="submit"]');
        const uploadIdInput = form.querySelector('.js-mux-upload-id');
        const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');

        if (!videoInput || !submitButton || !uploadIdInput || !tokenInput) return;

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
                        <span class="ciit-upload-description">Your browser is uploading the video directly to secure Mux storage. You can keep this page open.</span>
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

            if (progress === null) {
                progressBar?.classList.add('is-indeterminate');
                progressBar?.style.removeProperty('width');
            } else {
                progressBar?.classList.remove('is-indeterminate');
                if (progressBar) progressBar.style.width = `${progress}%`;
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

        const requestDirectUpload = async () => {
            const response = await fetch('/Admin/Content/CreateVideoUpload', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': tokenInput.value,
                    'Accept': 'application/json'
                }
            });

            let payload = null;
            try {
                payload = await response.json();
            } catch {
                // Keep the generic error below if the server did not return JSON.
            }

            if (!response.ok || !payload?.uploadId || !payload?.uploadUrl) {
                throw new Error(payload?.message || 'Unable to prepare the Mux upload.');
            }

            return payload;
        };

        const uploadToMux = (uploadUrl, file, overlay) => new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open('PUT', uploadUrl, true);
            xhr.setRequestHeader('Content-Type', file.type || 'application/octet-stream');

            xhr.upload.addEventListener('progress', event => {
                if (!event.lengthComputable) {
                    updateOverlay(
                        overlay,
                        'Uploading video',
                        'Your browser is uploading the video directly to secure Mux storage.',
                        'Uploading video...',
                        null);
                    return;
                }

                const percent = Math.min(100, Math.round((event.loaded / event.total) * 100));
                updateOverlay(
                    overlay,
                    'Uploading video',
                    'Your browser is uploading the video directly to secure Mux storage.',
                    `Uploading video... ${percent}%`,
                    percent);
            });

            xhr.addEventListener('load', () => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    resolve();
                    return;
                }

                reject(new Error(`Mux upload failed with HTTP ${xhr.status}.`));
            });

            xhr.addEventListener('error', () => reject(new Error('The connection to Mux was interrupted.')));
            xhr.addEventListener('abort', () => reject(new Error('The video upload was cancelled.')));
            xhr.send(file);
        });

        const waitForMuxReady = async (uploadId, overlay) => {
            const deadline = Date.now() + (15 * 60 * 1000);

            while (Date.now() < deadline) {
                const response = await fetch(
                    `/Admin/Mux/VideoUploadStatus?uploadId=${encodeURIComponent(uploadId)}`,
                    { headers: { 'Accept': 'application/json' } });

                let payload = null;
                try {
                    payload = await response.json();
                } catch {
                    // Keep the generic error below if the server did not return JSON.
                }

                if (!response.ok) {
                    throw new Error(payload?.message || 'Unable to check the Mux video status.');
                }

                const progress = Number.isFinite(Number(payload?.progress))
                    ? Math.max(0, Math.min(100, Number(payload.progress)))
                    : 0;

                if (payload?.stage === 'ready' && payload?.playbackId) {
                    updateOverlay(
                        overlay,
                        'Video ready',
                        'Mux has finished processing the video. Saving the content now.',
                        'Video processing complete — 100%',
                        100,
                        true);
                    return payload;
                }

                if (payload?.stage === 'processing') {
                    const hasProgress = progress > 0;
                    updateOverlay(
                        overlay,
                        'Processing video',
                        payload.message || 'Mux is processing the video and preparing playback.',
                        hasProgress ? `Processing video... ${progress}%` : 'Processing video...',
                        hasProgress ? progress : null,
                        true);
                } else {
                    updateOverlay(
                        overlay,
                        'Finalizing upload',
                        payload.message || 'Mux is finalizing the uploaded video.',
                        'Finalizing upload...',
                        null,
                        true);
                }

                await new Promise(resolve => setTimeout(resolve, 2500));
            }

            throw new Error('Mux is taking longer than expected to process this video. Please try again shortly.');
        };

        form.addEventListener('submit', async event => {
            if (!videoInput.files || videoInput.files.length === 0) return;
            if (uploadIdInput.value) return;

            if (!form.checkValidity()) return;

            event.preventDefault();

            const file = videoInput.files[0];
            const overlay = showUploadOverlay();

            submitButton.disabled = true;
            submitButton.classList.add('is-uploading');
            submitButton.setAttribute('aria-busy', 'true');
            submitButton.innerHTML = '<span class="ciit-upload-button-spinner" aria-hidden="true"></span> Uploading...';

            try {
                const upload = await requestDirectUpload();

                updateOverlay(
                    overlay,
                    'Uploading video',
                    'Your browser is uploading the video directly to secure Mux storage.',
                    'Uploading video... 0%',
                    0);

                await uploadToMux(upload.uploadUrl, file, overlay);

                uploadIdInput.value = upload.uploadId;
                videoInput.disabled = true;

                updateOverlay(
                    overlay,
                    'Processing video',
                    'The upload is complete. Checking Mux processing status now.',
                    'Checking video status...',
                    null,
                    true);

                submitButton.innerHTML = '<span class="ciit-upload-button-spinner" aria-hidden="true"></span> Processing...';
                await waitForMuxReady(upload.uploadId, overlay);

                HTMLFormElement.prototype.submit.call(form);
            } catch (error) {
                showUploadError(overlay, error instanceof Error ? error.message : 'Unable to upload the video.');
                submitButton.disabled = false;
                submitButton.classList.remove('is-uploading');
                submitButton.removeAttribute('aria-busy');
                submitButton.innerHTML = '<i class="bi bi-check2-circle"></i> Try Again';
                document.body.classList.remove('ciit-uploading');
            }
        });
    });
});
