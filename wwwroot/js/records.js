// ─── RECORDS + REQUESTS (COMBINED) ──────────────

function switchTab(tab) {
    const isRecords = tab === 'records';
    document.getElementById('panel-records').style.display = isRecords ? '' : 'none';
    const panelReq = document.getElementById('panel-requests');
    if (panelReq) panelReq.style.display = isRecords ? 'none' : '';
    document.getElementById('tab-records').classList.toggle('active', isRecords);
    const tabReq = document.getElementById('tab-requests');
    if (tabReq) tabReq.classList.toggle('active', !isRecords);
    document.getElementById('page-title-text').textContent = isRecords ? 'Payment Records' : 'Modification Requests';
    document.getElementById('page-sub-text').textContent = isRecords
        ? 'View and manage all payment records'
        : 'Review and approve or reject payment modification requests';
}

function filterRecords(query) {
    const q = query.toLowerCase();
    document.querySelectorAll('#records-tbody tr').forEach(tr => {
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

// ── COLLECTOR: Request Modification with proposed changes ──
function openModifyRequest(paymentID, txnID) {
    document.getElementById('modify-payment-id').value = paymentID;
    const sub = document.getElementById('modal-modify-sub');
    if (sub) sub.textContent = 'Propose changes for ' + txnID;

    // Reset fields
    document.getElementById('modify-reason').value = '';
    document.getElementById('modify-or').value = '';
    document.getElementById('modify-date').value = '';
    document.getElementById('modify-method').value = '';
    document.getElementById('modify-remarks').value = '';
    document.getElementById('modify-amount').value = '';
    document.getElementById('modify-type').value = '';

    // Load current values
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
        PaymentID:            paymentID,
        Reason:               reason,
        ProposedOR:           document.getElementById('modify-or').value,
        ProposedDate:         document.getElementById('modify-date').value,
        ProposedTypeID:       document.getElementById('modify-type').value,
        ProposedPaymentMethod: document.getElementById('modify-method').value,
        ProposedRemarks:      document.getElementById('modify-remarks').value,
        ProposedAmount:       document.getElementById('modify-amount').value
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

// ── OFFICER: Approve ──
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

// ── OFFICER: Reject ──
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
    ['modal-view-record','modal-modify-request','modal-confirm-approve','modal-confirm-reject'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.addEventListener('click', function(e) { if (e.target === this) closeModal(id); });
    });
});
