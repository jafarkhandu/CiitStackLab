document.addEventListener("DOMContentLoaded", function () {
    const tree = document.querySelector(".ciit-assessment-tree, .ciit-practice-list");
    if (!tree) return;

    const topicNodes = Array.from(tree.querySelectorAll(":scope > details.ciit-assessment-tree-topic"));

    const closeSiblings = node => {
        const parent = node.parentElement;
        if (!parent) return;

        Array.from(parent.children).forEach(sibling => {
            if (sibling !== node && sibling.matches("details.ciit-assessment-tree-topic, details.ciit-assessment-tree-chapter")) {
                sibling.open = false;
            }
        });
    };

    const nodes = Array.from(tree.querySelectorAll("details.ciit-assessment-tree-topic, details.ciit-assessment-tree-chapter"));
    nodes.forEach(node => {
        node.addEventListener("toggle", function () {
            if (node.open) {
                closeSiblings(node);
            }
        });
    });
});
