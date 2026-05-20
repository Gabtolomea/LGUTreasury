// ─── RECORDS + MESSAGES + MY COLLECTIONS ──────────────

function switchTab(tab) {
    const panels = ['panel-records', 'panel-mycollections', 'panel-requests'];
    const tabs   = ['tab-records', 'tab-mycollections', 'tab-requests'];
    panels.forEach(id => { const el = document.getElementById(id); if (el) el.style.display = 'none'; });
    tabs.forEach(id => { const el = document.getElementById(id); if (el) el.classList.remove('active'); });

    const panel = document.getElementById('panel-' + tab);
    const tabEl = document.getElementById('tab-' + tab);
    if (panel) panel.style.display = '';
    if (tabEl) tabEl.classList.add('active');

    const titles = {
        records:       ['Payment Records',  'View and manage all payment records'],
        mycollections: ['My Collections',   'Summary of payments you have collected'],
        requests:      ['Messages',         'Messages from collectors about payment records']
    };
    if (titles[tab]) {
        document.getElementById('page-title-text').textContent = titles[tab][0];
        document.getElementById('page-sub-text').textContent   = titles[tab][1];
    }

    if (tab === 'mycollections') applyCollectionFilter();
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

// ── VIEW RECORD ───────────────────────────────────
function viewRecord(txnID, payee, type, amount, date, orNumber, status, method, remarks, collector) {
    document.getElementById('modal-view-title').textContent = 'Payment Record';
    document.getElementById('modal-record-content').innerHTML =
        '<div style="font-size:12px;color:var(--text-3);margin-bottom:10px">'
        + txnID + ' — <span class="status-text-' + status.toLowerCase() + '">' + status + '</span></div>'
        + '<div class="receipt-row"><span>OR Number:</span><strong>' + orNumber + '</strong></div>'
        + '<div class="receipt-row"><span>Payor:</span><strong>' + payee + '</strong></div>'
        + '<div class="receipt-row"><span>Collection Type:</span><span>' + type + '</span></div>'
        + '<div class="receipt-row"><span>Payment Method:</span><span>' + (method || '—') + '</span></div>'
        + '<div class="receipt-row"><span>Collected By:</span><span>' + (collector || '—') + '</span></div>'
        + '<div class="receipt-row"><span>Date Issued:</span><span>' + date + '</span></div>'
        + (remarks ? '<div class="receipt-row"><span>Remarks:</span><span>' + remarks + '</span></div>' : '');
    openModal('modal-view-record');
}

// ── VIEW MESSAGE ──────────────────────────────────
function viewMessage(requestID, txnID, payee, reason, date, reqBy, status, reviewNote) {
    document.getElementById('modal-message-content').innerHTML =
        '<div style="font-size:12px;color:var(--text-3);margin-bottom:10px">'
        + txnID + ' — <span class="status-text-' + status.toLowerCase() + '">' + status + '</span></div>'
        + '<div class="receipt-row"><span>Payor:</span><strong>' + payee + '</strong></div>'
        + '<div class="receipt-row"><span>Sent By:</span><span>' + reqBy + '</span></div>'
        + '<div class="receipt-row"><span>Date:</span><span>' + date + '</span></div>'
        + '<div class="receipt-row"><span>Message:</span><span>' + (reason || '—') + '</span></div>'
        + (reviewNote ? '<div class="receipt-row"><span>Officer Note:</span><span>' + reviewNote + '</span></div>' : '');
    openModal('modal-view-message');
}

// ── MY COLLECTIONS ────────────────────────────────
let currentFilter = 'today';

function setCollectionFilter(filter) {
    currentFilter = filter;
    document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
    const btn = document.getElementById('filter-' + filter);
    if (btn) btn.classList.add('active');

    const customInputs = document.getElementById('custom-range-inputs');
    if (customInputs) customInputs.style.display = filter === 'custom' ? 'flex' : 'none';

    if (filter !== 'custom') applyCollectionFilter();
}

function applyCollectionFilter() {
    if (typeof allCollectorRecords === 'undefined') return;

    const today     = new Date(); today.setHours(0,0,0,0);
    const yesterday = new Date(today); yesterday.setDate(today.getDate() - 1);
    const weekStart = new Date(today); weekStart.setDate(today.getDate() - today.getDay());

    let fromDate = null;
    let toDate   = null;
    let periodLabel = 'Today';

    if (currentFilter === 'today') {
        fromDate = toDate = today;
        periodLabel = 'Today';
    } else if (currentFilter === 'yesterday') {
        fromDate = toDate = yesterday;
        periodLabel = 'Yesterday';
    } else if (currentFilter === 'week') {
        fromDate = weekStart;
        toDate   = today;
        periodLabel = 'This Week';
    } else if (currentFilter === 'custom') {
        const fromInput = document.getElementById('filter-from').value;
        const toInput   = document.getElementById('filter-to').value;
        if (!fromInput || !toInput) return;
        fromDate = new Date(fromInput); fromDate.setHours(0,0,0,0);
        toDate   = new Date(toInput);   toDate.setHours(23,59,59,999);
        periodLabel = fromInput + ' to ' + toInput;
    }

    const filtered = allCollectorRecords.filter(r => {
        const d = new Date(r.date); d.setHours(0,0,0,0);
        if (currentFilter === 'custom') {
            const to = new Date(toDate); to.setHours(23,59,59,999);
            return d >= fromDate && d <= to;
        }
        if (fromDate && toDate) return d >= fromDate && d <= toDate;
        return true;
    });

    // Sort newest first
    filtered.sort((a, b) => new Date(b.date) - new Date(a.date));

    // Update stats
    const total = filtered.reduce((sum, r) => sum + r.amount, 0);
    const count = filtered.length;
    const avg   = count > 0 ? total / count : 0;

    document.getElementById('stat-total').textContent  = '₱ ' + total.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    document.getElementById('stat-count').textContent  = count;
    document.getElementById('stat-avg').textContent    = '₱ ' + avg.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    document.getElementById('stat-period').textContent = periodLabel;

    // Render table
    const tbody = document.getElementById('mycollections-tbody');
    if (!filtered.length) {
        tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;color:var(--text-3);padding:24px">No records found for this period.</td></tr>';
        return;
    }

    tbody.innerHTML = filtered.map(r =>
        '<tr>'
        + '<td style="font-weight:700;color:var(--green-dark)">' + r.txnID + '</td>'
        + '<td>' + r.payee + '</td>'
        + '<td>' + r.type + '</td>'
        + '<td style="font-weight:600">₱ ' + r.amount.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + '</td>'
        + '<td style="color:var(--text-3)">' + r.dateDisplay + '</td>'
        + '<td style="color:var(--text-3)">' + r.orNumber + '</td>'
        + '<td style="color:var(--text-3)">' + r.method + '</td>'
        + '</tr>'
    ).join('');
}

// ── COLLECTOR: Send Message ───────────────────────
function openSendMessage(paymentID, txnID) {
    document.getElementById('message-payment-id').value = paymentID;
    const sub = document.getElementById('modal-message-sub');
    if (sub) sub.textContent = 'Describe what you want changed for ' + txnID;
    document.getElementById('message-reason').value = '';
    openModal('modal-send-message');
}

async function submitMessage() {
    const paymentID = document.getElementById('message-payment-id').value;
    const reason    = document.getElementById('message-reason').value.trim();
    if (!reason) { showToast('Please enter a message.', true); return; }

    const token = document.querySelector('input[name=__RequestVerificationToken]');
    try {
        const res = await fetch('/Record/SendMessage', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : ''
            },
            body: new URLSearchParams({ PaymentID: paymentID, Reason: reason })
        });
        const result = await res.json();
        if (result.success) {
            closeModal('modal-send-message');
            showToast('Message sent to Officer!');
            setTimeout(() => window.location.reload(), 1200);
        } else {
            showToast(result.message || 'Failed to send message.', true);
        }
    } catch (err) { showToast('Error sending message.', true); }
}

// ── OFFICER: Edit Record ──────────────────────────
function openEditRecord(paymentID, txnID) {
    fetch('/Record/GetPaymentDetails?paymentID=' + paymentID)
        .then(r => r.json())
        .then(data => {
            if (!data.success) { showToast('Failed to load record details.', true); return; }

            document.getElementById('edit-payment-id').value  = paymentID;
            document.getElementById('edit-or').value          = data.officialReceipt || '';
            document.getElementById('edit-date').value        = data.dateIssued || '';
            document.getElementById('edit-method').value      = data.paymentMethod || '';
            document.getElementById('edit-remarks').value     = data.remarks || '';
            document.getElementById('edit-amount').value      = data.totalAmount || '';
            document.getElementById('edit-collector').value   = data.collectedBy_UserID || '';

            const sub = document.getElementById('modal-edit-sub');
            if (sub) sub.textContent = 'Editing record ' + txnID;

            openModal('modal-edit-record');
        })
        .catch(() => showToast('Error loading record.', true));
}

async function submitEditRecord() {
    const paymentID = document.getElementById('edit-payment-id').value;
    const or        = document.getElementById('edit-or').value.trim();
    const date      = document.getElementById('edit-date').value;
    const method    = document.getElementById('edit-method').value;
    const remarks   = document.getElementById('edit-remarks').value.trim();
    const amount    = document.getElementById('edit-amount').value;
    const collector = document.getElementById('edit-collector').value;

    if (!or)        { showToast('OR Number is required.', true); return; }
    if (!date)      { showToast('Date Issued is required.', true); return; }
    if (!collector) { showToast('Please select a collector.', true); return; }

    const token = document.querySelector('input[name=__RequestVerificationToken]');
    try {
        const res = await fetch('/Record/EditRecord', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : ''
            },
            body: new URLSearchParams({
                PaymentID: paymentID, OfficialReceipt: or,
                DateIssued: date, PaymentMethod: method,
                Remarks: remarks, TotalAmount: amount,
                CollectedBy_UserID: collector
            })
        });
        const result = await res.json();
        if (result.success) {
            closeModal('modal-edit-record');
            showToast('Record updated successfully!');
            setTimeout(() => window.location.reload(), 1200);
        } else {
            showToast(result.message || 'Failed to update record.', true);
        }
    } catch (err) { showToast('Error updating record.', true); }
}

// ── OFFICER: Resolve Message ──────────────────────
let resolveTarget = null;

function openResolve(requestID, txnID) {
    resolveTarget = requestID;
    const sub = document.getElementById('modal-resolve-sub');
    if (sub) sub.textContent = 'Resolving message for ' + txnID;
    document.getElementById('resolve-note').value = '';
    openModal('modal-resolve-message');
}

async function confirmResolve() {
    if (!resolveTarget) return;
    const token = document.querySelector('input[name=__RequestVerificationToken]');
    const note  = document.getElementById('resolve-note').value.trim();
    try {
        const res = await fetch('/Record/ResolveMessage', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : ''
            },
            body: new URLSearchParams({ RequestID: resolveTarget, ReviewNote: note })
        });
        const result = await res.json();
        if (result.success) {
            closeModal('modal-resolve-message');
            showToast('Message marked as resolved!');
            setTimeout(() => window.location.reload(), 1200);
        } else {
            showToast(result.message || 'Failed to resolve.', true);
        }
    } catch (err) { showToast('Error resolving message.', true); }
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
    ['modal-view-record', 'modal-send-message', 'modal-resolve-message',
     'modal-view-message', 'modal-edit-record'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.addEventListener('click', function(e) { if (e.target === this) closeModal(id); });
    });
});
