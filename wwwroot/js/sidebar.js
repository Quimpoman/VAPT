document.addEventListener("DOMContentLoaded", function () {

    const toggles =
        document.querySelectorAll("[data-sidebar-toggle]");

    toggles.forEach(function (toggle) {

        toggle.addEventListener("click", function () {

            const group =
                toggle.closest(".nav-group");

            group.classList.toggle("open");

        });

    });

});