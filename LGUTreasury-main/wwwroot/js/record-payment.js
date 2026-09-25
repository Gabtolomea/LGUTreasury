// ─── RECORD PAYMENT PAGE ────────────────────────

// ─── FETCH NEXT TRANSACTION ID ───────────────────
async function fetchNextTransactionID() {
    try {
        const res = await fetch('/Record/GetNextTransactionID');
        const data = await res.json();
        const txnInput = document.getElementById('pay-txnid');
        if (txnInput) txnInput.value = data.transactionID;
    } catch (err) {
        console.error('Error fetching transaction ID:', err);
    }
}

// ─── PAYOR SEARCH (real DB) ──────────────────────
async function searchPayors(query) {
    const dd = document.getElementById('payor-dropdown');
    if (!query.trim()) { dd.innerHTML = ''; dd.classList.remove('open'); return; }

    try {
        const res = await fetch('/Record/SearchPayee?query=' + encodeURIComponent(query));
        const results = await res.json();

        if (!results.length) {
            dd.innerHTML = '<div class="payor-dd-empty">No payors found.</div>';
            dd.classList.add('open');
            return;
        }

        dd.innerHTML = results.map(function(p) {
            const full = [p.firstname, p.middlename, p.lastname, p.suffix].filter(Boolean).join(' ');
            const meta = [p.contactNumber, p.residenceAddress].filter(Boolean).join(' · ');
            const initials = ((p.firstname || ' ')[0] + (p.lastname || ' ')[0]).toUpperCase();
            const encoded = encodeURIComponent(JSON.stringify(p));
            return '<div class="payor-dd-item" onclick="selectPayorEncoded(\'' + encoded + '\')">'
                + '<div class="payor-dd-avatar">' + initials + '</div>'
                + '<div><div class="payor-dd-name">' + full + '</div>'
                + '<div class="payor-dd-meta">' + (meta || '—') + '</div></div>'
                + '</div>';
        }).join('');
        dd.classList.add('open');
    } catch (err) {
        console.error('Search error:', err);
    }
}

function selectPayorEncoded(encoded) {
    selectPayor(JSON.parse(decodeURIComponent(encoded)));
}

function selectPayor(p) {
    document.getElementById('pay-payeeid').value = p.payeeID || '';

    const full = [p.firstname, p.middlename, p.lastname, p.suffix].filter(Boolean).join(' ');
    document.getElementById('pay-fullname').value = full;

    const meta = [p.contactNumber, p.residenceAddress].filter(Boolean).join(' · ');
    const initials = ((p.firstname || ' ')[0] + (p.lastname || ' ')[0]).toUpperCase();

    document.getElementById('sel-avatar').textContent = initials;
    document.getElementById('sel-name').textContent   = full;
    document.getElementById('sel-meta').textContent   = meta || '—';
    document.getElementById('selected-payor-card').classList.remove('hidden');

    document.getElementById('payor-search').value = '';
    const dd = document.getElementById('payor-dropdown');
    dd.innerHTML = ''; dd.classList.remove('open');

    fetchNextTransactionID();
}

function clearPayor() {
    document.getElementById('pay-payeeid').value = '';
    document.getElementById('pay-fullname').value = '';
    document.getElementById('selected-payor-card').classList.add('hidden');
    document.getElementById('pay-txnid').value = '';
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

// ─── CLOSE DROPDOWN ON OUTSIDE CLICK ────────────
document.addEventListener('click', function(e) {
    const wrap = document.querySelector('.payor-search-wrap');
    if (wrap && !wrap.contains(e.target)) {
        const dd = document.getElementById('payor-dropdown');
        if (dd) { dd.innerHTML = ''; dd.classList.remove('open'); }
    }
});

// ─── INIT ────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function() {
    const dateInput = document.getElementById('date-issued');
    if (dateInput) dateInput.max = new Date().toISOString().split('T')[0];

    const revModal = document.getElementById('modal-revtype-picker');
    if (revModal) revModal.addEventListener('click', function(e) {
        if (e.target === this) closeRevTypeModal();
    });
});
