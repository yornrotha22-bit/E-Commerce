(function () {
    "use strict";

    var reduceMotion = Boolean(
        window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches);

    // ---------------------------------------------------------------------
    // Product gallery: crossfade the main image, preloading the next one so
    // the swap never shows a blank frame. Aspect ratio is fixed in CSS, so no
    // layout shift occurs while the image loads.
    // ---------------------------------------------------------------------
    var mainImage = document.getElementById("mainImage");
    var thumbs = document.querySelectorAll("[data-gallery-full]");
    var fadeToken = 0;

    function showImage(src) {
        if (!mainImage || !src || mainImage.getAttribute("src") === src) {
            return;
        }

        fadeToken += 1;
        var token = fadeToken;

        var apply = function () {
            if (token !== fadeToken) {
                return;
            }

            mainImage.src = src;

            if (reduceMotion) {
                mainImage.classList.remove("is-fading");
                return;
            }

            var clearFade = function () {
                mainImage.classList.remove("is-fading");
            };

            window.requestAnimationFrame(clearFade);
            // Browsers throttle rAF in background tabs; never stay faded.
            window.setTimeout(clearFade, 350);
        };

        if (reduceMotion) {
            apply();
            return;
        }

        mainImage.classList.add("is-fading");

        var preload = new Image();
        preload.onload = apply;
        preload.onerror = apply;
        preload.src = src;

        // Never leave the image faded if the loader stalls.
        window.setTimeout(apply, 600);
    }

    if (mainImage && thumbs.length > 0) {
        thumbs.forEach(function (thumb) {
            thumb.addEventListener("click", function () {
                var src = thumb.getAttribute("data-gallery-full");

                thumbs.forEach(function (item) {
                    item.classList.remove("active");
                });

                thumb.classList.add("active");
                showImage(src);
            });
        });
    }

    // ---------------------------------------------------------------------
    // Variant selector: price, compare-at price, stock and SKU come from the
    // database values rendered on the page (no totals are trusted from input).
    // ---------------------------------------------------------------------
    var variantInputs = document.querySelectorAll("input[name='variantId']");
    var priceEl = document.getElementById("variant-price");
    var compareEl = document.getElementById("variant-compare");
    var stockEl = document.getElementById("variant-stock");
    var skuEl = document.getElementById("variant-sku");
    var mainImageEl = document.getElementById("mainImage");
    var summaryEl = document.getElementById("variant-summary");
    var selectedVariantInput = document.getElementById("selectedVariantId");
    var quantityInput = document.getElementById("quantity");
    var addButton = document.getElementById("addToCartButton");

    function flash(element) {
        if (!element || reduceMotion) {
            return;
        }

        element.classList.remove("is-updating");
        // Restart the CSS transition even for rapid changes.
        void element.offsetWidth;
        element.classList.add("is-updating");

        window.setTimeout(function () {
            element.classList.remove("is-updating");
        }, 220);
    }

    function updateVariant(input) {
        if (!input) {
            return;
        }

        var price = parseFloat(input.getAttribute("data-price") || "0");
        var compare = input.getAttribute("data-compare");
        var stock = parseInt(input.getAttribute("data-stock") || "0", 10);

        if (priceEl) {
            priceEl.textContent = "$" + price.toFixed(2);
            flash(priceEl);
        }

        if (compareEl) {
            if (compare) {
                compareEl.textContent = "$" + parseFloat(compare).toFixed(2);
                compareEl.hidden = false;
            } else {
                compareEl.hidden = true;
                compareEl.textContent = "";
            }
        }

        if (stockEl) {
            if (stock > 0) {
                stockEl.textContent = stock + " in stock";
                stockEl.className = "stock-badge in-stock";
            } else {
                stockEl.textContent = "Out of stock";
                stockEl.className = "stock-badge out-stock";
            }

            flash(stockEl);
        }

        if (skuEl) {
            skuEl.textContent = input.getAttribute("data-sku") || "";
        }

        if (summaryEl) {
            summaryEl.textContent = input.getAttribute("data-label") || "";
        }

        var variantImage = input.getAttribute("data-image") || (mainImageEl && mainImageEl.getAttribute("data-default-image"));
        if (mainImageEl && variantImage) {
            mainImageEl.src = variantImage;
            document.querySelectorAll(".gallery-thumb-btn").forEach(function (thumb) {
                thumb.classList.remove("active");
            });
        }

        if (selectedVariantInput) {
            selectedVariantInput.value = input.value;
        }

        if (quantityInput) {
            var max = stock > 0 ? stock : 1;
            quantityInput.max = max;

            var current = parseInt(quantityInput.value || "1", 10);
            if (isNaN(current) || current < 1) {
                current = 1;
            }
            if (current > max) {
                current = max;
            }
            quantityInput.value = current;
        }

        if (addButton) {
            addButton.disabled = stock <= 0;
            var label = addButton.querySelector(".btn-label");
            if (label) {
                label.textContent = stock > 0 ? "Add to cart" : "Out of stock";
            }
        }
    }

    if (variantInputs.length > 0) {
        variantInputs.forEach(function (input) {
            input.addEventListener("change", function () {
                updateVariant(input);
            });
        });
    }

    // ---------------------------------------------------------------------
    // Wishlist toggle feedback on the product page.
    // ---------------------------------------------------------------------
    document.querySelectorAll(".wishlist-form").forEach(function (form) {
        form.addEventListener("submit", function () {
            var button = form.querySelector(".wishlist-toggle");

            if (button && !reduceMotion) {
                button.classList.add("is-pulsing");
            }
        });
    });
}());
