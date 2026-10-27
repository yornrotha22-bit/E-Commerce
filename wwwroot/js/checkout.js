(function () {
    "use strict";
    var addressSelect = document.querySelector("[data-checkout-address]");
    if (!addressSelect) {
        return;
    }
    function setField(name, value) {
        var field = document.querySelector("[name='" + name + "']");
        if (field) {
            field.value = value || "";
        }
    }

    addressSelect.addEventListener("change", function () {
        if (!addressSelect.value) {
            return;
        }

        var option = addressSelect.options[addressSelect.selectedIndex];

        setField("FullName", option.getAttribute("data-fullname"));
        setField("PhoneNumber", option.getAttribute("data-phone"));
        setField("AddressLine1", option.getAttribute("data-line1"));
        setField("AddressLine2", option.getAttribute("data-line2"));
        setField("City", option.getAttribute("data-city"));
        setField("State", option.getAttribute("data-state"));
        setField("PostalCode", option.getAttribute("data-postal"));
        setField("Country", option.getAttribute("data-country"));
    });
})();
