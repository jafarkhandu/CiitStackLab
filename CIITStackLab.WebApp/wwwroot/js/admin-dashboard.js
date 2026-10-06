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
