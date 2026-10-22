(function () {
    "use strict";

    const styleId = "ciit-inline-assessment-styles";

    function ensureStyles() {
        if (document.getElementById(styleId)) return;
        const style = document.createElement("style");
        style.id = styleId;
        style.textContent = `
            .ciit-assessment-inline-results .ciit-learning-options input[type='radio'] { opacity:0 !important; }
            .ciit-assessment-inline-results .ciit-learning-options label.ciit-inline-correct { border-color:#2f9e62 !important; background:#eefaf2 !important; box-shadow:inset 0 0 0 1px rgba(47,158,98,.08); }
            .ciit-assessment-inline-results .ciit-learning-options label.ciit-inline-wrong { border-color:#d9535f !important; background:#fff0f1 !important; box-shadow:inset 0 0 0 1px rgba(217,83,95,.08); }
            .ciit-assessment-inline-results .ciit-learning-options label.ciit-inline-correct .ciit-learning-option-letter { background:#d8f5e2 !important; color:#247548 !important; }
            .ciit-assessment-inline-results .ciit-learning-options label.ciit-inline-wrong .ciit-learning-option-letter { background:#ffe1e3 !important; color:#ae343d !important; }
            .ciit-inline-option-status { display:inline-flex; align-items:center; gap:6px; margin-left:auto; font-size:.82rem; font-weight:700; white-space:nowrap; }
            .ciit-inline-question-status { display:inline-flex; align-items:center; margin-left:10px; font-size:.78rem; font-weight:700; }
            .ciit-inline-question-status.is-correct,.ciit-assessment-inline-results .ciit-learning-options label.ciit-inline-correct .ciit-inline-option-status { color:#247548; }
            .ciit-inline-question-status.is-wrong,.ciit-assessment-inline-results .ciit-learning-options label.ciit-inline-wrong .ciit-inline-option-status { color:#ae343d; }
            .ciit-assessment-inline-summary { display:flex; align-items:center; justify-content:space-between; gap:16px; margin-top:22px; padding:18px 20px; border:1px solid #dbe5f2; border-radius:16px; background:linear-gradient(135deg,#f8fbff,#fff); }
            .ciit-assessment-inline-summary-copy { display:grid; gap:4px; }
            .ciit-assessment-inline-summary-copy strong { font-size:1rem; color:#1f3554; }
            .ciit-assessment-inline-summary-copy span { color:#6c7b91; font-size:.88rem; }
            .ciit-assessment-inline-score { font-size:1.25rem; font-weight:800; color:#1f3554; white-space:nowrap; }
            .ciit-assessment-inline-score span { color:#2f9e62; }
            @media (max-width:640px) { .ciit-assessment-inline-summary { align-items:flex-start; flex-direction:column; } .ciit-inline-option-status { margin-left:0; width:100%; } }
        `;
        document.head.appendChild(style);
    }

    function getInlineEndpoint(form) {
        const action = form.action || "";
        if (action.includes("/TopicAssessment/")) return action.replace("/TopicAssessment/", "/TopicAssessmentInline/");
        if (action.includes("/Assessment/")) return action.replace("/Assessment/", "/AssessmentInline/");
        return null;
    }

    function getExistingResultEndpoint(form) {
        let actionUrl;
        try { actionUrl = new URL(form.action || "", window.location.origin); } catch { return null; }
        const topicMatch = actionUrl.pathname.match(/\/TopicAssessment\/(\d+)\/?$/i);
        const courseMatch = window.location.pathname.match(/\/Courses\/Learn\/(\d+)/i);
        if (!courseMatch || !topicMatch) return null;
        return `/Courses/Learn/${encodeURIComponent(courseMatch[1])}/TopicAssessmentResult/${encodeURIComponent(topicMatch[1])}`;
    }

    function showError(form, message) {
        let error = form.parentElement.querySelector(":scope > .ciit-assessment-error");
        if (!error) {
            error = document.createElement("div");
            error.className = "alert alert-warning ciit-assessment-error";
            error.setAttribute("role", "alert");
            form.parentElement.insertBefore(error, form);
        }
        error.replaceChildren();
        const icon = document.createElement("i");
        icon.className = "bi bi-exclamation-triangle-fill me-2";
        error.append(icon, document.createTextNode(message));
        error.hidden = false;
    }

    function clearError(form) {
        const error = form.parentElement.querySelector(":scope > .ciit-assessment-error");
        if (error) error.hidden = true;
    }

    function getSubmitButton(form) { return form.querySelector(".ciit-assessment-submit-row button[type='submit']"); }

    // QuestionId is outside .ciit-learning-question. Pagination moves question elements,
    // so resolve the hidden QuestionId through the stable answers[index] radio name.
    function getQuestionId(questionElement) {
        const radio = questionElement.querySelector("input[type='radio'][name$='.SelectedOptionNumber']");
        const match = radio?.name?.match(/^answers\[(\d+)\]\.SelectedOptionNumber$/);
        if (!match) return Number.MAX_SAFE_INTEGER;
        const form = questionElement.closest("form");
        const hidden = form?.querySelector(`input[name="answers[${match[1]}].QuestionId"]`);
        const questionId = hidden ? Number(hidden.value) : Number.MAX_SAFE_INTEGER;
        return Number.isFinite(questionId) ? questionId : Number.MAX_SAFE_INTEGER;
    }

    function normalizeQuestionOrder(form) {
        if (form.dataset.ciitPaginated === "true") return;
        Array.from(form.querySelectorAll(":scope > .ciit-learning-question"))
            .sort((left, right) => getQuestionId(left) - getQuestionId(right))
            .forEach(question => form.appendChild(question));
    }

    function markQuestion(questionElement, questionResult) {
        const questionId = getQuestionId(questionElement);
        const resultQuestionId = Number(questionResult.questionId);
        if (!Number.isFinite(resultQuestionId) || questionId !== resultQuestionId) return;

        const correctOptionNumber = Number(questionResult.correctOptionNumber);
        const selectedOptionNumber = questionResult.selectedOptionNumber == null ? null : Number(questionResult.selectedOptionNumber);
        const title = questionElement.querySelector(".ciit-learning-question-title");
        if (title) {
            title.querySelector(".ciit-inline-question-status")?.remove();
            const status = document.createElement("span");
            status.className = `ciit-inline-question-status ${questionResult.isCorrect ? "is-correct" : "is-wrong"}`;
            status.innerHTML = questionResult.isCorrect ? '<i class="bi bi-check-circle-fill me-1"></i>Correct' : '<i class="bi bi-x-circle-fill me-1"></i>Incorrect';
            title.appendChild(status);
        }

        questionElement.querySelectorAll(".ciit-learning-options label").forEach(label => {
            const input = label.querySelector("input[type='radio']");
            if (!input) return;
            const optionNumber = Number(input.value);
            const isCorrectOption = optionNumber === correctOptionNumber;
            const isSelectedOption = selectedOptionNumber !== null && optionNumber === selectedOptionNumber;
            input.checked = isSelectedOption;
            input.disabled = true;
            input.required = false;
            label.classList.remove("ciit-inline-correct", "ciit-inline-wrong");
            label.querySelector(".ciit-inline-option-status")?.remove();
            if (isCorrectOption) label.classList.add("ciit-inline-correct");
            if (isSelectedOption && !isCorrectOption) label.classList.add("ciit-inline-wrong");
            if (isCorrectOption || (isSelectedOption && !isCorrectOption)) {
                const status = document.createElement("span");
                status.className = "ciit-inline-option-status";
                status.innerHTML = isCorrectOption ? '<i class="bi bi-check-circle-fill"></i>Correct answer' : '<i class="bi bi-x-circle-fill"></i>Your answer';
                label.appendChild(status);
            }
        });
    }

    function renderResult(form, result) {
        if (!result || !Array.isArray(result.questions)) throw new Error("The assessment result is incomplete. Please reload the lesson.");
        ensureStyles();
        form.classList.add("ciit-assessment-inline-results");
        form.dataset.ciitAssessmentSubmitted = "true";
        form.dataset.ciitAssessmentSubmitting = "false";
        form.setAttribute("aria-label", "Assessment results");
        const resultsByQuestion = new Map(result.questions.map(question => [Number(question.questionId), question]));
        form.querySelectorAll(".ciit-learning-question").forEach(questionElement => {
            const questionResult = resultsByQuestion.get(getQuestionId(questionElement));
            if (questionResult) markQuestion(questionElement, questionResult);
        });
        form.querySelectorAll("input[type='radio']").forEach(input => { input.disabled = true; input.required = false; });
        form.querySelectorAll(".ciit-assessment-submit-row").forEach(row => row.remove());
        form.querySelector(".ciit-assessment-inline-summary")?.remove();

        const summary = document.createElement("div");
        summary.className = "ciit-assessment-inline-summary";
        summary.setAttribute("role", "status");
        summary.setAttribute("aria-live", "polite");
        const copy = document.createElement("div");
        copy.className = "ciit-assessment-inline-summary-copy";
        const heading = document.createElement("strong");
        heading.textContent = "Assessment submitted";
        const detail = document.createElement("span");
        detail.textContent = "Your answers have been graded. Correct answers are highlighted in green and incorrect selections in red.";
        copy.append(heading, detail);
        const score = document.createElement("div");
        score.className = "ciit-assessment-inline-score";
        score.textContent = `Score: ${result.score} / ${result.totalQuestions}`;
        const percentage = document.createElement("span");
        percentage.textContent = ` (${result.percentage}%)`;
        score.appendChild(percentage);
        summary.append(copy, score);
        form.appendChild(summary);
    }

    async function submitAssessment(form, submitButton) {
        const endpoint = getInlineEndpoint(form);
        if (!endpoint) { showError(form, "The assessment submission endpoint could not be resolved. Please reload the lesson."); return; }
        clearError(form);
        form.dataset.ciitAssessmentSubmitting = "true";
        if (submitButton) { submitButton.disabled = true; submitButton.innerHTML = '<i class="bi bi-hourglass-split me-2"></i>Submitting…'; }
        const controller = new AbortController();
        const timeoutId = window.setTimeout(() => controller.abort(), 30000);
        try {
            const response = await fetch(endpoint, { method:"POST", body:new FormData(form), credentials:"same-origin", headers:{Accept:"application/json"}, signal:controller.signal });
            let payload = null;
            try { payload = await response.json(); } catch { payload = null; }
            if (!response.ok || !payload?.result) throw new Error(payload?.message || "The assessment could not be submitted. Please reload the lesson and try again.");
            renderResult(form, payload.result);
        } catch (error) {
            form.dataset.ciitAssessmentSubmitting = "false";
            if (submitButton) { submitButton.disabled = false; submitButton.innerHTML = '<i class="bi bi-send-check me-2"></i>Submit assessment'; }
            const message = error?.name === "AbortError" ? "The assessment submission timed out. Please check your connection and try again." : error instanceof Error ? error.message : "The assessment could not be submitted. Please try again.";
            showError(form, message);
        } finally { window.clearTimeout(timeoutId); }
    }

    async function restoreExistingResult(form) {
        if (form.dataset.ciitAssessmentSubmitted === "true") return;
        const endpoint = getExistingResultEndpoint(form);
        if (!endpoint) return;
        const controller = new AbortController();
        const timeoutId = window.setTimeout(() => controller.abort(), 10000);
        try {
            const response = await fetch(endpoint, { method:"GET", credentials:"same-origin", headers:{Accept:"application/json"}, signal:controller.signal });
            if (!response.ok) return;
            const payload = await response.json();
            if (payload?.result) renderResult(form, payload.result);
        } catch {
            // Best-effort restoration; leave the form usable if no saved attempt is available.
        } finally { window.clearTimeout(timeoutId); }
    }

    function initializeForm(form) {
        if (form.dataset.ciitAssessmentInlineInitialized === "true") return;
        form.dataset.ciitAssessmentInlineInitialized = "true";
        normalizeQuestionOrder(form);
        form.addEventListener("submit", event => {
            if (form.dataset.ciitAssessmentSubmitted === "true" || form.dataset.ciitAssessmentSubmitting === "true") { event.preventDefault(); return; }
            if (!form.checkValidity()) return;
            event.preventDefault();
            void submitAssessment(form, getSubmitButton(form));
        });
        void restoreExistingResult(form);
    }

    function initialize() { document.querySelectorAll("form[data-ciit-assessment-inline]").forEach(initializeForm); }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, {once:true});
    else initialize();
})();
