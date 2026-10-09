// ─── RECORD PAYMENT PAGE ────────────────────────

// ─── TRANSACTION ID (encoded from payor full name) ───
// The Transaction ID field is hidden from the user; this function derives its
// value from the payor's full name so the encoded data still travels with the form.
function encodePayorFullName(fullName) {
    const cleaned = (fullName || '').trim().replace(/\s+/g, ' ');
    if (!cleaned) return '';

    const initials = cleaned
        .split(' ')
        .map(function(word) { return word.charAt(0); })
        .join('')
        .toUpperCase();

    let hash = 0;
    for (let i = 0; i < cleaned.length; i++) {
        hash = ((hash * 31) + cleaned.charCodeAt(i)) >>> 0;
    }

    return 'TXN-' + initials + '-' + hash.toString(16).toUpperCase().padStart(4, '0');
}

function updateTransactionId(value) {
    const hidden = document.getElementById('pay-txnid');
    if (hidden) hidden.value = encodePayorFullName(value);
}

// ─── COLLECTION TYPE MODAL ───────────────────────
function openRevTypeModal() {
    document.getElementById('rt-search').value = '';
    filterRevTypes();
    document.getElementById('modal-revtype-picker').classList.add('open');
}

function closeRevTypeModal() {
    document.getElementById('modal-revtype-picker').classList.remove('open');
}

function filterRevTypes() {
    const q = document.getElementById('rt-search').value.toLowerCase();
    document.querySelectorAll('.rt-option').forEach(function(opt) {
        opt.style.display = opt.dataset.name.toLowerCase().includes(q) ? '' : 'none';
    });
}

function selectRevType(el) {
    const name      = el.dataset.name;
    const typeID    = el.dataset.id;
    const baseRate  = parseFloat(el.dataset.baserate) || 0;
    const surcharge = parseFloat(el.dataset.surcharge) || 0;
    const interest  = parseFloat(el.dataset.interest) || 0;

    // Fill display and hidden fields
    document.getElementById('coltype-display').value = name;
    document.getElementById('pay-typeid').value      = typeID;

    // Compute breakdown
    const surchargeAmt = baseRate * (surcharge / 100);
    const interestAmt  = baseRate * (interest / 100);
    const total        = baseRate + surchargeAmt + interestAmt;

    document.getElementById('pay-base').value      = baseRate.toFixed(2);
    document.getElementById('pay-surcharge').value = surchargeAmt.toFixed(2);
    document.getElementById('pay-interest').value  = interestAmt.toFixed(2);
    document.getElementById('pay-total').value     = total.toFixed(2);

    document.getElementById('bd-base').textContent      = '₱ ' + baseRate.toFixed(2);
    document.getElementById('bd-surcharge').textContent = '₱ ' + surchargeAmt.toFixed(2);
    document.getElementById('bd-interest').textContent  = '₱ ' + interestAmt.toFixed(2);
    document.getElementById('bd-total').textContent     = '₱ ' + total.toFixed(2);

    document.getElementById('payment-breakdown').classList.add('show');

    closeRevTypeModal();
}

// ─── INIT ────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function() {
    const dateInput = document.getElementById('date-issued');
    if (dateInput) dateInput.max = new Date().toISOString().split('T')[0];

    const nameInput = document.getElementById('pay-fullname');
    if (nameInput) updateTransactionId(nameInput.value);

    const revModal = document.getElementById('modal-revtype-picker');
    if (revModal) revModal.addEventListener('click', function(e) {
        if (e.target === this) closeRevTypeModal();
    });
});
