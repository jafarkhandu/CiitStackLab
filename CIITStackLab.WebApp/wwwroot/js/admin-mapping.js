document.addEventListener("DOMContentLoaded",function(){
    const courseSearch=document.getElementById("mappingCourseSearch"),courseItems=Array.from(document.querySelectorAll(".ciit-mapping-course-item")),courseEmpty=document.getElementById("mappingCourseEmpty");
    if(courseSearch){courseSearch.addEventListener("input",function(){const q=courseSearch.value.trim().toLowerCase();let shown=0;courseItems.forEach(i=>{const match=!q||i.dataset.courseName.includes(q);i.hidden=!match;if(match)shown++;});if(courseEmpty)courseEmpty.hidden=shown!==0;});}

    const topicSearch=document.getElementById("mappingTopicSearch"),cards=Array.from(document.querySelectorAll("[data-topic-card]")),selectedCount=document.getElementById("mappingSelectedCount"),noTopics=document.getElementById("mappingNoTopics");
    function refresh(){
        let visible=0;
        cards.forEach(card=>{const input=card.querySelector("input");card.classList.toggle("is-assigned",!!input?.checked);if(!card.hidden)visible++;});
        if(selectedCount)selectedCount.textContent=cards.filter(c=>c.querySelector("input")?.checked).length.toLocaleString();
        if(noTopics)noTopics.hidden=visible!==0;
    }
    if(topicSearch){topicSearch.addEventListener("input",function(){const q=topicSearch.value.trim().toLowerCase();cards.forEach(c=>c.hidden=!!q&&!c.dataset.topicName.includes(q));refresh();});}
    cards.forEach(c=>c.addEventListener("click",function(e){if(e.target.tagName!=="INPUT")c.querySelector("input").click();refresh();}));
    document.getElementById("mappingSelectAll")?.addEventListener("click",function(){cards.filter(c=>!c.hidden).forEach(c=>c.querySelector("input").checked=true);refresh();});
    document.getElementById("mappingClearAll")?.addEventListener("click",function(){cards.filter(c=>!c.hidden).forEach(c=>c.querySelector("input").checked=false);refresh();});
    document.querySelectorAll(".ciit-mapping-alert-close").forEach(b=>b.addEventListener("click",()=>b.closest(".ciit-mapping-alert")?.remove()));
    document.getElementById("mappingForm")?.addEventListener("submit",function(){const btn=document.getElementById("mappingSaveBtn");if(btn){btn.disabled=true;btn.querySelector("span").textContent="Saving...";}});
    refresh();
});
