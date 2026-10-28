/* CONTACT NUMBER INPUT — numbers only, letters are never accepted */
(function () {
    'use strict';

    var NOT_ALLOWED = /[^0-9+\-\s()]/g;
    var NOT_DIGIT = /\D/g;

    /* Removes every character that is not a digit or a common separator. */
    function sanitize(value) {
        return (value || '').replace(NOT_ALLOWED, '');
    }

    /* Digits only — used before sending a contact number to the server. */
    function digits(value) {
        return (value || '').replace(NOT_DIGIT, '');
    }

    function init() {
        var inputs = document.querySelectorAll('input[data-contact-number]');

        Array.prototype.forEach.call(inputs, function (input) {
            input.addEventListener('input', function () {
                var cleaned = sanitize(input.value);
                if (cleaned === input.value) return;

                var pos = input.selectionStart;
                input.value = cleaned;
                if (pos !== null && typeof input.setSelectionRange === 'function') {
                    input.setSelectionRange(pos, pos);
                }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', init);

    window.sanitizeContactNumber = sanitize;
    window.contactNumberDigits = digits;
})();
