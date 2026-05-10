// ─── RECORDS + REQUESTS (COMBINED) ──────────────

function switchTab(tab) {
    const panels = ['panel-records', 'panel-collection', 'panel-requests'];
    const tabs   = ['tab-records', 'tab-collection', 'tab-requests'];
    panels.forEach(id => { const el = document.getElementById(id); if (el) el.style.display = 'none'; });
    tabs.forEach(id => { const el = document.getElementById(id); if (el) el.classList.remove('active'); });

    const panel = document.getElementById('panel-' + tab);
    const tabEl = document.getElementById('tab-' + tab);
    if (panel) panel.style.display = '';
    if (tabEl) tabEl.classList.add('active');

    const titles = {
        records:    ['Payment Records',        'View and manage all payment records'],
        collection: ['For Collection',         'Confirm that you have collected these payments'],
        requests:   ['Modification Requests',  'Review and approve or reject payment modification requests']
    };
    if (titles[tab]) {
        document.getElementById('page-title-text').textContent = titles[tab][0];
        document.getElementById('page-sub-text').textContent   = titles[tab][1];
    }
}

function filterRecords(query) {
    const q = query.toLowerCase();
    document.querySelectorAll('#records-tbody tr').forEach(tr => {
        tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none';
    });
}

function filterCollection(query) {
    const q = query.toLowerCase();
    document.querySelectorAll('#collection-tbody tr').forEach(tr => {
        tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none';
    });
}

function filterApprove(query) {
    const q = query.toLowerCase();
    document.querySelectorAll('#approve-tbody tr').forEach(tr => {
        tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none';
    });
}

function viewRecord(txnID, payee, type, amount, date, orNumber, status, method, remarks) {
    document.getElementById('modal-view-title').textContent = 'Payment Record';
    document.getElementById('modal-record-content').innerHTML =
        '<div style="font-size:12px;color:var(--text-3);margin-bottom:10px">'
        + txnID + ' — <span class="status-text-' + status.toLowerCase() + '">' + status + '</span></div>'
        + '<div class="receipt-row"><span>OR Number:</span><strong>' + orNumber + '</strong></div>'
        + '<div class="receipt-row"><span>Payor:</span><strong>' + payee + '</strong></div>'
        + '<div class="receipt-row"><span>Collection Type:</span><span>' + type + '</span></div>'
        + '<div class="receipt-row"><span>Payment Method:</span><span>' + (method || '—') + '</span></div>'
        + '<div class="receipt-row"><span>Date Issued:</span><span>' + date + '</span></div>'
        + (remarks ? '<div class="receipt-row"><span>Remarks:</span><span>' + remarks + '</span></div>' : '');
    openModal('modal-view-record');
}

function viewRequest(requestID, txnID, payee, reason, date, reqBy, status, propOR, propDate, propMethod, propAmount, propRemarks) {
    document.getElementById('modal-view-title').textContent = 'Modification Request';
    let html = '<div style="font-size:12px;color:var(--text-3);margin-bottom:10px">'
        + txnID + ' — <span class="status-text-' + status.toLowerCase() + '">' + status + '</span></div>'
        + '<div class="receipt-row"><span>Payor:</span><strong>' + payee + '</strong></div>'
        + '<div class="receipt-row"><span>Requested By:</span><span>' + reqBy + '</span></div>'
        + '<div class="receipt-row"><span>Date:</span><span>' + date + '</span></div>'
        + '<div class="receipt-row"><span>Reason:</span><span>' + (reason || '—') + '</span></div>'
        + '<div style="font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:var(--green);margin:10px 0 6px;padding-bottom:4px;border-bottom:1.5px solid var(--green-light)">Proposed Changes</div>';
    if (propOR)      html += '<div class="receipt-row"><span>OR Number:</span><span>' + propOR + '</span></div>';
    if (propDate)    html += '<div class="receipt-row"><span>Date:</span><span>' + propDate + '</span></div>';
    if (propMethod)  html += '<div class="receipt-row"><span>Method:</span><span>' + propMethod + '</span></div>';
    if (propAmount)  html += '<div class="receipt-row"><span>Amount:</span><strong>₱ ' + propAmount + '</strong></div>';
    if (propRemarks) html += '<div class="receipt-row"><span>Remarks:</span><span>' + propRemarks + '</span></div>';
    if (!propOR && !propDate && !propMethod && !propAmount && !propRemarks)
        html += '<div style="color:var(--text-3);font-size:12px">No specific changes proposed.</div>';
    document.getElementById('modal-record-content').innerHTML = html;
    openModal('modal-view-record');
}

// ── COLLECTOR: Confirm Collection ──────────────────
async function confirmCollection(paymentID) {
    if (!confirm('Confirm that you have collected this payment?')) return;
    const token = document.querySelector('input[name=__RequestVerificationToken]');
    try {
        const res = await fetch('/Record/ConfirmCollection', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token ? token.value : '' },
            body: new URLSearchParams({ PaymentID: paymentID })
        });
        const result = await res.json();
        if (result.success) {
            showToast('Collection confirmed!');
            const row = document.querySelector('#collection-tbody [data-paymentid="' + paymentID + '"]');
            if (row) row.remove();
            updateCollectionBadge();
            setTimeout(() => window.location.reload(), 1200);
        } else {
            showToast(result.message || 'Failed to confirm.', true);
        }
    } catch (err) { showToast('Error confirming collection.', true); }
}

function updateCollectionBadge() {
    const rows = document.querySelectorAll('#collection-tbody tr[data-paymentid]');
    const badge = document.getElementById('collection-badge');
    if (badge) {
        badge.textContent = rows.length;
        badge.style.display = rows.length > 0 ? '' : 'none';
    }
}

// ── COLLECTOR: Request Modification ───────────────
function openModifyRequest(paymentID, txnID) {
    document.getElementById('modify-payment-id').value = paymentID;
    const sub = document.getElementById('modal-modify-sub');
    if (sub) sub.textContent = 'Propose changes for ' + txnID;

    document.getElementById('modify-reason').value  = '';
    document.getElementById('modify-or').value      = '';
    document.getElementById('modify-date').value    = '';
    document.getElementById('modify-method').value  = '';
    document.getElementById('modify-remarks').value = '';
    document.getElementById('modify-amount').value  = '';
    document.getElementById('modify-type').value    = '';

    fetch('/Record/GetPaymentDetails?paymentID=' + paymentID)
        .then(r => r.json())
        .then(data => {
            if (data.success) {
                document.getElementById('modify-or').value      = data.officialReceipt || '';
                document.getElementById('modify-date').value    = data.dateIssued || '';
                document.getElementById('modify-method').value  = data.paymentMethod || '';
                document.getElementById('modify-remarks').value = data.remarks || '';
                document.getElementById('modify-amount').value  = data.totalAmount || '';
                document.getElementById('modify-type').value    = data.typeID || '';
            }
        });

    openModal('modal-modify-request');
}

async function submitModifyRequest() {
    const paymentID = document.getElementById('modify-payment-id').value;
    const reason    = document.getElementById('modify-reason').value.trim();
    if (!reason) { showToast('Please enter a reason for the modification.', true); return; }

    const token = document.querySelector('input[name=__RequestVerificationToken]');
    const body = new URLSearchParams({
        PaymentID: paymentID, Reason: reason,
        ProposedOR:            document.getElementById('modify-or').value,
        ProposedDate:          document.getElementById('modify-date').value,
        ProposedTypeID:        document.getElementById('modify-type').value,
        ProposedPaymentMethod: document.getElementById('modify-method').value,
        ProposedRemarks:       document.getElementById('modify-remarks').value,
        ProposedAmount:        document.getElementById('modify-amount').value
    });

    try {
        const res = await fetch('/Record/RequestModification', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token ? token.value : '' },
            body: body
        });
        const result = await res.json();
        if (result.success) {
            closeModal('modal-modify-request');
            showToast('Modification request submitted!');
            setTimeout(() => window.location.reload(), 1200);
        } else {
            showToast(result.message || 'Failed to submit.', true);
        }
    } catch (err) { showToast('Error submitting request.', true); }
}

// ── OFFICER: Approve ──────────────────────────────
let approveTarget = null;

function openApprove(requestID) {
    approveTarget = requestID;
    openModal('modal-confirm-approve');
}

async function confirmApprove() {
    if (!approveTarget) return;
    const token = document.querySelector('input[name=__RequestVerificationToken]');
    try {
        const res = await fetch('/Record/ApproveRequest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token ? token.value : '' },
            body: new URLSearchParams({ RequestID: approveTarget })
        });
        const result = await res.json();
        if (result.success) {
            closeModal('modal-confirm-approve');
            showToast('Modification approved and record updated!');
            setTimeout(() => window.location.reload(), 1200);
        } else {
            showToast(result.message || 'Failed to approve.', true);
        }
    } catch (err) { showToast('Error approving request.', true); }
}

// ── OFFICER: Reject ───────────────────────────────
let rejectTarget = null;

function openReject(requestID) {
    rejectTarget = requestID;
    document.getElementById('reject-reason').value = '';
    openModal('modal-confirm-reject');
}

async function confirmReject() {
    if (!rejectTarget) return;
    const token  = document.querySelector('input[name=__RequestVerificationToken]');
    const reason = document.getElementById('reject-reason').value.trim();
    try {
        const res = await fetch('/Record/RejectRequest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token ? token.value : '' },
            body: new URLSearchParams({ RequestID: rejectTarget, ReviewNote: reason })
        });
        const result = await res.json();
        if (result.success) {
            closeModal('modal-confirm-reject');
            showToast('Modification rejected.');
            setTimeout(() => window.location.reload(), 1200);
        } else {
            showToast(result.message || 'Failed to reject.', true);
        }
    } catch (err) { showToast('Error rejecting request.', true); }
}

function updateBadge() {
    const pendingRows = document.querySelectorAll('#approve-tbody .status-text-pending');
    const badge = document.getElementById('tab-badge');
    if (badge) {
        badge.textContent = pendingRows.length;
        badge.style.display = pendingRows.length > 0 ? '' : 'none';
    }
}

document.addEventListener('DOMContentLoaded', function() {
    updateBadge();
    updateCollectionBadge();
    ['modal-view-record','modal-modify-request','modal-confirm-approve','modal-confirm-reject'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.addEventListener('click', function(e) { if (e.target === this) closeModal(id); });
    });
});
