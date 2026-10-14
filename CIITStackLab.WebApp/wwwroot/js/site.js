document.addEventListener("DOMContentLoaded",function(){const t=document.getElementById("ciitTopicsScroll"),p=document.getElementById("topicsPrev"),n=document.getElementById("topicsNext");if(t&&p&&n){const a=350;p.addEventListener("click",function(){t.scrollBy({left:-a,behavior:"smooth"})});n.addEventListener("click",function(){t.scrollBy({left:a,behavior:"smooth"})})}const s=document.querySelectorAll(".ciit-hero-slide"),d=document.querySelectorAll(".ciit-slider-dot");if(!s.length||!d.length)return;let c=0,timer;function show(i){s.forEach(x=>x.classList.remove("active"));d.forEach(x=>x.classList.remove("active"));if(s[i]&&d[i]){s[i].classList.add("active");d[i].classList.add("active")}}function restart(){clearInterval(timer);timer=setInterval(function(){c=(c+1)%s.length;show(c)},4000)}d.forEach(function(x,i){x.addEventListener("click",function(){c=i;show(c);restart()})});show(0);restart()});

document.addEventListener("DOMContentLoaded", function () {
    const hubLink = document.querySelector("[data-learning-hub-transition]");
    const transition = document.getElementById("ciitHubTransition");

    if (!hubLink || !transition) {
        return;
    }

    hubLink.addEventListener("click", function (event) {
        if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        event.preventDefault();

        if (hubLink.dataset.transitioning === "true") {
            return;
        }

        hubLink.dataset.transitioning = "true";
        hubLink.setAttribute("aria-disabled", "true");
        transition.classList.add("is-active");
        transition.setAttribute("aria-hidden", "false");
        document.body.classList.add("ciit-hub-transitioning");

        const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        window.setTimeout(function () {
            window.location.assign(hubLink.href);
        }, reducedMotion ? 120 : 720);
    });

    window.addEventListener("pageshow", function () {
        transition.classList.remove("is-active");
        transition.setAttribute("aria-hidden", "true");
        document.body.classList.remove("ciit-hub-transitioning");
        hubLink.dataset.transitioning = "false";
        hubLink.removeAttribute("aria-disabled");
    });
});
