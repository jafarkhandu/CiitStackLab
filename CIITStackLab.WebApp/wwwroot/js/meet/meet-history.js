document.addEventListener("DOMContentLoaded", () => {
    const root = document.querySelector("[data-meet-admin-index]");
    const modal = document.querySelector("[data-meet-history-modal]");
    const content = modal?.querySelector("[data-meet-history-content]");
    if (!root || !modal || !content) return;

    const close = () => {
        modal.classList.remove("is-open");
        modal.setAttribute("aria-hidden", "true");
        document.body.classList.remove("meet-modal-open");
    };

    const open = () => {
        modal.classList.add("is-open");
        modal.setAttribute("aria-hidden", "false");
        document.body.classList.add("meet-modal-open");
    };

    const addText = (parent, tag, className, value) => {
        const element = document.createElement(tag);
        if (className) element.className = className;
        element.textContent = value ?? "";
        parent.append(element);
        return element;
    };

    const formatDate = value => value
        ? new Date(value).toLocaleString([], { dateStyle: "medium", timeStyle: "short" })
        : "—";

    const render = data => {
        content.replaceChildren();
        if (data.description) addText(content, "p", "meet-history-description", data.description);

        const summary = document.createElement("div");
        summary.className = "meet-history-summary";
        [
            ["Status", data.status],
            ["Actual runtime", data.runtimeMinutes + " min"],
            ["Batch strength", data.totalStudents],
            ["Attended", data.attendedStudents]
        ].forEach(([label, value]) => {
            const stat = document.createElement("div");
            stat.className = "meet-history-stat";
            addText(stat, "span", "", label);
            addText(stat, "strong", "", value);
            summary.append(stat);
        });
        content.append(summary);

        const dates = document.createElement("p");
        dates.className = "meet-history-description";
        dates.textContent = "Scheduled: " + formatDate(data.startsAtUtc)
            + " · Started: " + formatDate(data.startedAtUtc)
            + " · Ended: " + formatDate(data.endedAtUtc)
            + " · Planned duration: " + data.durationMinutes + " min";
        content.append(dates);

        const heading = document.createElement("div");
        heading.className = "meet-history-students-heading";
        addText(heading, "h3", "", "Student attendance");
        addText(heading, "span", "meet-count-pill", data.notAttendedStudents + " not attended");
        content.append(heading);

        const list = document.createElement("div");
        list.className = "meet-history-student-list";
        if (!data.students?.length) {
            addText(list, "p", "meet-history-empty", "No students were assigned to this batch.");
        } else {
            data.students.forEach(student => {
                const row = document.createElement("div");
                row.className = "meet-history-student";
                const identity = document.createElement("div");
                addText(identity, "strong", "", student.name);
                addText(identity, "small", "", student.email || student.status);
                const badge = addText(row, "span",
                    "meet-history-attendance " + (student.attended ? "is-attended" : "is-absent"),
                    student.attended ? "Attended" : "Not attended");
                row.append(identity, badge);
                list.append(row);
            });
        }
        content.append(list);
    };

    root.addEventListener("click", async event => {
        const button = event.target.closest("[data-meet-session-details]");
        if (!button) return;

        open();
        content.replaceChildren();
        addText(content, "p", "meet-history-loading", "Loading meeting details…");
        try {
            const response = await fetch(button.dataset.detailsUrl, {
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) throw new Error("Unable to load meeting details.");
            const data = await response.json();
            render(data);
            const title = modal.querySelector("#meetHistoryTitle");
            if (title) title.textContent = data.title + " · " + data.batchName;
        } catch (error) {
            content.replaceChildren();
            addText(content, "p", "meet-history-empty", error.message);
        }
    });

    root.addEventListener("submit", async event => {
        const form = event.target.closest("form[data-meet-session-delete]");
        if (!form || event.defaultPrevented) return;
        if (!window.confirm("Permanently delete this meeting history and its attendance records?")) {
            event.preventDefault();
            return;
        }
        event.preventDefault();
        const button = form.querySelector('[type="submit"]');
        if (button) button.disabled = true;
        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) throw new Error("Unable to delete this meeting history.");
            form.closest("[data-meet-session-id]")?.remove();
            const list = root.querySelector("[data-meet-admin-session-list]");
            const count = root.querySelector(".meet-session-list-section .meet-count-pill");
            if (count && list) count.textContent = list.querySelectorAll("[data-meet-session-id]").length + " sessions";
        } catch (error) {
            console.error(error);
            if (button) button.disabled = false;
            window.alert(error.message);
        }
    }, true);

    modal.querySelector("[data-close-history]")?.addEventListener("click", close);
    modal.addEventListener("click", event => { if (event.target === modal) close(); });
    document.addEventListener("keydown", event => { if (event.key === "Escape") close(); });
});
