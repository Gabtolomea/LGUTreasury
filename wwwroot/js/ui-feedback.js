/* ============================================================
   Shared feedback UI: toasts, alerts and confirmation dialogs
   Exposes window.UI plus backwards-compatible helpers.
   ============================================================ */
(function () {
    'use strict';

    var ICONS = {
        success: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20 6 9 17l-5-5"/></svg>',
        error: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M15 9l-6 6M9 9l6 6"/></svg>',
        warning: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z"/><path d="M12 9v4M12 17h.01"/></svg>',
        info: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8h.01"/></svg>',
        close: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>'
    };

    var TYPES = { success: true, error: true, warning: true, info: true };

    var TOAST_TITLES = {
        success: 'Success',
        error: 'Something went wrong',
        warning: 'Warning',
        info: 'Information'
    };

    var DIALOG_TITLES = {
        success: 'Success',
        error: 'Something went wrong',
        warning: 'Warning',
        info: 'Notice'
    };

    var dialogId = 0;

    function normalizeType(type) {
        if (type === true) return 'error';
        if (type === false || type === null || type === undefined || type === '') return 'success';
        var value = String(type).toLowerCase();
        if (value === 'danger' || value === 'fail' || value === 'failed' || value === 'alert') return 'error';
        if (value === 'warn') return 'warning';
        return TYPES[value] ? value : 'info';
    }

    function ensureToastRoot() {
        var root = document.getElementById('ui-toast-root');
        if (root) return root;

        root = document.createElement('div');
        root.id = 'ui-toast-root';
        root.className = 'ui-toast-root';
        root.setAttribute('role', 'region');
        root.setAttribute('aria-live', 'polite');
        root.setAttribute('aria-label', 'Notifications');
        document.body.appendChild(root);
        return root;
    }

    /* ── Toasts ───────────────────────────────────────────── */
    function toast(message, type, options) {
        if (type && typeof type === 'object') {
            options = type;
            type = options.type;
        }

        options = options || {};
        type = normalizeType(type);

        var duration = options.duration;
        if (duration === undefined || duration === null) {
            duration = type === 'error' ? 6500 : type === 'warning' ? 5500 : 4000;
        }

        var root = ensureToastRoot();
        var el = document.createElement('div');
        el.className = 'ui-toast ui-toast--' + type;
        el.setAttribute('role', type === 'error' ? 'alert' : 'status');
        el.setAttribute('aria-live', type === 'error' ? 'assertive' : 'polite');

        el.innerHTML =
            '<span class="ui-toast__icon">' + ICONS[type] + '</span>' +
            '<div class="ui-toast__body">' +
                '<p class="ui-toast__title"></p>' +
                '<p class="ui-toast__message"></p>' +
            '</div>' +
            '<button type="button" class="ui-toast__close" aria-label="Dismiss notification">' + ICONS.close + '</button>' +
            (duration > 0 ? '<span class="ui-toast__timer" style="--ui-duration:' + duration + 'ms"></span>' : '');

        var titleEl = el.querySelector('.ui-toast__title');
        var messageEl = el.querySelector('.ui-toast__message');
        titleEl.textContent = options.title || TOAST_TITLES[type];
        messageEl.textContent = message === null || message === undefined ? '' : String(message);

        if (!messageEl.textContent) {
            messageEl.style.display = 'none';
        }

        var timer = null;
        var dismissed = false;

        function dismiss() {
            if (dismissed) return;
            dismissed = true;
            if (timer) clearTimeout(timer);
            el.classList.add('is-hiding');
            window.setTimeout(function () {
                if (el.parentNode) el.parentNode.removeChild(el);
            }, 220);
        }

        el.querySelector('.ui-toast__close').addEventListener('click', dismiss);

        if (duration > 0) {
            timer = window.setTimeout(dismiss, duration);
        }

        el.addEventListener('mouseenter', function () {
            if (timer) {
                clearTimeout(timer);
                timer = null;
            }
            var bar = el.querySelector('.ui-toast__timer');
            if (bar) bar.style.animationPlayState = 'paused';
        });

        el.addEventListener('mouseleave', function () {
            if (dismissed || duration <= 0) return;
            var bar = el.querySelector('.ui-toast__timer');
            if (bar) bar.style.animationPlayState = 'running';
            timer = window.setTimeout(dismiss, 1200);
        });

        while (root.children.length >= 4) {
            root.removeChild(root.firstElementChild);
        }

        root.appendChild(el);
        return el;
    }

    /* ── Dialogs ──────────────────────────────────────────── */
    function trapFocus(event, container) {
        if (event.key !== 'Tab') return;

        var focusable = Array.prototype.filter.call(
            container.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'),
            function (node) {
                return !node.disabled && node.offsetParent !== null;
            }
        );

        if (!focusable.length) return;

        var first = focusable[0];
        var last = focusable[focusable.length - 1];

        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    }

    function createDialog(options) {
        options = options || {};

        var showCancel = !!options.showCancel;
        var type = normalizeType(options.type || (showCancel ? 'warning' : 'info'));
        var danger = !!options.danger;
        var titleText = options.title || (showCancel ? 'Please confirm' : DIALOG_TITLES[type]);
        var messageText = options.message === null || options.message === undefined ? '' : String(options.message);
        var confirmText = options.confirmText || (showCancel ? 'Confirm' : 'OK');
        var cancelText = options.cancelText || 'Cancel';
        var previousFocus = document.activeElement;
        var uid = ++dialogId;

        var overlay = document.createElement('div');
        overlay.className = 'ui-dialog-overlay';

        overlay.innerHTML =
            '<div class="ui-dialog ui-dialog--' + type + '" role="alertdialog" aria-modal="true" ' +
                 'aria-labelledby="ui-dialog-title-' + uid + '" aria-describedby="ui-dialog-message-' + uid + '">' +
                '<div class="ui-dialog__accent"></div>' +
                '<div class="ui-dialog__body">' +
                    '<div class="ui-dialog__icon" aria-hidden="true">' + ICONS[type] + '</div>' +
                    '<h2 class="ui-dialog__title" id="ui-dialog-title-' + uid + '"></h2>' +
                    '<p class="ui-dialog__message" id="ui-dialog-message-' + uid + '"></p>' +
                '</div>' +
                '<div class="ui-dialog__actions">' +
                    (showCancel
                        ? '<button type="button" class="ui-dialog__btn ui-dialog__btn--ghost" data-ui-cancel></button>'
                        : '') +
                    '<button type="button" class="ui-dialog__btn ' +
                        (danger ? 'ui-dialog__btn--danger' : 'ui-dialog__btn--primary') +
                        '" data-ui-confirm></button>' +
                '</div>' +
            '</div>';

        overlay.querySelector('.ui-dialog__title').textContent = titleText;

        var messageEl = overlay.querySelector('.ui-dialog__message');
        messageEl.textContent = messageText;
        if (!messageText) messageEl.style.display = 'none';

        var confirmBtn = overlay.querySelector('[data-ui-confirm]');
        confirmBtn.textContent = confirmText;

        var cancelBtn = overlay.querySelector('[data-ui-cancel]');
        if (cancelBtn) cancelBtn.textContent = cancelText;

        var settled = false;
        var resolvePromise;

        var promise = new Promise(function (resolve) {
            resolvePromise = resolve;
        });

        function close(result) {
            if (settled) return;
            settled = true;

            overlay.classList.remove('is-open');
            document.removeEventListener('keydown', onKeyDown, true);

            window.setTimeout(function () {
                if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
                if (previousFocus && typeof previousFocus.focus === 'function') {
                    previousFocus.focus();
                }
            }, 220);

            resolvePromise(result);
        }

        function onKeyDown(event) {
            if (event.key === 'Escape') {
                event.preventDefault();
                close(showCancel ? false : true);
                return;
            }
            trapFocus(event, overlay.querySelector('.ui-dialog'));
        }

        confirmBtn.addEventListener('click', function () {
            close(true);
        });

        if (cancelBtn) {
            cancelBtn.addEventListener('click', function () {
                close(false);
            });
        }

        overlay.addEventListener('click', function (event) {
            if (event.target === overlay) {
                close(showCancel ? false : true);
            }
        });

        document.body.appendChild(overlay);
        document.addEventListener('keydown', onKeyDown, true);

        window.requestAnimationFrame(function () {
            overlay.classList.add('is-open');
        });

        window.setTimeout(function () {
            confirmBtn.focus();
        }, 40);

        return promise;
    }

    function alertBox(message, options) {
        options = options || {};
        options.message = message;
        options.showCancel = false;
        if (!options.title) options.title = DIALOG_TITLES[normalizeType(options.type || 'info')];
        return createDialog(options);
    }

    function confirmBox(message, options) {
        options = options || {};
        options.message = message;
        options.showCancel = true;
        if (!options.title) options.title = 'Please confirm';
        return createDialog(options);
    }

    /* ── Legacy modal helpers ─────────────────────────────── */
    function openModal(id) {
        var el = typeof id === 'string' ? document.getElementById(id) : id;
        if (!el) return;
        el.classList.add('open');
        el.setAttribute('aria-hidden', 'false');

        var focusable = el.querySelector('input:not([type="hidden"]), select, textarea, button');
        if (focusable) {
            window.setTimeout(function () {
                focusable.focus();
            }, 40);
        }
    }

    function closeModal(id) {
        var el = typeof id === 'string' ? document.getElementById(id) : id;
        if (!el) return;
        el.classList.remove('open');
        el.setAttribute('aria-hidden', 'true');
    }

    /* ── Delegated helpers ────────────────────────────────── */
    document.addEventListener('click', function (event) {
        var overlay = event.target.closest ? event.target.closest('.modal-overlay') : null;
        if (!overlay || event.target !== overlay) return;
        if (overlay.getAttribute('data-ui-static') === 'true') return;
        overlay.classList.remove('open');
        overlay.setAttribute('aria-hidden', 'true');
    });

    document.addEventListener('click', function (event) {
        var link = event.target.closest ? event.target.closest('a[data-ui-confirm]') : null;
        if (!link) return;
        if (event.defaultPrevented || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        if (link.target && link.target !== '_self') return;

        event.preventDefault();

        confirmBox(link.getAttribute('data-ui-confirm'), {
            title: link.getAttribute('data-ui-confirm-title') || 'Please confirm',
            type: link.getAttribute('data-ui-confirm-type') || 'warning',
            confirmText: link.getAttribute('data-ui-confirm-text') || 'Continue',
            cancelText: link.getAttribute('data-ui-confirm-cancel') || 'Cancel',
            danger: link.getAttribute('data-ui-confirm-danger') === 'true'
        }).then(function (ok) {
            if (ok) window.location.href = link.href;
        });
    });

    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!form || form.nodeName !== 'FORM') return;
        if (form.getAttribute('data-ui-confirm') === null) return;

        if (form.getAttribute('data-ui-confirmed') === 'true') {
            form.removeAttribute('data-ui-confirmed');
            return;
        }

        event.preventDefault();

        confirmBox(form.getAttribute('data-ui-confirm'), {
            title: form.getAttribute('data-ui-confirm-title') || 'Please confirm',
            type: form.getAttribute('data-ui-confirm-type') || 'warning',
            confirmText: form.getAttribute('data-ui-confirm-text') || 'Continue',
            cancelText: form.getAttribute('data-ui-confirm-cancel') || 'Cancel',
            danger: form.getAttribute('data-ui-confirm-danger') === 'true'
        }).then(function (ok) {
            if (!ok) return;
            form.setAttribute('data-ui-confirmed', 'true');
            if (typeof form.requestSubmit === 'function') form.requestSubmit();
            else form.submit();
        });
    }, true);

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape') return;
        if (document.querySelector('.ui-dialog-overlay.is-open')) return;

        var open = document.querySelector('.modal-overlay.open');
        if (open) {
            open.classList.remove('open');
            open.setAttribute('aria-hidden', 'true');
        }
    }, true);

    /* ── Public API ───────────────────────────────────────── */
    window.UI = {
        toast: toast,
        alert: alertBox,
        confirm: confirmBox,
        dialog: createDialog,
        open: openModal,
        close: closeModal
    };

    window.showToast = function (message, isError) {
        var type = 'success';
        if (typeof isError === 'string') type = isError;
        else if (isError === true) type = 'error';
        return toast(message, type);
    };

    window.openModal = openModal;
    window.closeModal = closeModal;
    window.uiToast = toast;
    window.uiAlert = alertBox;
    window.uiConfirm = confirmBox;
})();
