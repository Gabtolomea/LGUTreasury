// ─── REVENUE TYPE PAGE ──────────────────────────

let editingTypeID = null;

// ─── SEARCH ──────────────────────────────────────
function filterRevTypes(query) {
    const q = query.toLowerCase();
    document.querySelectorAll('#revtype-tbody tr').forEach(function(tr) {
        tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none';
    });
}

// ─── LIVE COMPUTATION ────────────────────────────
function computePreview() {
    const base      = parseFloat(document.getElementById('rt-rate').value) || 0;
    const surcharge = parseFloat(document.getElementById('rt-surcharge').value) || 0;
    const interest  = parseFloat(document.getElementById('rt-interest').value) || 0;

    const surchargeAmt = base * (surcharge / 100);
    const interestAmt  = base * (interest / 100);
    const total        = base + surchargeAmt + interestAmt;

    document.getElementById('preview-base').textContent      = '₱ ' + base.toFixed(2);
    document.getElementById('preview-surcharge').textContent = '₱ ' + surchargeAmt.toFixed(2);
    document.getElementById('preview-interest').textContent  = '₱ ' + interestAmt.toFixed(2);
    document.getElementById('preview-total').textContent     = '₱ ' + total.toFixed(2);
}

// ─── EDIT ─────────────────────────────────────────
function editRevType(btn) {
    const row = btn.closest('tr');
    editingTypeID = row.dataset.typeid;
    document.getElementById('modal-revtype-title').textContent = 'Edit Revenue Type';
    document.getElementById('rt-name').value      = row.dataset.name;
    document.getElementById('rt-category').value  = row.dataset.categoryid;
    document.getElementById('rt-rate').value      = row.dataset.baserate;
    document.getElementById('rt-surcharge').value = row.dataset.surcharge;
    document.getElementById('rt-interest').value  = row.dataset.interest;
    computePreview();
    openModal('modal-revtype');
}

// ─── SAVE EDIT ────────────────────────────────────
async function saveRevType() {
    const name      = document.getElementById('rt-name').value.trim();
    const category  = document.getElementById('rt-category').value;
    const rate      = parseFloat(document.getElementById('rt-rate').value) || 0;
    const surcharge = parseFloat(document.getElementById('rt-surcharge').value) || 0;
    const interest  = parseFloat(document.getElementById('rt-interest').value) || 0;

    if (!name) { showToast('Please enter a type name.', true); return; }

    const token = document.querySelector('input[name=__RequestVerificationToken]');

    try {
        const res = await fetch('/RevenueType/Edit', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : ''
            },
            body: new URLSearchParams({
                TypeID: editingTypeID,
                Name: name,
                CategoryID: category,
                BaseRate: rate,
                SurchargeRate: surcharge,
                InterestRate: interest
            })
        });
        const result = await res.json();
        if (result.success) {
            const row = document.querySelector('[data-typeid="' + editingTypeID + '"]');
            if (row) {
                row.dataset.name       = name;
                row.dataset.categoryid = category;
                row.dataset.baserate   = rate;
                row.dataset.surcharge  = surcharge;
                row.dataset.interest   = interest;
                const cells = row.querySelectorAll('td');
                cells[1].textContent = name;
                cells[2].textContent = category;
                cells[3].textContent = '₱ ' + rate.toFixed(2);
                cells[4].textContent = surcharge + '%';
                cells[5].textContent = interest + '%';
            }
            closeModal('modal-revtype');
            showToast('Revenue type updated!');
        } else {
            showToast(result.message || 'Failed to update.', true);
        }
    } catch (err) {
        showToast('Error updating revenue type.', true);
        console.error(err);
    }
}

// ─── DELETE ───────────────────────────────────────
async function removeRevType(btn) {
    if (!confirm('Are you sure you want to delete this revenue type? This cannot be undone.')) return;

    const row    = btn.closest('tr');
    const typeID = row.dataset.typeid;
    const token  = document.querySelector('input[name=__RequestVerificationToken]');

    try {
        const res = await fetch('/RevenueType/Delete', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : ''
            },
            body: new URLSearchParams({ TypeID: typeID })
        });
        const result = await res.json();
        if (result.success) {
            row.remove();
            showToast('Revenue type deleted.');
        } else {
            showToast(result.message || 'Failed to delete.', true);
        }
    } catch (err) {
        showToast('Error deleting revenue type.', true);
        console.error(err);
    }
}

// ─── ADD INLINE ───────────────────────────────────
async function addRevTypeInline() {
    const name      = document.getElementById('add-rt-name').value.trim();
    const category  = document.getElementById('add-rt-category').value;
    const rate      = parseFloat(document.getElementById('add-rt-rate').value) || 0;
    const surcharge = parseFloat(document.getElementById('add-rt-surcharge').value) || 0;
    const interest  = parseFloat(document.getElementById('add-rt-interest').value) || 0;

    if (!name) { showToast('Please enter a type name.', true); return; }

    const token = document.querySelector('input[name=__RequestVerificationToken]');

    try {
        const res = await fetch('/RevenueType/Add', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : ''
            },
            body: new URLSearchParams({
                Name: name,
                CategoryID: category,
                BaseRate: rate,
                SurchargeRate: surcharge,
                InterestRate: interest
            })
        });
        const result = await res.json();
        if (result.success) {
            const tbody = document.getElementById('revtype-tbody');
            const tr = document.createElement('tr');
            tr.dataset.typeid     = result.typeID;
            tr.dataset.name       = result.name;
            tr.dataset.categoryid = result.categoryID;
            tr.dataset.baserate   = result.baseRate;
            tr.dataset.surcharge  = result.surchargeRate;
            tr.dataset.interest   = result.interestRate;
            tr.innerHTML =
                '<td style="font-weight:700;color:var(--green-dark)">#' + String(result.typeID).padStart(3,'0') + '</td>'
                + '<td>' + result.name + '</td>'
                + '<td>' + result.categoryID + '</td>'
                + '<td>₱ ' + parseFloat(result.baseRate).toFixed(2) + '</td>'
                + '<td>' + result.surchargeRate + '%</td>'
                + '<td>' + result.interestRate + '%</td>'
                + '<td><button class="action-edit-btn" onclick="editRevType(this)" title="Edit"><span class="btn-icon-rev"><svg width="10" height="10" fill="none" viewBox="0 0 10 10"><path d="M1 9l1.5-1.5 5-5L9 4 4 9H1Z" stroke="white" stroke-width="1" stroke-linejoin="round"/><path d="M6.5 2.5l1 1" stroke="white" stroke-width="1" stroke-linecap="round"/></svg></span></button></td>'
                + '<td><button class="action-remove-btn" onclick="removeRevType(this)" title="Remove"><span class="btn-icon-rev"><svg width="10" height="10" fill="none" viewBox="0 0 10 10"><path d="M1.5 3h7M4 3V2h2v1M2.5 3l.5 6h4l.5-6" stroke="white" stroke-width="1" stroke-linecap="round" stroke-linejoin="round"/><path d="M4 5v2.5M6 5v2.5" stroke="white" stroke-width="1" stroke-linecap="round"/></svg></span></button></td>';
            tbody.appendChild(tr);

            document.getElementById('add-rt-name').value      = '';
            document.getElementById('add-rt-rate').value      = '';
            document.getElementById('add-rt-surcharge').value = '';
            document.getElementById('add-rt-interest').value  = '';
            showToast('Revenue type added!');
        } else {
            showToast(result.message || 'Failed to add.', true);
        }
    } catch (err) {
        showToast('Error adding revenue type.', true);
        console.error(err);
    }
}

// ─── INIT ────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function() {
    const modal = document.getElementById('modal-revtype');
    if (modal) {
        modal.addEventListener('click', function(e) {
            if (e.target === this) closeModal('modal-revtype');
        });
    }

    // Live compute on input change
    ['rt-rate', 'rt-surcharge', 'rt-interest'].forEach(function(id) {
        const el = document.getElementById(id);
        if (el) el.addEventListener('input', computePreview);
    });
});
document.addEventListener('DOMContentLoaded', function() {
    // ... existing init code ...

    const form = document.getElementById('payment-form');
    if (form) {
        form.addEventListener('submit', function(e) {
            const payeeID = document.getElementById('pay-payeeid').value;
            const fname   = document.getElementById('pay-fname').value;
            const lname   = document.getElementById('pay-lname').value;
            const typeID  = document.getElementById('pay-typeid').value;

            if (!payeeID && (!fname || !lname)) {
                e.preventDefault();
                showToast('Please select or add a payor first.', true);
                return;
            }

            if (!typeID) {
                e.preventDefault();
                showToast('Please select a collection type.', true);
                return;
            }
        });
    }
});
