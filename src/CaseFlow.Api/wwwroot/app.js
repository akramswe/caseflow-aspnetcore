/* No framework required: the UI talks only to the documented REST API. */
const $ = (selector) => document.querySelector(selector);
const state = { page: 1, pageSize: 10, total: 0, case: null, searchTimer: null, toastTimer: null };
const nextStates = {
  Open: ['InProgress'],
  InProgress: ['Open', 'WaitingOnCustomer', 'Resolved'],
  WaitingOnCustomer: ['InProgress', 'Resolved'],
  Resolved: ['InProgress', 'Closed'],
  Closed: ['InProgress']
};
const statusLabels = {
  Open: 'Open', InProgress: 'In progress', WaitingOnCustomer: 'Waiting on customer',
  Resolved: 'Resolved', Closed: 'Closed'
};

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}
function dateLabel(value) {
  return value ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—';
}
function badge(text, type) {
  const allowed = type === 'priority' ? ['Low', 'Normal', 'High', 'Critical'] : Object.keys(statusLabels);
  const safe = allowed.includes(text) ? text : 'Open';
  return `<span class="badge ${type}-${safe}">${escapeHtml(type === 'status' ? statusLabels[safe] : safe)}</span>`;
}
function isOverdue(item) {
  return !['Resolved', 'Closed'].includes(item.status) && new Date(item.dueAtUtc) < new Date();
}
async function request(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: { ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...(options.headers || {}) }
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.detail || body.error || `Request failed (${response.status})`);
  }
  return response.json();
}
function toast(message, isError = false) {
  const item = $('#toast');
  item.textContent = message;
  item.classList.toggle('error', isError);
  item.classList.remove('hidden');
  clearTimeout(state.toastTimer);
  state.toastTimer = setTimeout(() => item.classList.add('hidden'), 4500);
}
function showBackdrop(selector, visible) {
  const element = $(selector);
  element.classList.toggle('hidden', !visible);
  element.setAttribute('aria-hidden', String(!visible));
  document.body.style.overflow = $('#drawerBackdrop').classList.contains('hidden') && $('#createBackdrop').classList.contains('hidden') ? '' : 'hidden';
}
async function refresh() {
  const params = new URLSearchParams({ page: String(state.page), pageSize: String(state.pageSize) });
  if ($('#searchInput').value.trim()) params.set('search', $('#searchInput').value.trim());
  if ($('#statusFilter').value) params.set('status', $('#statusFilter').value);
  if ($('#priorityFilter').value) params.set('priority', $('#priorityFilter').value);
  try {
    const [summary, listing] = await Promise.all([
      request('/api/dashboard'), request(`/api/cases?${params}`)
    ]);
    $('#statTotal').textContent = summary.total;
    $('#statInProgress').textContent = summary.inProgress;
    $('#statOverdue').textContent = summary.overdue;
    $('#statResolved').textContent = summary.resolved;
    state.total = listing.total;
    renderTable(listing.items);
    const start = listing.total ? (state.page - 1) * state.pageSize + 1 : 0;
    const end = Math.min(state.page * state.pageSize, listing.total);
    $('#paginationInfo').textContent = `Showing ${start}–${end} of ${listing.total} cases`;
    $('#prevPage').disabled = state.page === 1;
    $('#nextPage').disabled = state.page * state.pageSize >= listing.total;
  } catch (error) {
    $('#casesBody').innerHTML = '<tr><td colspan="7" class="empty-state">Unable to load cases. Check that the API is running.</td></tr>';
    toast(error.message, true);
  }
}
function renderTable(items) {
  if (!items.length) {
    $('#casesBody').innerHTML = '<tr><td colspan="7" class="empty-state">No cases match your filters.</td></tr>';
    return;
  }
  $('#casesBody').innerHTML = items.map((c) => `<tr>
    <td><div class="case-title" title="${escapeHtml(c.title)}">${escapeHtml(c.title)}</div><div class="case-ref">${escapeHtml(c.reference)}</div></td>
    <td><div class="customer-name">${escapeHtml(c.customerName)}</div><div class="customer-email">${escapeHtml(c.customerEmail)}</div></td>
    <td>${badge(c.priority, 'priority')}</td><td>${badge(c.status, 'status')}</td>
    <td>${escapeHtml(c.assignedTo || 'Unassigned')}</td>
    <td class="${isOverdue(c) ? 'due-danger' : ''}">${dateLabel(c.dueAtUtc)}${isOverdue(c) ? ' · Overdue' : ''}</td>
    <td><button type="button" class="open-case" data-open="${escapeHtml(c.id)}" aria-label="Open case ${escapeHtml(c.reference)}">View →</button></td>
  </tr>`).join('');
}
async function openCase(id) {
  try {
    state.case = await request(`/api/cases/${encodeURIComponent(id)}`);
    renderCaseDetails();
    showBackdrop('#drawerBackdrop', true);
    $('#caseDrawer').focus();
  } catch (error) { toast(error.message, true); }
}
function renderCaseDetails() {
  const c = state.case;
  $('#detailHeading').textContent = c.reference;
  const transitions = nextStates[c.status] || [];
  $('#caseDetailContent').innerHTML = `
    <h3 style="margin:0;color:#233149;font-size:20px;letter-spacing:-.3px">${escapeHtml(c.title)}</h3>
    <p class="detail-description">${escapeHtml(c.description)}</p>
    <div class="detail-summary">
      <div class="detail-box"><small>CUSTOMER</small><strong>${escapeHtml(c.customerName)}</strong></div>
      <div class="detail-box"><small>EMAIL</small><strong>${escapeHtml(c.customerEmail)}</strong></div>
      <div class="detail-box"><small>PRIORITY</small>${badge(c.priority, 'priority')}</div>
      <div class="detail-box"><small>CURRENT STATUS</small>${badge(c.status, 'status')}</div>
      <div class="detail-box"><small>ASSIGNED TO</small><strong>${escapeHtml(c.assignedTo || 'Unassigned')}</strong></div>
      <div class="detail-box"><small>SLA DEADLINE</small><strong class="${isOverdue(c) ? 'due-danger' : ''}">${dateLabel(c.dueAtUtc)}</strong></div>
    </div>
    <h4 class="section-title">Move case to</h4>
    <div class="workflow-buttons">${transitions.map((status) => `<button class="btn btn-light" data-next="${status}" type="button">${escapeHtml(statusLabels[status])} →</button>`).join('')}</div>
    <p class="subtle">Allowed changes are enforced by the C# domain workflow, not just this interface.</p>
    <h4 class="section-title">Assignment</h4>
    <form id="assignmentForm" class="detail-form"><label>Team member<input name="assignedTo" value="${escapeHtml(c.assignedTo || '')}" maxlength="80" placeholder="Leave empty to unassign"></label><button type="submit" class="btn btn-light">Save</button></form>
    <h4 class="section-title">Add an internal note</h4>
    <form id="noteForm" class="detail-form stack"><label>Note<textarea name="message" minlength="2" maxlength="1000" required placeholder="Write an update, next step, or investigation result..."></textarea></label><button type="submit" class="btn btn-primary">Add note</button></form>
    <h4 class="section-title">Internal notes <span class="subtle">(${c.notes.length})</span></h4>
    ${c.notes.length ? c.notes.map((note) => `<div class="timeline-item"><strong>${escapeHtml(note.author)}</strong><p>${escapeHtml(note.message)}</p><small>${dateLabel(note.createdAtUtc)}</small></div>`).join('') : '<p class="subtle">No notes yet.</p>'}
    <h4 class="section-title">Activity history <span class="subtle">(${c.activities.length})</span></h4>
    ${c.activities.map((activity) => `<div class="timeline-item"><strong>${escapeHtml(activity.actor)} · ${escapeHtml(activity.eventType.replaceAll('_', ' '))}</strong><p>${escapeHtml(activity.description)}</p><small>${dateLabel(activity.createdAtUtc)}</small></div>`).join('')}
    <p class="subtle">Created ${dateLabel(c.createdAtUtc)} · Updated ${dateLabel(c.updatedAtUtc)} · Version ${c.version}</p>
  `;
}
async function mutateCase(path, method, data, message) {
  try {
    state.case = await request(path, { method, body: JSON.stringify(data) });
    renderCaseDetails();
    await refresh();
    toast(message);
  } catch (error) {
    toast(error.message, true);
    if (error.message.includes('changed since')) await openCase(state.case.id);
  }
}

$('#newCaseButton').addEventListener('click', () => showBackdrop('#createBackdrop', true));
$('#closeCreate').addEventListener('click', () => showBackdrop('#createBackdrop', false));
$('#cancelCreate').addEventListener('click', () => showBackdrop('#createBackdrop', false));
$('#closeDrawer').addEventListener('click', () => showBackdrop('#drawerBackdrop', false));
$('#drawerBackdrop').addEventListener('click', (event) => { if (event.target.id === 'drawerBackdrop') showBackdrop('#drawerBackdrop', false); });
$('#createBackdrop').addEventListener('click', (event) => { if (event.target.id === 'createBackdrop') showBackdrop('#createBackdrop', false); });
document.addEventListener('keydown', (event) => { if (event.key === 'Escape') { showBackdrop('#drawerBackdrop', false); showBackdrop('#createBackdrop', false); } });
$('#exportButton').addEventListener('click', () => { window.location.href = '/api/cases/export.csv'; });
$('#casesBody').addEventListener('click', (event) => { const button = event.target.closest('[data-open]'); if (button) openCase(button.dataset.open); });
$('#searchInput').addEventListener('input', () => { clearTimeout(state.searchTimer); state.searchTimer = setTimeout(() => { state.page = 1; refresh(); }, 250); });
for (const selector of ['#statusFilter', '#priorityFilter']) $(selector).addEventListener('change', () => { state.page = 1; refresh(); });
$('#prevPage').addEventListener('click', () => { if (state.page > 1) { state.page--; refresh(); } });
$('#nextPage').addEventListener('click', () => { if (state.page * state.pageSize < state.total) { state.page++; refresh(); } });
$('#createForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  const button = $('#submitCreate');
  const form = new FormData(event.currentTarget);
  button.disabled = true;
  try {
    const created = await request('/api/cases', { method: 'POST', body: JSON.stringify(Object.fromEntries(form.entries())) });
    event.currentTarget.reset();
    showBackdrop('#createBackdrop', false);
    state.page = 1;
    await refresh();
    await openCase(created.id);
    toast(`Created case ${created.reference}`);
  } catch (error) { toast(error.message, true); }
  finally { button.disabled = false; }
});
$('#caseDetailContent').addEventListener('click', (event) => {
  const button = event.target.closest('[data-next]');
  if (button && state.case) mutateCase(`/api/cases/${state.case.id}/status`, 'PUT',
    { status: button.dataset.next, version: state.case.version, actor: 'Demo User' }, 'Status updated');
});
$('#caseDetailContent').addEventListener('submit', (event) => {
  event.preventDefault();
  const form = new FormData(event.target);
  const id = state.case?.id;
  if (!id) return;
  if (event.target.id === 'assignmentForm') mutateCase(`/api/cases/${id}/assignment`, 'PUT',
    { assignedTo: form.get('assignedTo'), version: state.case.version, actor: 'Demo User' }, 'Assignment updated');
  if (event.target.id === 'noteForm') mutateCase(`/api/cases/${id}/notes`, 'POST',
    { author: 'Demo User', message: form.get('message'), version: state.case.version }, 'Note added');
});
refresh();
