// ── Payee Search ──
const payeeSearch = document.getElementById('payee-search');
const suggestions = document.getElementById('payee-suggestions');

payeeSearch.addEventListener('input', async function () {
    const query = this.value.trim();
    if (query.length < 2) { suggestions.style.display = 'none'; return; }

    const res = await fetch(`/Record/SearchPayee?query=${encodeURIComponent(query)}`);
    const data = await res.json();

    if (data.length === 0) { suggestions.style.display = 'none'; return; }

    suggestions.innerHTML = data.map(p => `
        <div onclick="fillPayee(${JSON.stringify(p).replace(/"/g, '&quot;')})"
             style="padding:10px 14px;cursor:pointer;border-bottom:1px solid #f0f0f0;font-size:13px"
             onmouseover="this.style.background='#f0faf0'"
             onmouseout="this.style.background='white'">
            <strong>${p.lastname}, ${p.firstname} ${p.middlename ?? ''}</strong>
            <div style="font-size:11px;color:#888">${p.contactNumber ?? ''}</div>
        </div>
    `).join('');
    suggestions.style.display = 'block';
});

function fillPayee(p) {
    document.querySelector('[name=FirstName]').value  = p.firstname ?? '';
    document.querySelector('[name=MiddleName]').value = p.middlename ?? '';
    document.querySelector('[name=LastName]').value   = p.lastname ?? '';
    document.querySelector('[name=Suffix]').value     = p.suffix ?? '';
    document.querySelector('[name=ContactNumber]').value    = p.contactNumber ?? '';
    document.querySelector('[name=ResidenceAddress]').value = p.residenceAddress ?? '';
    payeeSearch.value = `${p.lastname}, ${p.firstname}`;
    suggestions.style.display = 'none';
}

// Close suggestions when clicking outside
document.addEventListener('click', function (e) {
    if (!payeeSearch.contains(e.target)) suggestions.style.display = 'none';
});

document.addEventListener('DOMContentLoaded', function () {
    const dateInput = document.getElementById('date-issued');
    if (dateInput) {
        const today = new Date().toISOString().split('T')[0];
        dateInput.max = today;
    }
    // ... rest of your existing DOMContentLoaded code
});

let activeRevInput = null;
let activeHiddenInput = null;

function openRevTypeModal(displayInput) {
    const row = displayInput.closest('tr');
    activeRevInput = displayInput;
    activeHiddenInput = row.querySelector('.rt-hidden');
    document.getElementById('rt-search').value = '';
    filterRevTypes();
    document.getElementById('modal-revtype-picker').classList.add('open');
}

function closeRevTypeModal() {
    document.getElementById('modal-revtype-picker').classList.remove('open');
}

function selectRevType(el) {
    const name = el.dataset.name;
    const rate = parseFloat(el.dataset.rate);
    activeRevInput.value = name;
    activeHiddenInput.value = el.dataset.id;
    // Auto-fill base amount from base rate
    const row = activeRevInput.closest('tr');
    const baseInput = row.querySelector('[name=BaseAmounts]');
    if (baseInput && rate > 0) {
        baseInput.value = rate.toFixed(2);
        calcLine(baseInput);
    }
    closeRevTypeModal();
}

function filterRevTypes() {
    const query = document.getElementById('rt-search').value.toLowerCase();
    document.querySelectorAll('.rt-option').forEach(opt => {
        opt.style.display = opt.dataset.name.toLowerCase().includes(query) ? '' : 'none';
    });
}

// Close modal on backdrop click
document.addEventListener('DOMContentLoaded', function () {
    const overlay = document.getElementById('modal-revtype-picker');
    if (overlay) {
        overlay.addEventListener('click', function (e) {
            if (e.target === this) closeRevTypeModal();
        });
    }
});

// ── Line Items ──
function addLineItem() {
    const tbody = document.getElementById('line-items-body');
    const tr = document.createElement('tr');
    tr.innerHTML = `
        <td>
            <input type="text" class="rt-display" placeholder="Click to select..." readonly onclick="openRevTypeModal(this)" />
            <input type="hidden" name="TypeIDs" class="rt-hidden" />
        </td>
        <td><input type="number" name="Quantities" value="1" min="1" style="width:60px" oninput="calcLine(this)" /></td>
        <td><input type="number" name="BaseAmounts" placeholder="0.00" step="0.01" oninput="calcLine(this)" /></td>
        <td><input type="number" name="SurchargeAmounts" placeholder="0.00" step="0.01" oninput="calcLine(this)" /></td>
        <td><input type="number" name="InterestAmounts" placeholder="0.00" step="0.01" oninput="calcLine(this)" /></td>
        <td class="line-total" style="font-weight:700">₱ 0.00</td>
        <td><button type="button" onclick="removeLineItem(this)" style="background:none;border:none;color:var(--red);font-size:16px;cursor:pointer">✕</button></td>
    `;
    tbody.appendChild(tr);
}

function removeLineItem(btn) {
    const tbody = document.getElementById('line-items-body');
    if (tbody.rows.length <= 1) {
        showToast('At least one line item is required.', true);
        return;
    }
    btn.closest('tr').remove();
    updateGrandTotal();
}

function calcLine(inp) {
    const row = inp.closest('tr');
    const qty       = parseFloat(row.querySelector('[name=Quantities]').value) || 1;
    const base      = parseFloat(row.querySelector('[name=BaseAmounts]').value) || 0;
    const surcharge = parseFloat(row.querySelector('[name=SurchargeAmounts]').value) || 0;
    const interest  = parseFloat(row.querySelector('[name=InterestAmounts]').value) || 0;
    const total = (base + surcharge + interest) * qty;
    row.querySelector('.line-total').textContent = '₱ ' + total.toFixed(2);
    updateGrandTotal();
}

function updateGrandTotal() {
    let sum = 0;
    document.querySelectorAll('.line-total').forEach(td => {
        sum += parseFloat(td.textContent.replace('₱', '').trim()) || 0;
    });
    document.getElementById('grand-total').textContent = '₱ ' + sum.toFixed(2);
}
