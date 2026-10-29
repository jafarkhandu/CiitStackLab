(() => {
    const initializeContentModal = () => {
        if (document.querySelector('.ciit-content-modal-shell')) return;

        const modal = document.createElement('div');
        modal.className = 'ciit-content-modal-shell';
        modal.hidden = true;
        modal.setAttribute('role', 'dialog');
        modal.setAttribute('aria-modal', 'true');
        modal.setAttribute('aria-labelledby', 'ciitContentModalTitle');
        modal.innerHTML = `
            <div class="ciit-content-modal-dialog">
                <div class="ciit-content-modal-toolbar">
                    <div class="ciit-content-modal-toolbar-copy">
                        <span class="ciit-content-modal-kicker">Admin workspace</span>
                        <span class="ciit-content-modal-title" id="ciitContentModalTitle">Content</span>
                    </div>
                    <button type="button" class="ciit-content-modal-close" aria-label="Close dialog">
                        <i class="bi bi-x-lg" aria-hidden="true"></i>
                    </button>
                </div>
                <div class="ciit-content-modal-frame-wrap">
                    <iframe class="ciit-content-modal-frame" title="Content dialog"></iframe>
                </div>
            </div>
        `;
        document.body.appendChild(modal);

        const frame = modal.querySelector('.ciit-content-modal-frame');
        const closeButton = modal.querySelector('.ciit-content-modal-close');
        const title = modal.querySelector('#ciitContentModalTitle');
        let lastFocusedElement = null;

        const isContentModalUrl = url => {
            try {
                const parsed = new URL(url, window.location.origin);
                if (parsed.origin !== window.location.origin) return false;

                const segments = parsed.pathname.split('/').filter(Boolean);
                if (segments.length < 3) return false;
                if (segments[0].toLowerCase() !== 'Admin'.toLowerCase()) return false;
                const controller = segments[1].toLowerCase();
                if (controller !== 'content' && controller !== 'notes' && controller !== 'assessments') return false;

                const action = segments[2].toLowerCase();
                if (action === 'create') return segments.length === 3;
                if (action === 'edit' || action === 'details' || action === 'editinterviewquestion' || action === 'editpracticeprogram') {
                    return segments.length === 4 && /^\d+$/.test(segments[3]);
                }

                return false;
            } catch {
                return false;
            }
        };

        const getModalTitle = url => {
            const parsed = new URL(url, window.location.origin);
            const segments = parsed.pathname.split('/').filter(Boolean);
            const controller = segments[1]?.toLowerCase();
            const action = segments[2]?.toLowerCase();
            const label = controller === 'notes' ? 'Note' : 'Content';
            if (action === 'create') return `Add ${label}`;
            if (action === 'editinterviewquestion') return 'Edit Interview Question';
            if (action === 'editpracticeprogram') return 'Edit Practice Program';
            if (action === 'edit') return `Edit ${label}`;
            if (action === 'details') return 'Content Details';
            return 'Content';
        };

        const openModal = url => {
            const parsed = new URL(url, window.location.origin);
            parsed.searchParams.set('modal', 'true');

            lastFocusedElement = document.activeElement;
            title.textContent = getModalTitle(url);
            frame.src = parsed.toString();
            modal.hidden = false;
            document.body.classList.add('ciit-content-modal-open');
            closeButton.focus();
        };

        const closeModal = () => {
            modal.hidden = true;
            frame.src = 'about:blank';
            document.body.classList.remove('ciit-content-modal-open');
            if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
                lastFocusedElement.focus();
            }
        };

        document.addEventListener('click', event => {
            const link = event.target.closest('a[href]');
            if (!link || link.target === '_blank' || link.hasAttribute('download')) return;

            const url = link.href;
            if (!isContentModalUrl(url)) return;

            event.preventDefault();
            openModal(url);
        });

        closeButton.addEventListener('click', closeModal);

        modal.addEventListener('click', event => {
            if (event.target === modal) closeModal();
        });

        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && !modal.hidden) {
                event.preventDefault();
                closeModal();
            }
        });

        window.addEventListener('message', event => {
            if (event.origin !== window.location.origin) return;

            if (event.data?.type === 'ciit-content-modal-navigate') {
                if (typeof event.data.url === 'string' && isContentModalUrl(event.data.url)) {
                    openModal(event.data.url);
                }
                return;
            }

            if (event.data?.type !== 'ciit-content-saved') return;

            closeModal();
            window.location.reload();
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeContentModal, { once: true });
    } else {
        initializeContentModal();
    }
})();
