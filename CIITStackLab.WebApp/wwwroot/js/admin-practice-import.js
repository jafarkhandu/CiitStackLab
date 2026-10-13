(() => {
    "use strict";

    const form = document.getElementById("practiceProgramImportUploadForm");
    if (!form) return;

    const contentSelect = document.getElementById("PracticeProgramImportNoteId");
    const fileInput = document.getElementById("PracticeProgramImportFile");
    const submitButton = form.querySelector('button[type="submit"]');
    const previewHost = document.getElementById("practiceProgramImportPreviewHost");
    const liveStatus = document.getElementById("practiceProgramImportLiveStatus");
    const liveTitle = document.getElementById("practiceProgramImportLiveTitle");
    const liveState = document.getElementById("practiceProgramImportLiveState");
    const liveDetail = document.getElementById("practiceProgramImportLiveDetail");
    const progressLog = document.getElementById("practiceProgramImportProgressLog");

    if (!contentSelect || !fileInput || !submitButton || !previewHost ||
        !liveStatus || !liveTitle || !liveState || !liveDetail || !progressLog) {
        return;
    }

    const originalButtonMarkup = submitButton.innerHTML;
    let isAnalyzing = false;

    function setLiveStatus(title, detail, state) {
        liveStatus.classList.remove("d-none", "is-complete", "is-error");

        if (state === "complete") {
            liveStatus.classList.add("is-complete");
            liveState.textContent = "COMPLETE";
        } else if (state === "error") {
            liveStatus.classList.add("is-error");
            liveState.textContent = "ACTION REQUIRED";
        } else {
            liveState.textContent = "ANALYZING";
        }

        liveTitle.textContent = title;
        liveDetail.textContent = detail;
    }

    function appendProgress(message) {
        if (Array.from(progressLog.children).some(item => item.textContent === message)) {
            return;
        }

        const item = document.createElement("li");
        item.textContent = message;
        progressLog.appendChild(item);
    }

    function processLine(line, onResult) {
        if (!line.trim()) return;

        let event;
        try {
            event = JSON.parse(line);
        } catch {
            throw new Error("The server returned an unreadable analysis update.");
        }

        if (event.type === "progress") {
            setLiveStatus("Analyzing programming questions", event.message, "loading");
            appendProgress(event.message);
        } else if (event.type === "result") {
            onResult(event);
        } else if (event.type === "error") {
            throw new Error(event.message || "The workbook could not be analyzed.");
        }
    }

    async function loadPreview(previewToken) {
        const url = new URL(previewHost.dataset.previewUrl, document.baseURI);
        url.searchParams.set("previewToken", previewToken);

        const response = await fetch(url.toString(), {
            method: "GET",
            headers: { "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        });

        if (!response.ok) {
            throw new Error(response.status === 404
                ? "The preview expired. Upload the workbook again."
                : "The question preview could not be loaded.");
        }

        previewHost.innerHTML = await response.text();
    }

    async function analyzeWorkbook() {
        if (isAnalyzing) return;
        const selectedFile = fileInput.files && fileInput.files[0];
        if (!selectedFile) return;

        if (!contentSelect.value || Number(contentSelect.value) <= 0) {
            setLiveStatus(
                "Choose a Notes chapter first",
                "Select the Notes chapter these programming questions should be linked to.",
                "error");
            return;
        }

        const formData = new FormData(form);
        isAnalyzing = true;
        contentSelect.disabled = true;
        fileInput.disabled = true;
        submitButton.disabled = true;
        form.setAttribute("aria-busy", "true");
        submitButton.innerHTML =
            '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Analyzing questions...';

        previewHost.replaceChildren();
        progressLog.replaceChildren();
        setLiveStatus(
            "Uploading and starting analysis",
            "Reading the Excel workbook. Each row will be validated separately; line breaks remain inside each cell.",
            "loading");
        appendProgress("Preparing the selected Notes chapter and Excel workbook...");

        try {
            const response = await fetch(form.dataset.analyzeUrl, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "Accept": "application/x-ndjson"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(response.status === 413
                    ? "The workbook is too large. Maximum upload size is 5 MB."
                    : "The analysis request failed. Refresh the page and sign in again if your session expired.");
            }

            if (!response.headers.get("content-type")?.includes("application/x-ndjson") || !response.body) {
                throw new Error("The server did not start the live analysis stream.");
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = "";
            let resultEvent = null;

            while (true) {
                const { value, done } = await reader.read();
                buffer += decoder.decode(value || new Uint8Array(), { stream: !done });

                let newlineIndex = buffer.indexOf("\n");
                while (newlineIndex >= 0) {
                    const line = buffer.slice(0, newlineIndex);
                    buffer = buffer.slice(newlineIndex + 1);
                    processLine(line, event => { resultEvent = event; });
                    newlineIndex = buffer.indexOf("\n");
                }

                if (done) break;
            }

            if (buffer.trim()) {
                processLine(buffer, event => { resultEvent = event; });
            }

            if (!resultEvent || !resultEvent.previewToken) {
                throw new Error("The server ended analysis without returning a preview.");
            }

            await loadPreview(resultEvent.previewToken);

            if (resultEvent.canImport) {
                setLiveStatus(
                    "Analysis complete",
                    resultEvent.validCount + " valid programming question(s) are ready to review. No question or solution has been saved yet.",
                    "complete");
            } else {
                setLiveStatus(
                    "Analysis finished with issues",
                    resultEvent.validCount + " valid and " + resultEvent.invalidCount +
                        " invalid question(s), with " + resultEvent.fileErrorCount +
                        " workbook issue(s). Review the details below; invalid batches cannot be saved.",
                    "error");
            }

            previewHost.scrollIntoView({ behavior: "smooth", block: "start" });
        } catch (error) {
            setLiveStatus(
                "Analysis could not be completed",
                error instanceof Error ? error.message : "The workbook could not be analyzed. Please try again.",
                "error");
        } finally {
            isAnalyzing = false;
            contentSelect.disabled = false;
            fileInput.disabled = false;
            submitButton.disabled = false;
            submitButton.innerHTML = originalButtonMarkup;
            form.removeAttribute("aria-busy");
        }
    }

    fileInput.addEventListener("change", () => {
        if (fileInput.files && fileInput.files.length > 0) void analyzeWorkbook();
    });

    contentSelect.addEventListener("change", () => {
        if (fileInput.files && fileInput.files.length > 0) void analyzeWorkbook();
    });

    form.addEventListener("submit", event => {
        event.preventDefault();
        void analyzeWorkbook();
    });
})();
