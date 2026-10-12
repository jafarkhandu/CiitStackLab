(() => {
    "use strict";
    const form = document.getElementById("interviewQuestionImportUploadForm");
    if (!form) return;

    const content = document.getElementById("ImportInterviewTopicId");
    const file = document.getElementById("ImportInterviewFile");
    const button = form.querySelector('button[type="submit"]');
    const preview = document.getElementById("interviewQuestionImportPreviewHost");
    const status = document.getElementById("interviewImportLiveStatus");
    const title = document.getElementById("interviewImportLiveTitle");
    const state = document.getElementById("interviewImportLiveState");
    const detail = document.getElementById("interviewImportLiveDetail");
    const log = document.getElementById("interviewImportProgressLog");
    if (!content || !file || !button || !preview || !status || !title || !state || !detail || !log) return;

    const originalMarkup = button.innerHTML;
    let running = false;

    function setStatus(heading, message, mode) {
        status.classList.remove("d-none", "is-complete", "is-error");
        if (mode === "complete") {
            status.classList.add("is-complete");
            state.textContent = "COMPLETE";
        } else if (mode === "error") {
            status.classList.add("is-error");
            state.textContent = "ACTION REQUIRED";
        } else {
            state.textContent = "ANALYZING";
        }
        title.textContent = heading;
        detail.textContent = message;
    }

    function appendProgress(message) {
        if (Array.from(log.children).some(item => item.textContent === message)) return;
        const item = document.createElement("li");
        item.textContent = message;
        log.appendChild(item);
    }

    async function loadPreview(token) {
        const url = new URL(preview.dataset.previewUrl, document.baseURI);
        url.searchParams.set("previewToken", token);
        const response = await fetch(url.toString(), {
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) {
            throw new Error(response.status === 404
                ? "The preview expired. Upload the notes again."
                : "The analysis result could not be loaded.");
        }
        preview.innerHTML = await response.text();
    }

    function processLine(line, setResult) {
        if (!line.trim()) return;
        let event;
        try { event = JSON.parse(line); }
        catch { throw new Error("The server returned an unreadable analysis update."); }

        if (event.type === "progress") {
            setStatus("Analyzing interview notes", event.message, "loading");
            appendProgress(event.message);
        } else if (event.type === "result") {
            setResult(event);
        } else if (event.type === "error") {
            throw new Error(event.message || "The notes could not be analyzed.");
        }
    }

    async function analyze() {
        if (running || !file.files || !file.files[0]) return;
        if (!content.value || Number(content.value) <= 0) {
            setStatus("Choose a chapter first", "Select the chapter these questions should be linked to.", "error");
            return;
        }

        const formData = new FormData(form);
        running = true;
        content.disabled = true;
        file.disabled = true;
        button.disabled = true;
        form.setAttribute("aria-busy", "true");
        button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Analyzing notes...';
        preview.replaceChildren();
        log.replaceChildren();
        setStatus("Uploading and starting analysis", "The file is being read locally. Validation steps will appear as they run.", "loading");
        appendProgress("Preparing the selected content and uploaded file...");

        try {
            const response = await fetch(form.dataset.analyzeUrl, {
                method: "POST",
                body: formData,
                headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/x-ndjson" },
                credentials: "same-origin"
            });
            if (!response.ok) {
                throw new Error(response.status === 413
                    ? "The file is too large. Maximum upload size is 5 MB."
                    : "The analysis request failed. Refresh the page and sign in again if your session expired.");
            }
            if (!response.headers.get("content-type")?.includes("application/x-ndjson") || !response.body) {
                throw new Error("The server did not start the live analysis stream.");
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = "";
            let result = null;
            while (true) {
                const chunk = await reader.read();
                buffer += decoder.decode(chunk.value || new Uint8Array(), { stream: !chunk.done });
                let newline = buffer.indexOf("\n");
                while (newline >= 0) {
                    const line = buffer.slice(0, newline);
                    buffer = buffer.slice(newline + 1);
                    processLine(line, value => { result = value; });
                    newline = buffer.indexOf("\n");
                }
                if (chunk.done) break;
            }
            if (buffer.trim()) processLine(buffer, value => { result = value; });
            if (!result || !result.previewToken) throw new Error("The server ended analysis without returning a preview.");

            await loadPreview(result.previewToken);
            if (result.canImport) {
                setStatus("Analysis complete", "20 valid question-and-answer pairs are ready for review. Nothing has been saved yet.", "complete");
            } else {
                setStatus("Analysis finished with issues",
                    result.validCount + " valid and " + result.invalidCount + " invalid pair(s), with " +
                    result.fileErrorCount + " file-level issue(s). Review the details; invalid batches cannot be saved.", "error");
            }
            preview.scrollIntoView({ behavior: "smooth", block: "start" });
        } catch (error) {
            setStatus("Analysis could not be completed",
                error instanceof Error ? error.message : "The notes could not be analyzed. Please try again.", "error");
        } finally {
            running = false;
            content.disabled = false;
            file.disabled = false;
            button.disabled = false;
            button.innerHTML = originalMarkup;
            form.removeAttribute("aria-busy");
        }
    }

    file.addEventListener("change", () => { if (file.files && file.files.length) void analyze(); });
    content.addEventListener("change", () => { if (file.files && file.files.length) void analyze(); });
    form.addEventListener("submit", event => { event.preventDefault(); void analyze(); });
})();
