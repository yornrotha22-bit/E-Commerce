(function () {
    "use strict";

    // ---------------------------------------------------------------------
    // Slug helper: fills the slug field from the product name when left empty.
    // The server also normalizes the slug, so this is only a convenience.
    // ---------------------------------------------------------------------
    function slugify(value) {
        return (value || "")
            .toLowerCase()
            .trim()
            .replace(/[^a-z0-9\u00c0-\uffff]+/g, "-")
            .replace(/-{2,}/g, "-")
            .replace(/^-|-$/g, "");
    }

    var nameInput = document.getElementById("Name");
    var slugInput = document.getElementById("Slug");

    if (nameInput && slugInput) {
        nameInput.addEventListener("blur", function () {
            if (!slugInput.value.trim()) {
                slugInput.value = slugify(nameInput.value);
            }
        });
    }

    // ---------------------------------------------------------------------
    // Variant rows
    // ---------------------------------------------------------------------
    var table = document.getElementById("variants-table");
    var tbody = table ? table.querySelector("tbody") : null;
    var template = document.getElementById("variant-row-template");
    var addButton = document.getElementById("add-variant");
    var indexField = document.getElementById("variant-next-index");

    if (!table || !tbody || !template || !addButton || !indexField) {
        return;
    }

    addButton.addEventListener("click", function () {
        var nextIndex = parseInt(indexField.value || "0", 10);
        var html = template.innerHTML.replace(/__index__/g, nextIndex);

        var holder = document.createElement("tbody");
        holder.innerHTML = html.trim();

        var row = holder.querySelector("tr");
        if (row) {
            tbody.appendChild(row);
            indexField.value = nextIndex + 1;

            var firstInput = row.querySelector("input[type='text']");
            if (firstInput) {
                firstInput.focus();
            }
        }
    });

    table.addEventListener("click", function (event) {
        var button = event.target.closest(".remove-variant");
        if (!button) {
            return;
        }

        var rows = tbody.querySelectorAll("tr");
        if (rows.length <= 1) {
            window.alert("At least one variant is required.");
            return;
        }

        button.closest("tr").remove();
    });
})();
