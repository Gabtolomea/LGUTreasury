// ─── RECORDS + REQUESTS (COMBINED) ──────────────

// ── Tab switching ────────────────────────────────
function switchTab(tab) {
    const isRecords = tab === 'records';
    document.getElementById('panel-records').style.display  = isRecords ? '' : 'none';
    document.getElementById('panel-requests').style.display = isRecords ? 'none' : '';
    document.getElementById('tab-records').classList.toggle('active', isRecords);
    document.getElementById('tab-requests').classList.toggle('active', !isRecords);
    document.getElementById('page-title-text').textContent = isRecords ? 'Payment Records' : 'Modification Requests';
    document.getElementById('page-sub-text').textContent   = isRecords
        ? 'View and manage all payment records'
        : 'Review and approve or reject payment modification requests';
}

// ── RECORDS SEARCH ────────────────────────────────
function filterRecords(query) {
    const q = query.toLowerCase();
    document.querySelectorAll('#records-tbody tr').forEach(function(tr) {
        tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none';
    });
}

// ── REQUESTS SEARCH ───────────────────────────────
function filterApprove(query) {
    const q = query.toLowerCase();
    document.querySelectorAll('#approve-tbody tr').forEach(function(tr) {
        tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none';
    });
}

// ── VIEW RECORD MODAL ─────────────────────────────
function viewRecord(paymentID, txnID, payee, type, amount, date, orNumber, status, method, remarks) {
    document.getElementById('modal-view-title').textContent = 'Payment Record';
    document.getElementById('modal-record-content').innerHTML =
        '<div style="font-size:12px;color:var(--text-3);margin-bottom:10px">'
        + txnID + ' — <span class="status-text-' + status.toLowerCase() + '">' + status + '</span></div>'
        + '<div class="receipt-row"><span>OR Number:</span><strong>' + orNumber + '</strong></div>'
        + '<div class="receipt-row"><span>Payor:</span><strong>' + payee + '</strong></div>'
        + '<div class="receipt-row"><span>Collection Type:</span><span>' + type + '</span></div>'
        + '<div class="receipt-row"><span>Payment Method:</span><span>' + (method || '—') + '</span></div>'
        + '<div class="receipt-row"><span>Date Issued:</span><span>' + date + '</span></div>'
        + '<div class="receipt-row"><span>Total Amount:</span><strong>₱ ' + amount + '</strong></div>'
        + (remarks ? '<div class="receipt-row"><span>Remarks:</span><span>' + remarks + '</span></div>' : '');
    openModal('modal-view-record');
}

// ── APPROVE/REJECT ────────────────────────────────
let approveTarget = null;
let rejectTarget  = null;

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
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : ''
            },
            body: new URLSearchParams({ RequestID: approveTarget })
        });
        const result = await res.json();
        if (result.success) {
            closeModal('modal-confirm-approve');
            showToast('Modification approved!');
            updateRequestRow(approveTarget, 'Approved');
            updateBadge();
        } else {
            showToast(result.message || 'Failed to approve.', true);
        }
    } catch (err) {
        showToast('Error approving request.', true);
    }
}

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
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : ''
            },
            body: new URLSearchParams({ RequestID: rejectTarget, ReviewNote: reason })
        });
        const result = await res.json();
        if (result.success) {
            closeModal('modal-confirm-reject');
            showToast('Modification rejected.');
            updateRequestRow(rejectTarget, 'Rejected');
            updateBadge();
        } else {
            showToast(result.message || 'Failed to reject.', true);
        }
    } catch (err) {
        showToast('Error rejecting request.', true);
    }
}

function updateRequestRow(requestID, newStatus) {
    const row = document.querySelector('[data-requestid="' + requestID + '"]');
    if (!row) return;
    const statusCell = row.querySelector('.request-status');
    if (statusCell) statusCell.className = 'request-status status-text-' + newStatus.toLowerCase();
    if (statusCell) statusCell.textContent = newStatus;
    // Hide approve/reject buttons if no longer pending
    const appBtn = row.querySelector('.action-approve-btn');
    const rejBtn = row.querySelector('.action-reject-btn');
    if (appBtn) appBtn.style.display = 'none';
    if (rejBtn) rejBtn.style.display = 'none';
}

function updateBadge() {
    const pendingRows = document.querySelectorAll('#approve-tbody tr[data-requestid] .status-text-pending');
    const badge = document.getElementById('tab-badge');
    if (badge) {
        badge.textContent = pendingRows.length;
        badge.style.display = pendingRows.length > 0 ? '' : 'none';
    }
}

// ── INIT ─────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function() {
    updateBadge();

    const viewModal = document.getElementById('modal-view-record');
    if (viewModal) viewModal.addEventListener('click', function(e) {
        if (e.target === this) closeModal('modal-view-record');
    });

    const approveModal = document.getElementById('modal-confirm-approve');
    if (approveModal) approveModal.addEventListener('click', function(e) {
        if (e.target === this) closeModal('modal-confirm-approve');
    });

    const rejectModal = document.getElementById('modal-confirm-reject');
    if (rejectModal) rejectModal.addEventListener('click', function(e) {
        if (e.target === this) closeModal('modal-confirm-reject');
    });
});
