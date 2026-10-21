(() => {
    "use strict";

    const uploadForm = document.getElementById("mcqImportUploadForm");

    if (!uploadForm) {
        return;
    }

    const chapterSelect = document.getElementById("ImportMcqTopicId");
    const fileInput = document.getElementById("ImportMcqFile");
    const uploadButton = uploadForm.querySelector('button[type="submit"]');
    const previewHost = document.getElementById("mcqImportPreviewHost");
    const liveStatus = document.getElementById("mcqImportLiveStatus");
    const liveTitle = document.getElementById("mcqImportLiveTitle");
    const liveState = document.getElementById("mcqImportLiveState");
    const liveDetail = document.getElementById("mcqImportLiveDetail");
    const progressLog = document.getElementById("mcqImportProgressLog");

    if (!chapterSelect || !fileInput || !uploadButton || !previewHost ||
        !liveStatus || !liveTitle || !liveState || !liveDetail || !progressLog) {
        return;
    }

    const originalUploadButtonMarkup = uploadButton.innerHTML;
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

    function addProgressMessage(message) {
        const existingMessages = Array.from(progressLog.children)
            .map((item) => item.textContent);

        if (existingMessages.includes(message)) {
            return;
        }

        const item = document.createElement("li");
        item.textContent = message;
        progressLog.appendChild(item);
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
            throw new Error(
                response.status === 404
                    ? "The analysis preview expired. Upload the file again."
                    : "The analysis result could not be loaded. Please try again."
            );
        }

        previewHost.innerHTML = await response.text();
    }

    function processStreamLine(line, onResult) {
        if (!line.trim()) {
            return;
        }

        let event;

        try {
            event = JSON.parse(line);
        } catch {
            throw new Error("The server returned an unreadable analysis update.");
        }

        if (event.type === "progress") {
            setLiveStatus("Analyzing the uploaded file", event.message, "loading");
            addProgressMessage(event.message);
        } else if (event.type === "result") {
            onResult(event);
        } else if (event.type === "error") {
            throw new Error(event.message || "The file could not be analyzed.");
        }
    }

    async function analyzeFile() {
        if (isAnalyzing) {
            return;
        }

        const selectedFile = fileInput.files && fileInput.files[0];

        if (!selectedFile) {
            return;
        }

        if (!chapterSelect.value || Number(chapterSelect.value) <= 0) {
            setLiveStatus(
                "Choose a chapter first",
                "Select the target chapter. Analysis will start automatically after the chapter is selected.",
                "error"
            );
            return;
        }

        const formData = new FormData(uploadForm);
        isAnalyzing = true;

        chapterSelect.disabled = true;
        fileInput.disabled = true;
        uploadButton.disabled = true;
        uploadForm.setAttribute("aria-busy", "true");
        uploadButton.innerHTML =
            '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Analyzing file...';

        previewHost.replaceChildren();
        progressLog.replaceChildren();
        setLiveStatus(
            "Uploading and starting analysis",
            "Sending the file to the server. Live validation steps will appear here as they happen.",
            "loading"
        );
        addProgressMessage("Preparing the selected file for analysis...");

        try {
            const response = await fetch(uploadForm.dataset.analyzeUrl, {
                method: "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "Accept": "application/x-ndjson"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(
                    response.status === 413
                        ? "The file is too large. Maximum upload size is 5 MB."
                        : "The analysis request failed. Refresh the page and sign in again if your session expired."
                );
            }

            if (!response.headers.get("content-type")?.includes("application/x-ndjson") ||
                !response.body) {
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
                    processStreamLine(line, (event) => { resultEvent = event; });
                    newlineIndex = buffer.indexOf("\n");
                }

                if (done) {
                    break;
                }
            }

            if (buffer.trim()) {
                processStreamLine(buffer, (event) => { resultEvent = event; });
            }

            if (!resultEvent || !resultEvent.previewToken) {
                throw new Error("The server ended analysis without returning a preview.");
            }

            await loadPreview(resultEvent.previewToken);

            if (resultEvent.canImport) {
                setLiveStatus(
                    "Analysis complete",
                    resultEvent.validCount + " valid MCQs are ready for chapter-wise review. Nothing has been saved yet; click Add Questions when ready.",
                    "complete"
                );
            } else {
                setLiveStatus(
                    "Analysis finished with issues",
                    resultEvent.validCount + " valid and " + resultEvent.invalidCount +
                        " invalid question(s) detected, with " + resultEvent.fileErrorCount +
                        " file-level issue(s). Review the details below; invalid batches cannot be saved.",
                    "error"
                );
            }

            previewHost.scrollIntoView({ behavior: "smooth", block: "start" });
        } catch (error) {
            setLiveStatus(
                "Analysis could not be completed",
                error instanceof Error
                    ? error.message
                    : "The file could not be analyzed. Please try again.",
                "error"
            );
        } finally {
            isAnalyzing = false;
            chapterSelect.disabled = false;
            fileInput.disabled = false;
            uploadButton.disabled = false;
            uploadButton.innerHTML = originalUploadButtonMarkup;
            uploadForm.removeAttribute("aria-busy");
        }
    }

    fileInput.addEventListener("change", () => {
        if (fileInput.files && fileInput.files.length > 0) {
            void analyzeFile();
        }
    });

    chapterSelect.addEventListener("change", () => {
        if (fileInput.files && fileInput.files.length > 0) {
            void analyzeFile();
        }
    });

    uploadForm.addEventListener("submit", (event) => {
        event.preventDefault();
        void analyzeFile();
    });
})();
