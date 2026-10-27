/* ==========================================================================
   KStore storefront behaviour
   - sticky header glass state
   - opt-in scroll reveal (IntersectionObserver, one-shot)
   - toast notifications for cart / wishlist feedback
   - submit loading state and image fallback
   Everything degrades to static content when JavaScript is unavailable.
   ========================================================================== */

(function () {
    'use strict';

    var reduceMotion = Boolean(
        window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    /* ----------------------------------------------------------------------
       Light / dark theme
       The inline <head> script sets the initial attribute before paint; this
       keeps every toggle in sync and remembers the choice.
       ---------------------------------------------------------------------- */

    var themeToggles = Array.prototype.slice.call(document.querySelectorAll('[data-theme-toggle]'));

    if (themeToggles.length > 0) {
        var root = document.documentElement;

        var applyTheme = function (theme) {
            var isDark = theme === 'dark';

            root.setAttribute('data-theme', isDark ? 'dark' : 'light');

            themeToggles.forEach(function (button) {
                var label = button.getAttribute(isDark ? 'data-label-light' : 'data-label-dark');

                button.setAttribute('aria-pressed', isDark ? 'true' : 'false');
                button.setAttribute('aria-label', label);
                button.setAttribute('title', label);
            });

            var meta = document.querySelector('meta[name="theme-color"]');

            if (meta) {
                meta.setAttribute('content', isDark ? '#14161a' : '#faf9f6');
            }
        };

        applyTheme(root.getAttribute('data-theme') === 'dark' ? 'dark' : 'light');

        themeToggles.forEach(function (button) {
            button.addEventListener('click', function () {
                var next = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';

                try {
                    window.localStorage.setItem('rs-theme', next);
                } catch (error) {
                    // Storage can be blocked; the theme still changes for this page.
                }

                applyTheme(next);
            });
        });
    }

    /* ----------------------------------------------------------------------
       Sticky header
       ---------------------------------------------------------------------- */

    var header = document.getElementById('rsHeader');

    if (header) {
        var onScroll = function () {
            header.classList.toggle('is-scrolled', window.scrollY > 4);
        };

        onScroll();
        window.addEventListener('scroll', onScroll, { passive: true });
    }

    /* ----------------------------------------------------------------------
       Buttons with a loading state (forms marked data-loading)
       ---------------------------------------------------------------------- */

    document.querySelectorAll('form[data-loading]').forEach(function (form) {
        form.addEventListener('submit', function () {
            // Blocked submits (native or jQuery validation) must not spin forever.
            if (form.noValidate !== true && !form.checkValidity()) {
                return;
            }

            var button = form.querySelector('button[type="submit"]');

            // Submit buttons may live outside the form (form="checkoutForm").
            if (!button && form.id) {
                button = document.querySelector('[form="' + form.id + '"]');
            }

            if (!button || button.classList.contains('is-loading')) {
                return;
            }

            button.classList.add('is-loading');
            button.setAttribute('aria-busy', 'true');

            // Safety net for script-side validation that cancels the submit.
            // If the browser navigates away this timer dies with the page.
            window.setTimeout(function () {
                button.classList.remove('is-loading');
                button.removeAttribute('aria-busy');
            }, 8000);
        });
    });

    /* ----------------------------------------------------------------------
       Wishlist heart feedback (the form navigates immediately after)
       ---------------------------------------------------------------------- */

    document.querySelectorAll('form.pcard__wish').forEach(function (form) {
        form.addEventListener('submit', function () {
            var button = form.querySelector('.pcard__wish-btn');

            if (button && !reduceMotion) {
                button.classList.add('is-pulsing');
            }
        });
    });

    /* ----------------------------------------------------------------------
       Toasts (server-rendered from TempData)
       ---------------------------------------------------------------------- */

    document.querySelectorAll('[data-rs-toast]').forEach(function (element) {
        if (!window.bootstrap || !window.bootstrap.Toast) {
            return;
        }

        new window.bootstrap.Toast(element, {
            animation: !reduceMotion,
            autohide: true,
            delay: 4200
        }).show();
    });

    /* ----------------------------------------------------------------------
       Image fallback
       ---------------------------------------------------------------------- */

    document.querySelectorAll('img[data-fallback]').forEach(function (image) {
        image.addEventListener('error', function () {
            var fallback = image.getAttribute('data-fallback');

            if (!fallback) {
                return;
            }

            image.removeAttribute('data-fallback');
            image.src = fallback;
        }, { once: true });
    });

    /* ----------------------------------------------------------------------
       Auto-submit for sort/filter selects
       ---------------------------------------------------------------------- */

    document.querySelectorAll('[data-autosubmit]').forEach(function (control) {
        control.addEventListener('change', function () {
            if (control.form) {
                control.form.submit();
            }
        });
    });

    /* ----------------------------------------------------------------------
       Toast helper (used by the AJAX cart; server toasts render from TempData)
       ---------------------------------------------------------------------- */

    var toastCheckIcon =
        '<svg width="16" height="16" viewBox="0 0 24 24"><circle cx="12" cy="12" r="8.5" fill="none" ' +
        'stroke="currentColor" stroke-width="1.6"/><path d="m8.4 12.2 2.4 2.4 4.8-4.9" fill="none" ' +
        'stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>';

    var toastErrorIcon =
        '<svg width="16" height="16" viewBox="0 0 24 24"><circle cx="12" cy="12" r="8.5" fill="none" ' +
        'stroke="#f08a76" stroke-width="1.6"/><path d="M12 8v5M12 16.2v.1" fill="none" stroke="#f08a76" ' +
        'stroke-width="1.8" stroke-linecap="round"/></svg>';

    function showToast(message, isError) {
        if (!message || !window.bootstrap || !window.bootstrap.Toast) {
            return;
        }

        // The label is rendered by the layout in the active language.
        var dismissLabel = document.body.getAttribute('data-dismiss-label') || 'Close';

        var region = document.querySelector('.rs-toast-region');

        if (!region) {
            region = document.createElement('div');
            region.className = 'rs-toast-region';
            region.setAttribute('aria-live', 'polite');
            region.setAttribute('aria-atomic', 'true');
            document.body.appendChild(region);
        }

        var toast = document.createElement('div');
        toast.className = 'toast rs-toast';
        toast.setAttribute('role', 'status');
        toast.innerHTML =
            '<div class="d-flex align-items-start"><div class="toast-body">' +
            '<span class="rs-toast__icon" aria-hidden="true">' + (isError ? toastErrorIcon : toastCheckIcon) +
            '</span><span data-toast-text></span></div>' +
            '<button type="button" class="btn-close" data-bs-dismiss="toast" ' +
            'aria-label="' + dismissLabel + '"></button></div>';

        toast.querySelector('[data-toast-text]').textContent = message;

        // Keep one message on screen at a time so repeated clicks stay calm.
        region.querySelectorAll('.rs-toast').forEach(function (existing) {
            existing.remove();
        });

        region.appendChild(toast);
        toast.addEventListener('hidden.bs.toast', function () {
            toast.remove();
        });

        new window.bootstrap.Toast(toast, {
            animation: !reduceMotion,
            autohide: true,
            delay: 4200
        }).show();
    }

    /* ----------------------------------------------------------------------
       Slide-out cart
       The drawer body is rendered by /Cart/Drawer so prices, stock flags and
       totals always come from the server. Without JavaScript the header cart
       stays a plain link to /Cart and add-to-cart posts normally.
       ---------------------------------------------------------------------- */

    var drawer = document.getElementById('cartDrawer');
    var drawerBody = document.getElementById('cartDrawerBody');
    var drawerUrl = drawer ? drawer.getAttribute('data-cart-drawer-url') : null;
    var drawerLoaded = false;
    var drawerLoading = false;

    function updateCartBadges(count) {
        document.querySelectorAll('[data-cart-badge]').forEach(function (link) {
            var badge = link.querySelector('.rs-cart__badge');

            if (badge) {
                var next = String(count);

                if (badge.textContent !== next && !reduceMotion) {
                    badge.classList.remove('is-bump');
                    void badge.offsetWidth; // restart the keyframes
                    badge.classList.add('is-bump');
                    window.setTimeout(function () {
                        badge.classList.remove('is-bump');
                    }, 400);
                }

                badge.textContent = next;
                badge.classList.toggle('is-empty', count === 0);
            }

            link.classList.toggle('has-items', count > 0);

            var labelOne = link.getAttribute('data-cart-label-one') || 'Shopping cart, {0} item';
            var labelMany = link.getAttribute('data-cart-label-many') || 'Shopping cart, {0} items';

            link.setAttribute('aria-label', (count === 1 ? labelOne : labelMany).replace('{0}', String(count)));
        });
    }

    function openCartDrawer() {
        if (!drawer || !window.bootstrap || !window.bootstrap.Offcanvas) {
            return false;
        }

        window.bootstrap.Offcanvas.getOrCreateInstance(drawer).show();
        return true;
    }

    function loadCartDrawer(force) {
        if (!drawerBody || !drawerUrl) {
            return Promise.resolve(false);
        }

        if (drawerLoading || (drawerLoaded && !force)) {
            return Promise.resolve(true);
        }

        drawerLoading = true;

        var finish = function (ok) {
            drawerLoading = false;
            return ok;
        };

        return window.fetch(drawerUrl, {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('Cart drawer request failed');
            }

            return response.text();
        }).then(function (html) {
            drawerBody.innerHTML = html;

            var content = drawerBody.querySelector('[data-cart-content]');

            if (content) {
                updateCartBadges(Number(content.getAttribute('data-cart-count')) || 0);
            }

            drawerLoaded = true;
            return finish(true);
        }).catch(function () {
            // Keep whatever is already on screen; the cart page still works.
            return finish(false);
        });
    }

    document.querySelectorAll('[data-cart-toggle]').forEach(function (link) {
        link.addEventListener('click', function (event) {
            if (!openCartDrawer()) {
                return;
            }

            event.preventDefault();
            loadCartDrawer(false);
        });
    });

    if (drawer) {
        drawer.addEventListener('show.bs.offcanvas', function () {
            loadCartDrawer(false);
        });
    }

    // Remove a line straight from the drawer.
    if (drawerBody) {
        drawerBody.addEventListener('submit', function (event) {
            var form = event.target;

            if (!form.matches || !form.matches('form[data-drawer-remove]')) {
                return;
            }

            if (!window.fetch || !window.FormData) {
                return;
            }

            event.preventDefault();

            window.fetch(form.action, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: new FormData(form)
            }).then(function (response) {
                return response.json();
            }).then(function (data) {
                updateCartBadges(Number(data.count) || 0);
                showToast(data.message, !data.success);
                return loadCartDrawer(true);
            }).catch(function () {
                form.submit();
            });
        });
    }

    // Add to cart without leaving the page, then reveal the drawer.
    document.querySelectorAll('form[data-cart-form]').forEach(function (form) {
        form.addEventListener('submit', function (event) {
            if (form.getAttribute('data-cart-form') === 'native') {
                return;
            }

            if (!window.fetch || !window.FormData) {
                return;
            }

            event.preventDefault();

            var button = form.querySelector('button[type="submit"]');

            if (button) {
                button.classList.add('is-loading');
                button.setAttribute('aria-busy', 'true');
            }

            var resetButton = function () {
                if (button) {
                    button.classList.remove('is-loading');
                    button.removeAttribute('aria-busy');
                }
            };

            window.fetch(form.action, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: new FormData(form)
            }).then(function (response) {
                if (!response.ok) {
                    throw new Error('Add to cart failed');
                }

                return response.json();
            }).then(function (data) {
                resetButton();
                updateCartBadges(Number(data.count) || 0);
                showToast(data.message, !data.success);

                if (!data.success) {
                    return null;
                }

                openCartDrawer();
                return loadCartDrawer(true);
            }).catch(function () {
                // Fall back to a normal POST so the item is never lost.
                resetButton();
                form.setAttribute('data-cart-form', 'native');
                form.submit();
            });
        });
    });

    /* ----------------------------------------------------------------------
       Typewriter heading (home hero)
       Types the finished heading one grapheme at a time so Khmer clusters are
       never split. Reduced motion, missing JS and empty text all fall back to
       the full line that is already rendered.
       ---------------------------------------------------------------------- */

    document.querySelectorAll('[data-typewriter]').forEach(function (target) {
        if (reduceMotion) {
            return;
        }

        var live = target.querySelector('.rs-type__text');
        var fullText = live ? live.textContent : '';

        if (!live || !fullText.trim()) {
            return;
        }

        var segmenter = null;

        if (window.Intl && typeof window.Intl.Segmenter === 'function') {
            try {
                segmenter = new window.Intl.Segmenter(
                    document.documentElement.lang || undefined,
                    { granularity: 'grapheme' });
            } catch (error) {
                segmenter = null;
            }
        }

        var characters = segmenter
            ? Array.from(segmenter.segment(fullText), function (part) {
                return part.segment;
            })
            : Array.from(fullText);

        var index = 0;

        target.classList.add('is-typing');
        live.textContent = '';

        var typeNext = function () {
            if (index >= characters.length) {
                window.setTimeout(function () {
                    target.classList.add('is-done');
                }, 1800);
                return;
            }

            var character = characters[index];
            live.textContent += character;
            index += 1;

            var delay = 46;

            if (/[.,!?…]/.test(character)) {
                delay = 420;
            } else if (/\s/.test(character)) {
                delay = 90;
            }

            window.setTimeout(typeNext, delay);
        };

        window.setTimeout(typeNext, 250);
    });

    /* ----------------------------------------------------------------------
       Reveal on scroll
       The initial hidden state only exists when <html> has .motion-ready,
       which the inline head script sets when JS runs and motion is allowed.
       ---------------------------------------------------------------------- */

    var revealItems = Array.prototype.slice.call(document.querySelectorAll('[data-reveal]'));

    if (revealItems.length === 0) {
        return;
    }

    var revealAll = function () {
        revealItems.forEach(function (element) {
            element.classList.add('is-visible');
        });
    };

    if (reduceMotion || !('IntersectionObserver' in window)) {
        revealAll();
        return;
    }

    // Stagger siblings inside a reveal group (product grids, brand rows, ...).
    var counters = new Map();

    revealItems.forEach(function (element) {
        var group = element.closest('[data-reveal-group]') || element.parentElement;
        var index = counters.get(group) || 0;

        element.style.setProperty('--reveal-index', String(Math.min(index, 8)));
        counters.set(group, index + 1);
    });

    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (!entry.isIntersecting) {
                return;
            }

            entry.target.classList.add('is-visible');
            observer.unobserve(entry.target);
        });
    }, {
        rootMargin: '0px 0px -6% 0px',
        threshold: 0.06
    });

    revealItems.forEach(function (element) {
        observer.observe(element);
    });

    // Safety net: if the observer never fires (unusual embedding, broken scroll
    // container, layout change), reveal everything so content can never stay
    // invisible. Normal pages finish revealing long before this timer.
    window.setTimeout(revealAll, 5000);
}());
