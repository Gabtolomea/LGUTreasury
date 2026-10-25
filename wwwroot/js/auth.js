/* SHARED AUTH PAGE BEHAVIOUR — LOGIN & REGISTER */
(function () {
    'use strict';

    function showToast(msg, isError) {
        var t = document.getElementById('toast');
        if (!t) return;
        t.textContent = msg;
        t.className = 'toast' + (isError ? ' error' : '');
        t.classList.add('show');
        setTimeout(function () { t.classList.remove('show'); }, 2800);
    }

    function initPasswordToggles() {
        var buttons = document.querySelectorAll('.password-toggle');
        Array.prototype.forEach.call(buttons, function (btn) {
            var input = document.getElementById(btn.getAttribute('data-target'));
            if (!input) return;

            btn.addEventListener('click', function () {
                var showing = input.type === 'text';
                input.type = showing ? 'password' : 'text';

                var eyeOpen = btn.querySelector('.eye-open');
                var eyeOff = btn.querySelector('.eye-off');
                if (eyeOpen) eyeOpen.style.display = showing ? '' : 'none';
                if (eyeOff) eyeOff.style.display = showing ? 'none' : '';

                btn.setAttribute('aria-label', showing ? 'Show password' : 'Hide password');
            });
        });
    }

    function initPasswordMatch() {
        var pwd = document.getElementById('Password');
        var confirm = document.getElementById('ConfirmPassword');
        var hint = document.getElementById('passwordMatchHint');
        if (!pwd || !confirm || !hint) return;

        function check() {
            if (!confirm.value) {
                hint.textContent = '';
                hint.className = 'field-hint';
            } else if (pwd.value === confirm.value) {
                hint.textContent = 'Passwords match.';
                hint.className = 'field-hint ok';
            } else {
                hint.textContent = 'Passwords do not match.';
                hint.className = 'field-hint bad';
            }
        }

        pwd.addEventListener('input', check);
        confirm.addEventListener('input', check);
        check();
    }

    document.addEventListener('DOMContentLoaded', function () {
        initPasswordToggles();
        initPasswordMatch();
    });

    window.showToast = showToast;
})();
