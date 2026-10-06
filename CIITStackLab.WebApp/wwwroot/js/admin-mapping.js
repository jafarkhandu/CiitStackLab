document.addEventListener("DOMContentLoaded", function () {
    const courseSearch = document.getElementById("mappingCourseSearch");
    const courseItems = Array.from(document.querySelectorAll(".ciit-mapping-course-item"));
    const courseEmpty = document.getElementById("mappingCourseEmpty");

    if (courseSearch) {
        courseSearch.addEventListener("input", function () {
            const query = courseSearch.value.trim().toLowerCase();
            let shown = 0;

            courseItems.forEach(function (item) {
                const matches = !query || item.dataset.courseName.includes(query);
                item.hidden = !matches;

                if (matches) {
                    shown++;
                }
            });

            if (courseEmpty) {
                courseEmpty.hidden = shown !== 0;
            }
        });
    }

    const topicSearch = document.getElementById("mappingTopicSearch");
    const cards = Array.from(document.querySelectorAll("[data-topic-card]"));
    const selectedCount = document.getElementById("mappingSelectedCount");
    const noTopics = document.getElementById("mappingNoTopics");

    function refreshTopics() {
        let visible = 0;
        let selected = 0;

        cards.forEach(function (card) {
            const input = card.querySelector("input[type='checkbox']");
            const checked = !!input?.checked;

            card.classList.toggle("is-assigned", checked);

            if (!card.hidden) {
                visible++;
            }

            if (checked) {
                selected++;
            }
        });

        if (selectedCount) {
            selectedCount.textContent = selected.toLocaleString();
        }

        if (noTopics) {
            noTopics.hidden = visible !== 0;
        }
    }

    cards.forEach(function (card) {
        const input = card.querySelector("input[type='checkbox']");

        // Let the <label> perform native checkbox toggling.
        // Avoid calling input.click() from the card click handler, which
        // would toggle the checkbox twice and make it appear unclickable.
        input?.addEventListener("change", refreshTopics);
    });

    if (topicSearch) {
        topicSearch.addEventListener("input", function () {
            const query = topicSearch.value.trim().toLowerCase();

            cards.forEach(function (card) {
                card.hidden = !!query && !card.dataset.topicName.includes(query);
            });

            refreshTopics();
        });
    }

    document.getElementById("mappingSelectAll")?.addEventListener("click", function () {
        cards
            .filter(function (card) { return !card.hidden; })
            .forEach(function (card) {
                const input = card.querySelector("input[type='checkbox']");
                if (input) {
                    input.checked = true;
                }
            });

        refreshTopics();
    });

    document.getElementById("mappingClearAll")?.addEventListener("click", function () {
        cards
            .filter(function (card) { return !card.hidden; })
            .forEach(function (card) {
                const input = card.querySelector("input[type='checkbox']");
                if (input) {
                    input.checked = false;
                }
            });

        refreshTopics();
    });

    document.querySelectorAll(".ciit-mapping-alert-close").forEach(function (button) {
        button.addEventListener("click", function () {
            button.closest(".ciit-mapping-alert")?.remove();
        });
    });

    document.querySelectorAll(".ciit-mapping-alert").forEach(function (alert) {
        window.setTimeout(function () {
            alert.classList.add("is-fading");

            window.setTimeout(function () {
                alert.remove();
            }, 650);
        }, 5000);
    });

    document.getElementById("mappingForm")?.addEventListener("submit", function () {
        const button = document.getElementById("mappingSaveBtn");

        if (button) {
            button.disabled = true;
            button.querySelector("span").textContent = "Saving...";
        }
    });

    refreshTopics();
});
