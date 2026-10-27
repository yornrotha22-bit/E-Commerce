/* ==========================================================================
   KStore admin shell behaviour
   - desktop: remember collapsed sidebar
   - mobile: off-canvas sidebar with backdrop
   ========================================================================== */

(function () {
    'use strict';

    var shell = document.getElementById('adminShell');

    if (!shell) {
        return;
    }

    var storageKey = 'rothashop.admin.sidebar';

    try {
        if (window.localStorage.getItem(storageKey) === 'collapsed') {
            shell.classList.add('is-collapsed');
        }
    } catch (error) {
        /* storage can be blocked; the sidebar simply stays expanded */
    }

    function isMobile() {
        return window.innerWidth < 992;
    }

    document.querySelectorAll('[data-admin-toggle]').forEach(function (button) {
        button.addEventListener('click', function () {
            if (isMobile()) {
                shell.classList.toggle('is-open');
                return;
            }

            var collapsed = shell.classList.toggle('is-collapsed');

            try {
                window.localStorage.setItem(storageKey, collapsed ? 'collapsed' : 'expanded');
            } catch (error) {
                /* ignore */
            }
        });
    });

    document.querySelectorAll('[data-admin-close]').forEach(function (element) {
        element.addEventListener('click', function () {
            shell.classList.remove('is-open');
        });
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            shell.classList.remove('is-open');
        }
    });

    document.querySelectorAll('.admin-nav__link').forEach(function (link) {
        link.addEventListener('click', function () {
            if (isMobile()) {
                shell.classList.remove('is-open');
            }
        });
    });

    document.querySelectorAll('[data-page-size]').forEach(function (select) {
        select.addEventListener('change', function () {
            if (select.form) {
                select.form.submit();
            }
        });
    });
}());
