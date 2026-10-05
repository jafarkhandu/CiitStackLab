document.addEventListener("DOMContentLoaded", function () {

    const topicsContainer =
        document.getElementById("ciitTopicsScroll");

    const previousButton =
        document.getElementById("topicsPrev");

    const nextButton =
        document.getElementById("topicsNext");


    if (!topicsContainer ||
        !previousButton ||
        !nextButton) {

        return;
    }


    const scrollAmount = 350;


    previousButton.addEventListener("click", function () {

        topicsContainer.scrollBy({
            left: -scrollAmount,
            behavior: "smooth"
        });

    });


    nextButton.addEventListener("click", function () {

        topicsContainer.scrollBy({
            left: scrollAmount,
            behavior: "smooth"
        });

    });

});

/* =========================================================
   CIIT HERO AUTO SLIDER
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {

    const slides =
        document.querySelectorAll(".ciit-hero-slide");

    const dots =
        document.querySelectorAll(".ciit-slider-dot");

    if (!slides.length || !dots.length) {
        return;
    }

    let currentSlide = 0;


    function showSlide(index) {

        slides.forEach(function (slide) {

            slide.classList.remove("active");

        });


        dots.forEach(function (dot) {

            dot.classList.remove("active");

        });


        slides[index].classList.add("active");

        dots[index].classList.add("active");

    }


    dots.forEach(function (dot, index) {

        dot.addEventListener("click", function () {

            currentSlide = index;

            showSlide(currentSlide);

        });

    });


    setInterval(function () {

        currentSlide++;

        if (currentSlide >= slides.length) {
            currentSlide = 0;
        }

        showSlide(currentSlide);

    }, 4000);

});