document.addEventListener("DOMContentLoaded", function () {
    const modalElement=document.getElementById("courseModal"),form=document.getElementById("courseForm"),idInput=document.getElementById("courseId"),nameInput=document.getElementById("courseName"),feesInput=document.getElementById("feesAmount"),dateInput=document.getElementById("feesChangeDate"),installmentInput=document.getElementById("installmentPercentage"),title=document.getElementById("courseModalLabel"),description=document.getElementById("courseModalDescription"),submit=document.getElementById("courseSubmitBtn"),search=document.getElementById("courseSearch"),rows=Array.from(document.querySelectorAll("[data-course-row]")),visibleCount=document.getElementById("courseVisibleCount"),searchEmpty=document.getElementById("courseSearchEmpty"),archiveToggle=document.getElementById("archiveToggle"),archivePanel=document.getElementById("archivePanel");

    function resetForm(){if(!form)return;form.reset();idInput.value="0";}
    function fillEdit(){const c=window.ciitEditCourse;if(!c)return;idInput.value=c.id;nameInput.value=c.courseName||"";feesInput.value=c.feesAmount??"";dateInput.value=c.feesChangeDate||"";installmentInput.value=c.installmentPercentage??"";title.textContent="Edit Course";description.textContent="Update the selected active course.";submit.querySelector("span").textContent="Update Course";}

    if(modalElement){
        modalElement.addEventListener("show.bs.modal",function(e){
            const mode=e.relatedTarget?.getAttribute("data-course-mode");
            if(mode==="create"){resetForm();title.textContent="Add Course";description.textContent="Create a new active course.";submit.querySelector("span").textContent="Save Course";}
            else if(window.ciitEditCourse) fillEdit();
        });
        if(window.ciitEditCourse) bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    if(search){search.addEventListener("input",function(){const term=search.value.trim().toLowerCase();let count=0;rows.forEach(function(row){const match=!term||row.dataset.courseName.includes(term);row.hidden=!match;if(match)count++;});visibleCount.textContent=count.toLocaleString();searchEmpty.hidden=rows.length===0||count!==0;});}

    if(archiveToggle&&archivePanel){archiveToggle.addEventListener("click",function(){const open=archiveToggle.getAttribute("aria-expanded")==="true";archiveToggle.setAttribute("aria-expanded",open?"false":"true");archivePanel.hidden=open;});}

    document.querySelectorAll(".ciit-course-alert-close").forEach(function(btn){btn.addEventListener("click",function(){btn.closest(".ciit-course-alert")?.remove();});});
    document.querySelectorAll(".js-course-delete-form").forEach(function(f){f.addEventListener("submit",function(e){const n=f.dataset.courseName||"this course";if(!window.confirm("Archive "+n+"? You can restore it later from Archived courses."))e.preventDefault();});});
});
