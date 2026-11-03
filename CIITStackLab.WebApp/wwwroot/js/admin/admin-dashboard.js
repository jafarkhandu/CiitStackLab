document.addEventListener("DOMContentLoaded", function () {
    const shell = document.getElementById("ciitAdminShell");
    const toggle = document.getElementById("ciitAdminMenuToggle");
    const overlay = document.getElementById("ciitAdminOverlay");

    if (shell && toggle && overlay) {
        const closeMenu = function () {
            shell.classList.remove("sidebar-open");
            overlay.classList.remove("is-visible");
            toggle.setAttribute("aria-expanded", "false");
        };

        toggle.addEventListener("click", function () {
            const open = shell.classList.toggle("sidebar-open");
            overlay.classList.toggle("is-visible", open);
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        });

        overlay.addEventListener("click", closeMenu);

        window.addEventListener("resize", function () {
            if (window.innerWidth > 992) {
                closeMenu();
            }
        });
    }

    document.querySelectorAll("[data-count]").forEach(function (element) {
        const target = Number(element.getAttribute("data-count"));
        if (!Number.isFinite(target) || target === 0) {
            return;
        }

        const duration = 550;
        const start = performance.now();

        const tick = function (now) {
            const progress = Math.min((now - start) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            element.textContent = Math.round(target * eased).toLocaleString();

            if (progress < 1) {
                requestAnimationFrame(tick);
            }
        };

        requestAnimationFrame(tick);
    });
});


/* ==================== TOPICS MODULE ==================== */
document.addEventListener("DOMContentLoaded", function () {
    const modal=document.getElementById("topicModal"),form=document.getElementById("topicForm"),idInput=document.getElementById("topicId"),nameInput=document.getElementById("topicName"),folderInput=document.getElementById("publicFolderId"),title=document.getElementById("topicModalLabel"),description=document.getElementById("topicModalDescription"),submit=document.getElementById("topicSubmitBtn"),search=document.getElementById("topicSearch"),rows=Array.from(document.querySelectorAll("[data-topic-row]")),count=document.getElementById("topicVisibleCount"),empty=document.getElementById("topicSearchEmpty"),archiveToggle=document.getElementById("topicArchiveToggle"),archivePanel=document.getElementById("topicArchivePanel");

    function reset(){form?.reset();if(idInput)idInput.value="0";}
    function fill(){const t=window.ciitEditTopic;if(!t)return;idInput.value=t.id;nameInput.value=t.topicName||"";folderInput.value=t.publicFolderId||"";title.textContent="Edit Topic";description.textContent="Update the selected active topic.";submit.querySelector("span").textContent="Update Topic";}

    if(modal){modal.addEventListener("show.bs.modal",function(e){if(e.relatedTarget?.getAttribute("data-topic-mode")==="create"){reset();title.textContent="Add Topic";description.textContent="Create a new active learning topic.";submit.querySelector("span").textContent="Save Topic";}else if(window.ciitEditTopic)fill();});if(window.ciitEditTopic)bootstrap.Modal.getOrCreateInstance(modal).show();}
    if(search){search.addEventListener("input",function(){const term=search.value.trim().toLowerCase();let shown=0;rows.forEach(r=>{const match=!term||r.dataset.topicName.includes(term);r.hidden=!match;if(match)shown++;});count.textContent=shown.toLocaleString();empty.hidden=rows.length===0||shown!==0;});}
    if(archiveToggle&&archivePanel){archiveToggle.addEventListener("click",function(){const open=archiveToggle.getAttribute("aria-expanded")==="true";archiveToggle.setAttribute("aria-expanded",open?"false":"true");archivePanel.hidden=open;});}
    document.querySelectorAll(".ciit-topic-alert-close").forEach(b=>b.addEventListener("click",()=>b.closest(".ciit-topic-alert")?.remove()));
    document.querySelectorAll(".js-topic-delete-form").forEach(f=>f.addEventListener("submit",function(e){if(!window.confirm("Archive "+(f.dataset.topicName||"this topic")+"? You can restore it later."))e.preventDefault();}));
});


/* ==================== DASHBOARD MODAL NAVIGATION ==================== */
document.addEventListener("DOMContentLoaded", function () {
    const modalBody = document.getElementById("ciitDashboardModalBody");
    const modalTitle = document.getElementById("ciitDashboardModalTitle");

    if (!modalBody || !modalTitle) {
        return;
    }

    const routes = {
        "Active Courses": function () { return "/Admin/Courses"; },
        "Active Topics": function () { return "/Admin/Topics"; },
        "Learning Content": function (id) { return "/Admin/Content/Details?id=" + encodeURIComponent(id); },
        "Active MCQs": function (id) { return "/Admin/Assessments/Mcqs?editId=" + encodeURIComponent(id); }
    };

    function getTarget(row) {
        const idElement = row.querySelector(".ciit-dashboard-modal-number");
        const id = idElement?.textContent.trim();
        const routeFactory = routes[modalTitle.textContent.trim()];
        return id && routeFactory ? routeFactory(id) : null;
    }

    function makeRowInteractive(row) {
        const target = getTarget(row);
        if (!target) {
            return;
        }

        row.setAttribute("role", "link");
        row.setAttribute("tabindex", "0");
        row.style.cursor = "pointer";
        row.setAttribute("aria-label", "Open selected " + modalTitle.textContent.trim().toLowerCase().replace(/^active /, ""));
    }

    const observeRows = function () {
        modalBody.querySelectorAll(".ciit-dashboard-modal-item").forEach(makeRowInteractive);
    };

    const observer = new MutationObserver(observeRows);
    observer.observe(modalBody, { childList: true });
    observeRows();

    modalBody.addEventListener("click", function (event) {
        const row = event.target.closest(".ciit-dashboard-modal-item");
        if (!row || !modalBody.contains(row)) {
            return;
        }

        const target = getTarget(row);
        if (target) {
            window.location.href = target;
        }
    });

    modalBody.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        const row = event.target.closest(".ciit-dashboard-modal-item");
        if (!row || !modalBody.contains(row)) {
            return;
        }

        event.preventDefault();
        const target = getTarget(row);
        if (target) {
            window.location.href = target;
        }
    });
});


/* ==================== DASHBOARD QUICK ACTIONS ==================== */
document.addEventListener("DOMContentLoaded", function () {
    const statGrid = document.querySelector(".ciit-admin-stat-grid");
    const modal = document.getElementById("ciitDashboardModal");
    const modalBody = document.getElementById("ciitDashboardModalBody");
    const modalTitle = document.getElementById("ciitDashboardModalTitle");
    const modalSummary = document.getElementById("ciitDashboardModalSummary");

    if (!statGrid || !modal || !modalBody || !modalTitle || !modalSummary || document.getElementById("ciitDashboardQuickActions")) {
        return;
    }

    const actions = [
        { title: "Add Course", description: "Create a new course", icon: "bi-journal-plus", url: "/Admin/Courses" },
        { title: "Add Topic", description: "Create a learning topic", icon: "bi-diagram-3", url: "/Admin/Topics" },
        { title: "Add Content", description: "Create a lesson or content", icon: "bi-file-earmark-plus", url: "/Admin/Content/Create" },
        { title: "Add MCQ", description: "Create an assessment question", icon: "bi-ui-checks", url: "/Admin/Assessments/CreateMcq" },
        { title: "Add Interview Question", description: "Add an interview question", icon: "bi-chat-square-text", url: "/Admin/Assessments/CreateInterviewQuestion" },
        { title: "Add Practice Program", description: "Create a practice program", icon: "bi-code-square", url: "/Admin/Assessments/CreatePracticeProgram" }
    ];

    const style = document.createElement("style");
    style.textContent = `
        .ciit-dashboard-quick-actions {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 10px;
            padding: 16px 0 2px;
        }

        .ciit-dashboard-quick-action {
            display: flex;
            align-items: center;
            gap: 12px;
            min-width: 0;
            padding: 13px 14px;
            border: 1px solid #e5eaf2;
            border-radius: 14px;
            background: #fff;
            color: #172033;
            text-decoration: none;
            transition: border-color .18s ease, background .18s ease, transform .18s ease, box-shadow .18s ease;
        }

        .ciit-dashboard-quick-action:hover,
        .ciit-dashboard-quick-action:focus-visible {
            border-color: #b8d0fb;
            background: #f8fbff;
            box-shadow: 0 8px 20px rgba(23, 105, 255, .08);
            transform: translateY(-1px);
            color: #172033;
            outline: none;
        }

        .ciit-dashboard-quick-action i {
            width: 38px;
            height: 38px;
            flex: 0 0 38px;
            display: grid;
            place-items: center;
            border-radius: 11px;
            background: #eef4ff;
            color: #1769ff;
            font-size: 1rem;
        }

        .ciit-dashboard-quick-action strong {
            display: block;
            font-size: .8rem;
            font-weight: 800;
        }

        .ciit-dashboard-quick-action span {
            display: block;
            margin-top: 3px;
            color: #718096;
            font-size: .68rem;
            line-height: 1.35;
        }

        .ciit-dashboard-modal-dialog.ciit-dashboard-modal-dialog--quick {
            width: min(620px, 100%);
        }

        @media (max-width: 520px) {
            .ciit-dashboard-quick-actions {
                grid-template-columns: 1fr;
            }
        }
    `;
    document.head.appendChild(style);

    const card = document.createElement("article");
    card.id = "ciitDashboardQuickActions";
    card.className = "ciit-admin-stat-card";
    card.setAttribute("tabindex", "0");
    card.setAttribute("role", "button");
    card.setAttribute("aria-label", "Open quick actions");
    card.innerHTML = `
        <div class="ciit-admin-stat-icon"><i class="bi bi-plus-circle"></i></div>
        <div class="ciit-admin-stat-copy">
            <span>Quick Actions</span>
            <strong>Add New</strong>
        </div>
    `;
    statGrid.appendChild(card);

    const closeQuickActions = function () {
        modal.classList.remove("ciit-dashboard-modal--quick");
        modal.querySelector(".ciit-dashboard-modal-dialog")?.classList.remove("ciit-dashboard-modal-dialog--quick");
    };

    const openQuickActions = function () {
        modalTitle.textContent = "Quick Actions";
        modalSummary.textContent = "Choose what you want to add to the learning platform.";
        modalBody.innerHTML = `
            <div class="ciit-dashboard-quick-actions">
                ${actions.map(function (action) {
                    return `<a class="ciit-dashboard-quick-action" href="${action.url}">
                        <i class="bi ${action.icon}"></i>
                        <span><strong>${action.title}</strong>${action.description}</span>
                    </a>`;
                }).join("")}
            </div>
        `;
        modal.querySelector(".ciit-dashboard-modal-dialog")?.classList.add("ciit-dashboard-modal-dialog--quick");
        modal.hidden = false;
        document.body.classList.add("ciit-dashboard-modal-open");
        modal.querySelector(".ciit-dashboard-modal-close")?.focus();
    };

    card.addEventListener("click", openQuickActions);
    card.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }
        event.preventDefault();
        openQuickActions();
    });

    modal.querySelectorAll("[data-dashboard-modal-close]").forEach(function (button) {
        button.addEventListener("click", closeQuickActions);
    });
});
