// ═══════════════════════════════════════════════════════════
// Mortgage Rule Engine — Frontend Logic
// ═══════════════════════════════════════════════════════════

const API_BASE = '';
let currentWorkflowId = null;  // int id — stable across renames
let allWorkflows = [];          // [{id, workflowName}, ...]

// ─── Initialization ─────────────────────────────────────
document.addEventListener('DOMContentLoaded', async () => {
    await loadWorkflows();
    setupTabNavigation();
    setupWorkflowSelectors();
    setupEditFormHandler();
    setupBuilder();
    setupEditorSubRules();
});

// ─── Workflow Management ────────────────────────────────
async function loadWorkflows() {
    try {
        const res = await fetch(`${API_BASE}api/rules/workflows`);
        // API now returns [{id, workflowName}, ...]
        allWorkflows = await res.json();
        if (allWorkflows.length > 0) {
            const still = allWorkflows.find(w => w.id === currentWorkflowId);
            currentWorkflowId = still ? still.id : allWorkflows[0].id;
        }
        updateWorkflowSelectors();
        const activeTab = document.querySelector('.tab-btn.active').dataset.tab;
        if (activeTab === 'rules') loadRules();
    } catch (err) {
        showToast('Failed to load workflows', 'error');
    }
}

function updateWorkflowSelectors() {
    ['eval-workflow-selector', 'rules-workflow-selector', 'builder-workflow-selector'].forEach(id => {
        const el = document.getElementById(id);
        if (!el) return;
        const currentVal = parseInt(el.value) || currentWorkflowId;
        el.innerHTML = allWorkflows.map(w =>
            `<option value="${w.id}" ${w.id === currentVal ? 'selected' : ''}>${w.workflowName}</option>`
        ).join('');
    });
}

function setupWorkflowSelectors() {
    ['eval-workflow-selector', 'rules-workflow-selector'].forEach(id => {
        document.getElementById(id).addEventListener('change', (e) => {
            currentWorkflowId = parseInt(e.target.value);
            updateWorkflowSelectors();
            const activeTab = document.querySelector('.tab-btn.active').dataset.tab;
            if (activeTab === 'rules') loadRules();
        });
    });
}

// ─── Tab Navigation ─────────────────────────────────────
function setupTabNavigation() {
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
            btn.classList.add('active');
            document.getElementById(`panel-${btn.dataset.tab}`).classList.add('active');
            if (btn.dataset.tab === 'rules') loadRules();
            updateWorkflowSelectors();
        });
    });
}

// ─── Loan Evaluator ─────────────────────────────────────
const editorEl = document.getElementById('evaluator-json-input');

document.getElementById('btn-evaluate').addEventListener('click', async () => {
    let payload;
    try {
        const raw = editorEl.value.trim();
        if (!raw) return showToast('Please enter JSON data', 'error');
        payload = JSON.parse(raw);
    } catch (e) {
        return showToast('Invalid JSON format', 'error');
    }

    const btn = document.getElementById('btn-evaluate');
    btn.disabled = true;
    btn.innerHTML = 'Evaluating...';

    try {
        const res = await fetch(`${API_BASE}api/eligibility/evaluate?workflowId=${currentWorkflowId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const result = await res.json();
        renderResults(result);
    } catch (err) {
        showToast('Evaluation failed', 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = 'Run Evaluation';
    }
});

function loadSample() {
    const sample = {};
    editorEl.value = JSON.stringify(sample, null, 2);
    showToast('Empty payload loaded — add any fields your rules require', 'info');
}

// Toggle label live update
document.addEventListener('DOMContentLoaded', () => {
    const toggle = document.getElementById('builder-enabled');
    const label = document.getElementById('toggle-status-label');
    if (toggle && label) {
        toggle.addEventListener('change', () => {
            label.textContent = toggle.checked ? 'Enabled' : 'Disabled';
            label.style.color = toggle.checked ? 'var(--success)' : 'var(--text-muted)';
        });
    }
});

function formatEvaluatorJson() {
    try {
        editorEl.value = JSON.stringify(JSON.parse(editorEl.value), null, 2);
    } catch (e) { showToast('Invalid JSON', 'error'); }
}

function renderResults(result) {
    const placeholder = document.getElementById('resultsPlaceholder');
    placeholder.style.display = 'none';
    const content = document.getElementById('resultsContent');
    content.style.display = 'block';

    const badgeClass = result.isEligible ? 'eligible' : 'ineligible';
    const badgeText = result.isEligible ? 'ELIGIBLE' : 'INELIGIBLE';
    const badgeIcon = result.isEligible
        ? '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3"><path d="M20 6L9 17l-5-5"/></svg>'
        : '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>';

    const total = result.totalRulesEvaluated || (result.details || []).length;
    const passed = result.rulesPassed || (result.details || []).filter(d => d.isSuccess).length;
    const failed = result.rulesFailed || total - passed;

    let rulesHtml = (result.details || []).map(d => {
        const confPercent = (d.confidenceScore !== null && d.confidenceScore !== undefined)
            ? Math.round(d.confidenceScore * 100)
            : null;
        const confBadge = confPercent !== null 
            ? `<span class="rule-confidence-badge" title="Lowest OCR field confidence used in this rule evaluation">Confidence: ${confPercent}%</span>`
            : '';

        const exceptionBox = d.exceptionMessage
            ? `<div class="rule-info-exception" title="Runtime evaluation error in engine"><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" style="margin-right:4px; vertical-align: middle;"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>System Error: ${d.exceptionMessage}</div>`
            : '';

        return `
        <div class="result-rule-item">
            <div class="rule-status-icon ${d.isSuccess ? 'pass' : 'fail'}">${d.isSuccess ? '✓' : '✗'}</div>
            <div class="rule-info">
                <div class="rule-info-header" style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:8px; margin-bottom:2px;">
                    <div class="rule-info-name" style="margin-bottom:0;">${d.ruleName || 'Rule'}</div>
                    ${confBadge}
                </div>
                <div class="rule-info-message">
                    ${d.isSuccess 
                        ? `<span style="color:var(--success)">${d.successMessage || 'Passed'}</span>` 
                        : `<span style="color:var(--danger)">${d.errorMessage || 'Failed'}</span>`}
                </div>
                ${exceptionBox}
                ${(!d.isSuccess && d.comment) ? `<div class="rule-info-comment">${d.comment}</div>` : ''}
            </div>
        </div>`;
    }).join('');

    content.innerHTML = `
        <div class="result-summary">
            <div class="result-badge ${badgeClass}">${badgeIcon} ${badgeText}</div>
            <div class="result-stats">
                <div class="stat-item">
                    <div class="stat-value total">${total}</div>
                    <div class="stat-label">Total Rules</div>
                </div>
                <div class="stat-item">
                    <div class="stat-value pass">${passed}</div>
                    <div class="stat-label">Passed</div>
                </div>
                <div class="stat-item">
                    <div class="stat-value fail">${failed}</div>
                    <div class="stat-label">Failed</div>
                </div>
            </div>
        </div>
        <div class="result-rules-header">
            <h4>Rule Details</h4>
        </div>
        <div class="result-rules">${rulesHtml || '<div class="loading-spinner">No rule details available</div>'}</div>
        <div class="result-json-toggle">
            <button class="btn btn-ghost btn-sm" onclick="toggleRawJson()">View Raw Response</button>
        </div>
        <div class="result-raw-json" id="resultRawJson" style="display:none;">
            <pre>${JSON.stringify(result, null, 2)}</pre>
        </div>`;
}

function toggleRawJson() {
    const el = document.getElementById('resultRawJson');
    el.style.display = el.style.display === 'none' ? 'block' : 'none';
}

// ─── Rule Dashboard ─────────────────────────────────────
async function loadRules() {
    const rulesListEl = document.getElementById('rulesList');
    rulesListEl.innerHTML = '<div class="loading-spinner">Loading rules...</div>';
    try {
        const res = await fetch(`${API_BASE}api/rules/workflows/${currentWorkflowId}/rules`);
        const rules = await res.json();
        renderRulesDashboard(rules);
    } catch (err) {
        rulesListEl.innerHTML = `<div class="loading-spinner">Failed to load rules</div>`;
    }
}

function buildFullExpression(rule) {
    if (!rule) return '';
    
    // Support both direct RuleEntity and nested RuleDefinition structures
    const expression = rule.expression !== undefined ? rule.expression : rule.definition?.expression;
    const operator = rule.operator !== undefined ? rule.operator : rule.definition?.operator;
    const childRules = rule.rules !== undefined ? rule.rules : rule.definition?.rules;
    
    const parentExpr = (expression || '').trim();
    
    // If no child rules, just return parent expression
    if (!childRules || childRules.length === 0) {
        return parentExpr || 'true';
    }
    
    // Process child rules recursively
    const childExprs = childRules
        .map(child => buildFullExpression(child))
        .map(expr => expr.trim())
        .filter(expr => expr !== '');
        
    if (childExprs.length === 0) {
        return parentExpr || 'true';
    }
    
    // Translate And -> &&, Or -> ||
    let jsOp = ' && ';
    if (operator && operator.toLowerCase() === 'or') {
        jsOp = ' || ';
    }
    
    // Combine children
    let combinedChildren = childExprs
        .map(expr => {
            // Add parentheses around child if it contains operator spaces or multiple terms to ensure proper precedence
            if (expr.includes(' && ') || expr.includes(' || ')) {
                return `(${expr})`;
            }
            return expr;
        })
        .join(jsOp);
        
    // Combine parent and children if parent has a real expression and it's not "true"
    if (parentExpr && parentExpr !== 'true') {
        if (parentExpr.includes(' && ') || parentExpr.includes(' || ')) {
            return `(${parentExpr})${jsOp}(${combinedChildren})`;
        }
        return `${parentExpr}${jsOp}(${combinedChildren})`;
    }
    
    return combinedChildren;
}

function renderRulesDashboard(rules) {
    let html = rules.map(r => {
        const isEnabled = r.enabled !== false;
        // Use workflowId and ID for API operations, ruleName for display and history
        return `
            <div class="rule-card ${isEnabled ? '' : 'disabled'}">
                <div class="rule-enabled-dot ${isEnabled ? 'active' : 'inactive'}"></div>
                <div class="rule-card-info">
                    <div class="rule-card-name">${r.ruleName}</div>
                    <div class="rule-card-expression">${buildFullExpression(r)}</div>
                </div>
                <div class="rule-card-actions">
                    <button class="btn btn-sm btn-ghost" onclick="openAuditPanel('${r.ruleName}')" title="View Audit Trail">History</button>
                    <button class="btn btn-sm btn-ghost" onclick="openEditModal(${r.workflowId}, ${r.id})" title="Edit Rule">Edit</button>
                    <button class="btn btn-sm btn-ghost" onclick="openTestModal(${r.workflowId}, ${r.id})" style="color: var(--success);" title="Test Rule">Test</button>
                    <label class="toggle-switch-sm" title="${isEnabled ? 'Disable' : 'Enable'} Rule">
                        <input type="checkbox" ${isEnabled ? 'checked' : ''} onchange="toggleRule(${r.workflowId}, ${r.id}, this)">
                        <span class="toggle-slider"></span>
                    </label>
                </div>
            </div>`;
    }).join('');
    document.getElementById('rulesList').innerHTML = html || '<div class="loading-spinner">No rules found</div>';
}

// ─── Audit Trail Logic ──────────────────────────────────
function renderAuditValue(val, isNew) {
    if (val === null || val === undefined || val === 'NULL' || val === 'null' || val === '') {
        return `<span class="audit-value-empty">none</span>`;
    }
    const trimmed = typeof val === 'string' ? val.trim() : String(val).trim();
    if ((trimmed.startsWith('{') && trimmed.endsWith('}')) || (trimmed.startsWith('[') && trimmed.endsWith(']'))) {
        try {
            const parsed = JSON.parse(val);
            return `<pre class="audit-json-code"><code>${JSON.stringify(parsed, null, 2)}</code></pre>`;
        } catch (e) { }
    }
    const cssClass = isNew ? 'val-new' : 'val-old';
    return `<span class="audit-value-text ${cssClass}">${val}</span>`;
}

async function openAuditPanel(ruleName) {
    const panel = document.getElementById('auditPanel');
    const overlay = document.getElementById('auditOverlay');
    const content = document.getElementById('auditTrailContent');
    const subtitle = document.getElementById('auditSubtitle');

    if (subtitle) {
        subtitle.textContent = ruleName;
    }

    panel.classList.add('open');
    overlay.classList.add('open');
    content.innerHTML = '<div class="loading-spinner">Loading history...</div>';

    try {
        const res = await fetch(`${API_BASE}api/rules/audit?ruleName=${encodeURIComponent(ruleName)}`);
        const history = await res.json();

        if (!history || history.length === 0) {
            content.innerHTML = '<div class="loading-spinner">No history found for this rule.</div>';
            return;
        }

        content.innerHTML = `
            <div class="audit-timeline">
                ${history.map(h => {
                    let badgeText = h.action;
                    let badgeClass = h.action.toLowerCase();
                    if (h.action === 'Update' && h.fieldName === 'Enabled') {
                        badgeText = 'TOGGLED';
                        badgeClass = 'toggled';
                    } else if (h.action === 'Create') {
                        badgeText = 'CREATE';
                        badgeClass = 'create';
                    } else if (h.action === 'Delete') {
                        badgeText = 'DELETE';
                        badgeClass = 'delete';
                    } else if (h.action === 'Update') {
                        badgeText = h.fieldName ? `UPDATE: ${h.fieldName.toUpperCase()}` : 'UPDATE';
                        badgeClass = 'update';
                    }

                    return `
                        <div class="audit-item action-${badgeClass}">
                            <div class="audit-card">
                                <div class="audit-card-header">
                                    <span class="audit-action-badge action-${badgeClass}">${badgeText}</span>
                                    <span class="audit-date">${new Date(h.changedDate).toLocaleString()}</span>
                                </div>
                                <div class="audit-user-row">
                                    <svg class="user-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                                        <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                                        <circle cx="12" cy="7" r="4"></circle>
                                    </svg>
                                    <span>${h.changedBy || 'System User'}</span>
                                </div>
                                <div class="audit-card-body">
                                    ${h.fieldName ? `
                                        <div class="audit-field-change">
                                            <div class="audit-field-title">${h.fieldName}</div>
                                            <div class="audit-diff-container">
                                                <div class="audit-diff-side">
                                                    <span class="audit-diff-label">BEFORE</span>
                                                    <div class="audit-diff-val">${renderAuditValue(h.oldValue, false)}</div>
                                                </div>
                                                <div class="audit-diff-arrow">
                                                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                                                        <line x1="5" y1="12" x2="19" y2="12"></line>
                                                        <polyline points="12 5 19 12 12 19"></polyline>
                                                    </svg>
                                                </div>
                                                <div class="audit-diff-side">
                                                    <span class="audit-diff-label">AFTER</span>
                                                    <div class="audit-diff-val">${renderAuditValue(h.newValue, true)}</div>
                                                </div>
                                            </div>
                                        </div>
                                    ` : `
                                        <div class="audit-full-value">
                                            ${renderAuditValue(h.newValue, true)}
                                        </div>
                                    `}
                                </div>
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        `;
    } catch (err) {
        content.innerHTML = '<div class="loading-spinner">Error loading audit trail.</div>';
    }
}

function closeAuditPanel() {
    document.getElementById('auditPanel').classList.remove('open');
    document.getElementById('auditOverlay').classList.remove('open');
}

// ─── Edit Rule Logic ────────────────────────────────────
async function openEditModal(workflowId, ruleId) {
    try {
        const res = await fetch(`${API_BASE}api/rules/workflows/${workflowId}/rules/${ruleId}`);
        const rule = await res.json();

        document.getElementById('edit-rule-workflow').value = workflowId;
        document.getElementById('edit-rule-id').value = rule.id;
        document.getElementById('edit-rule-original-name').value = rule.ruleName;
        document.getElementById('edit-rule-name-display').value = rule.ruleName;
        document.getElementById('edit-rule-description').value = rule.definition?.successEvent || '';
        document.getElementById('edit-rule-expression').value = rule.definition?.expression || '';
        document.getElementById('edit-rule-errorMessage').value = rule.definition?.errorMessage || '';
        document.getElementById('edit-rule-enabled').checked = rule.enabled !== false;
        document.getElementById('edit-rule-operator').value = rule.definition?.operator || '';

        // Workflows to Inject
        document.getElementById('edit-rule-workflows-to-inject').value = rule.definition?.workflowsToInject ? rule.definition.workflowsToInject.join(', ') : '';

        // Sample JSON Payload
        document.getElementById('edit-rule-sampleJson').value = rule.sampleJson || '';

        // Local Params & Actions (JSON)
        document.getElementById('edit-rule-local-params').value = rule.definition?.localParams ? JSON.stringify(rule.definition.localParams, null, 2) : '';
        document.getElementById('edit-rule-actions').value = rule.definition?.actions ? JSON.stringify(rule.definition.actions, null, 2) : '';

        // Sub-rules (Tree Editor)
        const subContainer = document.getElementById('edit-sub-rules-tree');
        subContainer.innerHTML = '';
        if (rule.definition?.rules && rule.definition.rules.length > 0) {
            rule.definition.rules.forEach(r => {
                const childCard = createSubRuleCardDom(0, r);
                subContainer.appendChild(childCard);
            });
        }
        updateEditSubRulesCount();

        document.getElementById('editModalTitle').textContent = `Edit Rule: ${rule.ruleName}`;
        document.getElementById('editModalOverlay').classList.add('open');
    } catch (err) {
        showToast('Failed to load rule details', 'error');
    }
}

function closeEditModal() {
    document.getElementById('editModalOverlay').classList.remove('open');
}

function setupEditFormHandler() {
    document.getElementById('editRuleForm').addEventListener('submit', async (e) => {
        e.preventDefault();
        const workflowId = document.getElementById('edit-rule-workflow').value; // stores int id
        const ruleId = document.getElementById('edit-rule-id').value;
        const name = document.getElementById('edit-rule-original-name').value;

        const expression = document.getElementById('edit-rule-expression').value;
        const sampleJsonRaw = document.getElementById('edit-rule-sampleJson').value;
        const workflowsToInjectRaw = document.getElementById('edit-rule-workflows-to-inject').value;
        const localParamsRaw = document.getElementById('edit-rule-local-params').value;
        const actionsRaw = document.getElementById('edit-rule-actions').value;

        // Parsing logic
        const workflowsToInject = workflowsToInjectRaw
            ? workflowsToInjectRaw.split(',').map(s => s.trim()).filter(s => s !== "")
            : null;

        let localParams = null;
        if (localParamsRaw) {
            try { localParams = JSON.parse(localParamsRaw); } catch (e) { return showToast('Invalid JSON in Local Parameters', 'error'); }
        }

        let actions = null;
        if (actionsRaw) {
            try { actions = JSON.parse(actionsRaw); } catch (e) { return showToast('Invalid JSON in Actions', 'error'); }
        }

        const payload = {
            ruleName: name,
            expression: expression,
            sampleJson: sampleJsonRaw ? sampleJsonRaw.trim() : null,
            description: document.getElementById('edit-rule-description').value,
            successMessage: document.getElementById('edit-rule-description').value,
            failureMessage: document.getElementById('edit-rule-errorMessage').value,
            errorMessage: document.getElementById('edit-rule-errorMessage').value,
            enabled: document.getElementById('edit-rule-enabled').checked,
            operator: document.getElementById('edit-rule-operator').value || null,
            workflowsToInject: workflowsToInject,
            localParams: localParams,
            actions: actions,
            rules: gatherSubRulesRecursive(document.getElementById('edit-sub-rules-tree'))
        };

        try {
            const res = await fetch(`${API_BASE}api/rules/workflows/${workflowId}/rules/${ruleId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (res.ok) {
                showToast('Rule updated successfully', 'success');
                closeEditModal();
                loadRules();
            } else {
                showToast('Update failed', 'error');
            }
        } catch (err) {
            showToast('Error updating rule', 'error');
        }
    });

    const testBtn = document.getElementById('btn-edit-test-expression');
    if (testBtn) {
        testBtn.onclick = () => testEditAdhocExpression();
    }
}

function setupEditorSubRules() {
    const btn = document.getElementById('btn-edit-add-sub-rule-card');
    if (btn) {
        btn.onclick = () => addRootEditSubRule();
    }
}

// ─── Rule Operations ────────────────────────────────────
async function toggleRule(workflowId, ruleId, checkboxEl) {
    const enabled = checkboxEl.checked;
    checkboxEl.disabled = true;

    // Visually toggle parent rule card container disabled class instantly
    const cardEl = checkboxEl.closest('.rule-card');
    const dotEl = cardEl?.querySelector('.rule-enabled-dot');

    try {
        const res = await fetch(`${API_BASE}api/rules/workflows/${workflowId}/rules/${ruleId}/toggle`, { 
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ enabled: enabled })
        });

        if (!res.ok) throw new Error('Toggle request failed');

        showToast('Rule toggled', 'success');

        // Update local styling directly for premium, real-time feedback
        if (cardEl) cardEl.classList.toggle('disabled', !enabled);
        if (dotEl) {
            dotEl.classList.toggle('active', enabled);
            dotEl.classList.toggle('inactive', !enabled);
        }
    } catch (err) { 
        showToast('Toggle failed', 'error');
        // Revert UI state on failure
        checkboxEl.checked = !enabled;
    } finally {
        checkboxEl.disabled = false;
    }
}

async function deleteRule(workflowId, ruleId) {
    if (!confirm('Are you sure you want to delete this rule?')) return;
    try {
        const res = await fetch(`${API_BASE}api/rules/workflows/${workflowId}/rules/${ruleId}`, {
            method: 'DELETE'
        });
        if (res.ok) {
            showToast('Rule deleted successfully', 'success');
            loadRules();
        } else {
            showToast('Failed to delete rule', 'error');
        }
    } catch (err) {
        showToast('Error deleting rule', 'error');
    }
}

// ─── Builder ──────────────────────────────────────────
let subRuleCounter = 0;

document.getElementById('builderForm')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    const ruleName = document.getElementById('builder-ruleName')?.value;
    const expression = document.getElementById('builder-expression')?.value;
    const sampleJson = document.getElementById('builder-sampleJson')?.value;
    const failureMessage = document.getElementById('builder-failureMessage')?.value;
    const description = document.getElementById('builder-description')?.value;
    const operator = document.getElementById('builder-operator')?.value;
    const workflowsToInjectRaw = document.getElementById('builder-workflows-to-inject')?.value;
    const localParamsRaw = document.getElementById('builder-local-params')?.value;
    const actionsRaw = document.getElementById('builder-actions')?.value;

    const workflowsToInject = workflowsToInjectRaw
        ? workflowsToInjectRaw.split(',').map(s => s.trim()).filter(s => s !== "")
        : null;

    let localParams = null;
    if (localParamsRaw) {
        try {
            localParams = JSON.parse(localParamsRaw);
        } catch (e) {
            showToast('Invalid JSON in Local Parameters', 'error');
            return;
        }
    }

    let actions = null;
    if (actionsRaw) {
        try {
            actions = JSON.parse(actionsRaw);
        } catch (e) {
            showToast('Invalid JSON in Actions', 'error');
            return;
        }
    }

    const ruleData = {
        ruleName,
        expression,
        sampleJson,
        failureMessage,
        errorMessage: failureMessage,
        description,
        successMessage: description,
        enabled: document.getElementById('builder-enabled').checked,
        operator: operator || null,
        workflowsToInject,
        localParams,
        actions,
        rules: gatherSubRules()
    };

    try {
        const selectedWorkflowId = document.getElementById('builder-workflow-selector')?.value || currentWorkflowId;
        const response = await fetch(`${API_BASE}api/rules/workflows/${selectedWorkflowId}/rules/builder`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(ruleData)
        });
        if (response.ok) {
            showToast('Rule created successfully!', 'success');
            loadRules();
            resetBuilderForm();
        } else {
            showToast('Failed to create rule', 'error');
        }
    } catch (err) { showToast('Creation failed', 'error'); }
});

function setupBuilder() {
    const addCardBtn = document.getElementById('btn-add-sub-rule-card');
    if (addCardBtn) {
        addCardBtn.onclick = () => addRootSubRule();
    }

    const testBtn = document.getElementById('btn-test-expression');
    if (testBtn) {
        testBtn.onclick = () => testAdhocExpression();
    }

    const resetBtn = document.getElementById('btn-reset-builder');
    if (resetBtn) {
        resetBtn.onclick = () => resetBuilderForm();
    }

    const workflowSelector = document.getElementById('builder-workflow-selector');
    if (workflowSelector) {
        workflowSelector.onchange = (e) => {
            currentWorkflowId = parseInt(e.target.value);
            updateWorkflowSelectors();
        };
    }
}

function addRootSubRule() {
    const container = document.getElementById('sub-rules-tree');
    if (!container) return;
    const card = createSubRuleCardDom(0);
    container.appendChild(card);
    updateSubRulesCount();
}

function addRootSubRuleWithData(data) {
    const container = document.getElementById('sub-rules-tree');
    if (!container) return;
    const card = createSubRuleCardDom(0, data);
    container.appendChild(card);
    updateSubRulesCount();
}

function createSubRuleCardDom(depth = 0, data = null) {
    subRuleCounter++;
    const cardId = `sub-rule-${subRuleCounter}`;
    const name = data?.ruleName || `SubRule_L${depth}_${subRuleCounter}`;

    const wrapper = document.createElement('div');
    wrapper.className = `sub-rule-card-wrapper spine-level-${depth}`;
    wrapper.id = wrapper.dataset.id = cardId;
    wrapper.dataset.depth = depth;

    const isEnabled = data ? (data.enabled !== false) : true;
    const selectedOperator = data?.operator || "";
    const expressionVal = data?.expression || "";
    const descriptionVal = data?.description || "";
    const failureMsgVal = data?.errorMessage || data?.failureMessage || "";

    wrapper.innerHTML = `
        <div class="sub-rule-card" id="card-${cardId}">
            <!-- Card Header -->
            <div class="sub-rule-header">
                <div class="sub-rule-header-left">
                    <button type="button" class="sub-rule-chevron-btn" onclick="toggleSubRuleCard('${cardId}')">
                        <svg class="chevron-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="6 9 12 15 18 9"></polyline></svg>
                    </button>
                    <input type="text" class="sub-rule-title-input" value="${name}" placeholder="Sub-Rule Name" required>
                </div>
                <div class="sub-rule-header-right">
                    <label class="toggle-switch-orange">
                        <input type="checkbox" class="sub-rule-enabled-chk" ${isEnabled ? 'checked' : ''}>
                        <span class="slider"></span>
                        <span style="font-size: 0.75rem; font-weight: 600; color: var(--text-secondary); margin-left: 6px;">Enabled</span>
                    </label>
                    <div class="help-icon" title="Enabled sub-rules are evaluated by the engine. Disabled ones are skipped.">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"></path><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>
                    </div>
                    <select class="sub-rule-operator-select" onchange="handleSubRuleOperatorChange('${cardId}')">
                        <option value="" ${selectedOperator === "" ? 'selected' : ''}>none</option>
                        <option value="And" ${selectedOperator === "And" ? 'selected' : ''}>And</option>
                        <option value="Or" ${selectedOperator === "Or" ? 'selected' : ''}>Or</option>
                    </select>
                    <button type="button" class="sub-rule-delete-btn" onclick="deleteSubRuleCard('${cardId}')">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path><line x1="10" y1="11" x2="10" y2="17"></line><line x1="14" y1="11" x2="14" y2="17"></line></svg>
                    </button>
                </div>
            </div>

            <!-- Card Body -->
            <div class="sub-rule-body">
                <!-- Expression Textarea -->
                <div class="form-group sub-rule-expression-group" style="margin-bottom: 0;">
                    <label style="display: block; font-size: 0.75rem; font-weight: 700; text-transform: uppercase; color: var(--text-secondary); margin-bottom: 4px; letter-spacing: 0.05em;">Expression *</label>
                    <textarea class="sub-rule-expression-input" rows="2" placeholder="e.g. input.Borrower1CreditScore >= 620" style="width: 100%; padding: 8px 12px; background: var(--bg-input); border: 1px solid var(--border); border-radius: 6px; color: var(--text-primary); font-family: 'Consolas', monospace; font-size: 0.85rem; outline: none; resize: vertical; min-height: 50px;">${expressionVal}</textarea>
                </div>

                <!-- Logical Warning Alert -->
                <div class="logical-group-warning" style="display: none;">
                    <div class="logical-group-warning-icon">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>
                    </div>
                    <div class="logical-group-warning-text">
                        Logical GROUP active. Direct expression is ignored by the engine. Children results are combined using <span class="warning-operator-label" style="font-weight: 800; text-transform: uppercase;">And</span> logic.
                    </div>
                </div>

                <!-- Description & Failure Message Grid -->
                <div class="form-row" style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 0;">
                    <div class="form-group" style="margin-bottom: 0;">
                        <label style="display: block; font-size: 0.75rem; font-weight: 700; text-transform: uppercase; color: var(--text-secondary); margin-bottom: 4px; letter-spacing: 0.05em;">Success Message</label>
                        <input type="text" class="sub-rule-description-input" placeholder="Message when sub-rule succeeds" value="${descriptionVal}" style="width: 100%; height: 36px; padding: 6px 10px; border-radius: 6px; border: 1px solid var(--border); background: var(--bg-input); color: var(--text-primary); outline: none; font-size: 0.85rem;">
                    </div>
                    <div class="form-group" style="margin-bottom: 0;">
                        <label style="display: block; font-size: 0.75rem; font-weight: 700; text-transform: uppercase; color: var(--text-secondary); margin-bottom: 4px; letter-spacing: 0.05em;">Failure Message</label>
                        <input type="text" class="sub-rule-failureMessage-input" placeholder="Error message when rule fails" value="${failureMsgVal}" style="width: 100%; height: 36px; padding: 6px 10px; border-radius: 6px; border: 1px solid var(--border); background: var(--bg-input); color: var(--text-primary); outline: none; font-size: 0.85rem;">
                    </div>
                </div>

                <!-- Children Spines / Cards Container -->
                <div class="sub-rules-children-container" style="display: none; flex-direction: column; gap: 12px; margin-top: 8px;">
                    <!-- Recursive children cards added here -->
                </div>

                <!-- Dash Add Button for Children -->
                <button type="button" class="dashed-add-button sub-rule-add-child-btn" onclick="addChildSubRuleCard('${cardId}')" style="display: none; width: 100%; border: 2px dashed rgba(99,102,241,0.3); border-radius: 8px; background: transparent; padding: 10px; color: var(--accent-hover); font-weight: 600; cursor: pointer; align-items: center; justify-content: center; gap: 6px; transition: all 0.2s; font-size: 0.8rem; margin-top: 4px;">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                    Add Sub-Rule Card
                </button>
            </div>
        </div>
    `;

    // Wait for insertion in DOM to execute the operator state if data is provided
    if (data && (selectedOperator === "And" || selectedOperator === "Or")) {
        setTimeout(() => {
            const opSelect = wrapper.querySelector('.sub-rule-operator-select');
            if (opSelect) {
                handleSubRuleOperatorChange(cardId);
                const nestedContainer = wrapper.querySelector('.sub-rules-children-container');
                if (nestedContainer && data.rules) {
                    nestedContainer.innerHTML = '';
                    data.rules.forEach(childRule => {
                        const childCard = createSubRuleCardDom(depth + 1, childRule);
                        nestedContainer.appendChild(childCard);
                    });
                }
            }
        }, 0);
    }

    return wrapper;
}

function handleSubRuleOperatorChange(cardId) {
    const cardWrapper = document.getElementById(cardId);
    if (!cardWrapper) return;

    const opSelect = cardWrapper.querySelector('.sub-rule-operator-select');
    const operator = opSelect.value;

    const exprGroup = cardWrapper.querySelector('.sub-rule-expression-group');
    const warningEl = cardWrapper.querySelector('.logical-group-warning');
    const childrenContainer = cardWrapper.querySelector('.sub-rules-children-container');
    const addChildBtn = cardWrapper.querySelector('.sub-rule-add-child-btn');

    if (operator === 'And' || operator === 'Or') {
        exprGroup.style.display = 'none';
        warningEl.style.display = 'flex';
        warningEl.querySelector('.warning-operator-label').textContent = operator;
        childrenContainer.style.display = 'flex';
        addChildBtn.style.display = 'flex';

        if (childrenContainer.children.length === 0) {
            const currentDepth = parseInt(cardWrapper.dataset.depth || 0);
            if (currentDepth < 2) {
                const childCard = createSubRuleCardDom(currentDepth + 1);
                childrenContainer.appendChild(childCard);
            }
        }
    } else {
        exprGroup.style.display = 'block';
        warningEl.style.display = 'none';
        childrenContainer.style.display = 'none';
        addChildBtn.style.display = 'none';
    }

    updateSubRulesCount();
    updateEditSubRulesCount();
}

function addChildSubRuleCard(parentId) {
    const parentWrapper = document.getElementById(parentId);
    if (!parentWrapper) return;

    const currentDepth = parseInt(parentWrapper.dataset.depth || 0);
    if (currentDepth >= 2) {
        showToast('Nesting is limited to 3 levels deep', 'error');
        return;
    }

    const childrenContainer = parentWrapper.querySelector('.sub-rules-children-container');
    const childCard = createSubRuleCardDom(currentDepth + 1);
    childrenContainer.appendChild(childCard);
    updateSubRulesCount();
    updateEditSubRulesCount();
}

function deleteSubRuleCard(cardId) {
    const cardEl = document.getElementById(cardId);
    if (cardEl) {
        cardEl.remove();
        updateSubRulesCount();
        updateEditSubRulesCount();
    }
}

function toggleSubRuleCard(cardId) {
    const cardEl = document.getElementById(`card-${cardId}`);
    if (cardEl) {
        cardEl.classList.toggle('collapsed');
    }
}

function updateSubRulesCount() {
    const rootContainer = document.getElementById('sub-rules-tree');
    if (!rootContainer) return;
    const count = rootContainer.querySelectorAll('.sub-rule-card').length;
    const countBadge = document.getElementById('sub-rules-count-badge');
    if (countBadge) {
        countBadge.textContent = count;
    }
}

function gatherSubRules() {
    return gatherSubRulesRecursive(document.getElementById('sub-rules-tree'));
}

function gatherSubRulesRecursive(containerEl) {
    if (!containerEl) return null;
    const rules = [];
    const children = Array.from(containerEl.children).filter(child => child.classList.contains('sub-rule-card-wrapper'));

    children.forEach(child => {
        const cardId = child.id;
        const cardEl = document.getElementById(`card-${cardId}`);
        if (!cardEl) return;

        const name = child.querySelector('.sub-rule-title-input').value.trim();
        const enabled = child.querySelector('.sub-rule-enabled-chk').checked;
        const operator = child.querySelector('.sub-rule-operator-select').value;
        const expression = child.querySelector('.sub-rule-expression-input').value.trim();
        const description = child.querySelector('.sub-rule-description-input').value.trim();
        const failureMessage = child.querySelector('.sub-rule-failureMessage-input').value.trim();

        const ruleObj = {
            ruleName: name || `SubRule_${cardId}`,
            enabled: enabled,
            description: description || null,
            successMessage: description || null,
            errorMessage: failureMessage || null,
            failureMessage: failureMessage || null
        };

        if (operator === 'And' || operator === 'Or') {
            ruleObj.operator = operator;
            const nestedContainer = child.querySelector('.sub-rules-children-container');
            const subRules = gatherSubRulesRecursive(nestedContainer);
            ruleObj.rules = subRules || [];
            ruleObj.expression = "";
        } else {
            ruleObj.operator = null;
            ruleObj.expression = expression;
            ruleObj.rules = null;
        }

        rules.push(ruleObj);
    });

    return rules.length > 0 ? rules : null;
}

async function testAdhocExpression() {
    const expression = document.getElementById('builder-expression')?.value.trim();
    const sampleJson = document.getElementById('builder-sampleJson')?.value.trim();

    if (!expression) {
        return showToast('Please enter an expression to test', 'error');
    }
    if (!sampleJson) {
        return showToast('Please enter sample payload JSON', 'error');
    }

    try {
        JSON.parse(sampleJson);
    } catch (e) {
        return showToast('Sample payload is not valid JSON', 'error');
    }

    const testBtn = document.getElementById('btn-test-expression');
    testBtn.disabled = true;
    testBtn.innerHTML = 'Testing...';

    try {
        const response = await fetch(`${API_BASE}api/eligibility/evaluate-adhoc`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ expression, sampleJson })
        });
        
        if (response.ok) {
            const result = await response.json();
            if (result.isSuccess) {
                showToast('Expression syntax valid and evaluates to TRUE!', 'success');
            } else {
                showToast(`Expression error: ${result.exceptionMessage || 'Evaluated to FALSE / failed'}`, 'error');
            }
        } else {
            showToast('Validation failed on server', 'error');
        }
    } catch (err) {
        showToast('Network error testing expression', 'error');
    } finally {
        testBtn.disabled = false;
        testBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polygon points="5 3 19 12 5 21 5 3"></polygon></svg> Test Expression';
    }
}

async function testEditAdhocExpression() {
    const expression = document.getElementById('edit-rule-expression')?.value.trim();
    const sampleJson = document.getElementById('edit-rule-sampleJson')?.value.trim();
    const workflowId = document.getElementById('edit-rule-workflow')?.value;
    const ruleId = document.getElementById('edit-rule-id')?.value;

    if (!expression) {
        return showToast('Please enter an expression to test', 'error');
    }
    if (!sampleJson) {
        return showToast('Please enter sample payload JSON', 'error');
    }

    try {
        JSON.parse(sampleJson);
    } catch (e) {
        return showToast('Sample payload is not valid JSON', 'error');
    }

    const testBtn = document.getElementById('btn-edit-test-expression');
    testBtn.disabled = true;
    testBtn.innerHTML = 'Testing...';

    try {
        const response = await fetch(`${API_BASE}api/eligibility/evaluate-adhoc`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                expression,
                sampleJson,
                workflowId: workflowId ? parseInt(workflowId, 10) : null,
                ruleId: ruleId ? parseInt(ruleId, 10) : null
            })
        });
        
        if (response.ok) {
            const result = await response.json();
            if (result.isSuccess) {
                showToast('Expression syntax valid and evaluates to TRUE!', 'success');
            } else {
                showToast(`Expression error: ${result.exceptionMessage || 'Evaluated to FALSE / failed'}`, 'error');
            }
        } else {
            showToast('Validation failed on server', 'error');
        }
    } catch (err) {
        showToast('Network error testing expression', 'error');
    } finally {
        testBtn.disabled = false;
        testBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polygon points="5 3 19 12 5 21 5 3"></polygon></svg> Test Expression';
    }
}

function resetBuilderForm() {
    document.getElementById('builderForm')?.reset();
    const tree = document.getElementById('sub-rules-tree');
    if (tree) {
        tree.innerHTML = '';
    }
    updateSubRulesCount();
    showToast('Builder form cleared', 'info');
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = message;
    container.appendChild(toast);
    setTimeout(() => toast.remove(), 4000);
}

// ─── Modal Row Helpers (Tree Editor) ───
function addRootEditSubRule() {
    const container = document.getElementById('edit-sub-rules-tree');
    if (!container) return;
    const card = createSubRuleCardDom(0);
    container.appendChild(card);
    updateEditSubRulesCount();
}

function updateEditSubRulesCount() {
    const rootContainer = document.getElementById('edit-sub-rules-tree');
    if (!rootContainer) return;
    const count = rootContainer.querySelectorAll('.sub-rule-card').length;
    const countBadge = document.getElementById('edit-sub-rules-count-badge');
    if (countBadge) {
        countBadge.textContent = count;
    }
}

// ✨ AI Assistant Logic
async function setupAIAssistant() {
    const btn = document.getElementById('btn-ai-generate');
    const promptInput = document.getElementById('ai-prompt');
    const statusEl = document.getElementById('ai-status');
    const exprTextarea = document.getElementById('builder-expression');

    if (!btn) return;

    btn.addEventListener('click', async () => {
        const prompt = promptInput.value.trim();
        if (!prompt) return showToast('Please enter a description for the rule', 'warning');

        btn.disabled = true;
        btn.innerHTML = '<div class="loading-spinner-sm"></div> <span style="margin-left:8px">Generating...</span>';
        statusEl.style.display = 'flex';
        statusEl.innerHTML = '<div class="loading-spinner-sm"></div> <span style="margin-left:8px">Consulting GPT-4o...</span>';

        try {
            const res = await fetch(`${API_BASE}api/ai/translate`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ prompt: prompt })
            });
            const data = await res.json();

            if (res.ok) {
                setTimeout(() => {
                    exprTextarea.value = data.expression;
                    statusEl.innerHTML = `<span>✨ <strong>Suggested:</strong> ${data.expression}</span>`;
                    btn.disabled = false;
                    btn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg> Generate Expression';
                    showToast('AI Logic Generated', 'success');
                }, 800);
            } else {
                const errorMessage = data.error || (typeof data === 'string' ? data : 'AI Service unavailable');
                throw new Error(errorMessage);
            }
        } catch (err) {
            statusEl.innerHTML = `<span style="color:var(--danger); display:block; line-height:1.4">❌ ${err.message}</span>`;
            btn.disabled = false;
            btn.innerHTML = 'Retry Generation';
            showToast('AI Translation failed', 'error');
        }
    });
}

// Initialize on load
document.addEventListener('DOMContentLoaded', () => {
    setupAIAssistant();
    setupBuilder();

    // Bind Dashboard Test Modal listeners
    const dashboardTestBtn = document.getElementById('btn-dashboard-test-expression');
    if (dashboardTestBtn) {
        dashboardTestBtn.onclick = () => testDashboardAdhocExpression();
    }
    
    const savePayloadBtn = document.getElementById('btn-save-test-payload');
    if (savePayloadBtn) {
        savePayloadBtn.onclick = () => saveTestPayloadOnly();
    }
});

// ─── Test Rule Modal Logic ──────────────────────────────
async function openTestModal(workflowId, ruleId) {
    try {
        const res = await fetch(`${API_BASE}api/rules/workflows/${workflowId}/rules/${ruleId}`);
        const rule = await res.json();

        document.getElementById('test-rule-workflow').value = workflowId;
        document.getElementById('test-rule-id').value = rule.id;
        document.getElementById('test-rule-expression').value = buildFullExpression(rule);
        document.getElementById('test-rule-sampleJson').value = rule.sampleJson || '';

        const resultEl = document.getElementById('test-rule-result-container');
        if (resultEl) {
            resultEl.style.display = 'none';
            resultEl.innerHTML = '';
        }

        document.getElementById('testModalTitle').textContent = `Test Rule: ${rule.ruleName}`;
        document.getElementById('testModalOverlay').classList.add('open');
    } catch (err) {
        showToast('Failed to load rule details', 'error');
    }
}

function closeTestModal() {
    document.getElementById('testModalOverlay').classList.remove('open');
}

async function testDashboardAdhocExpression() {
    const expression = document.getElementById('test-rule-expression')?.value.trim();
    const sampleJson = document.getElementById('test-rule-sampleJson')?.value.trim();
    const workflowId = document.getElementById('test-rule-workflow')?.value;
    const ruleId = document.getElementById('test-rule-id')?.value;

    if (!expression) {
        return showToast('This rule does not have a simple expression to test', 'error');
    }
    if (!sampleJson) {
        return showToast('Please enter sample payload JSON', 'error');
    }

    try {
        JSON.parse(sampleJson);
    } catch (e) {
        return showToast('Sample payload is not valid JSON', 'error');
    }

    const testBtn = document.getElementById('btn-dashboard-test-expression');
    testBtn.disabled = true;
    testBtn.innerHTML = 'Testing...';

    try {
        const response = await fetch(`${API_BASE}api/eligibility/evaluate-adhoc`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                expression,
                sampleJson,
                workflowId: workflowId ? parseInt(workflowId, 10) : null,
                ruleId: ruleId ? parseInt(ruleId, 10) : null
            })
        });
        
        const resultEl = document.getElementById('test-rule-result-container');
        if (response.ok) {
            const result = await response.json();
            if (resultEl) {
                resultEl.style.display = 'block';
                if (result.isSuccess) {
                    resultEl.style.background = 'rgba(16, 185, 129, 0.1)';
                    resultEl.style.border = '1px solid rgba(16, 185, 129, 0.3)';
                    resultEl.style.color = 'var(--success)';
                    resultEl.innerHTML = `
                        <div style="display: flex; align-items: center; gap: 8px;">
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
                            <span>Expression syntax valid and evaluates to TRUE!</span>
                        </div>`;
                    showToast('Expression syntax valid and evaluates to TRUE!', 'success');
                } else {
                    resultEl.style.background = 'rgba(239, 68, 68, 0.1)';
                    resultEl.style.border = '1px solid rgba(239, 68, 68, 0.3)';
                    resultEl.style.color = 'var(--danger)';
                    const msg = result.exceptionMessage || 'Expression evaluated to FALSE / failed';
                    resultEl.innerHTML = `
                        <div style="display: flex; align-items: flex-start; gap: 8px;">
                            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="flex-shrink: 0; margin-top: 2px;"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>
                            <span style="font-family: 'Consolas', monospace; font-size: 0.85rem; word-break: break-all;">${msg}</span>
                        </div>`;
                    showToast(`Expression error: ${msg}`, 'error');
                }
            }
        } else {
            if (resultEl) {
                resultEl.style.display = 'block';
                resultEl.style.background = 'rgba(239, 68, 68, 0.1)';
                resultEl.style.border = '1px solid rgba(239, 68, 68, 0.3)';
                resultEl.style.color = 'var(--danger)';
                resultEl.innerHTML = `
                    <div style="display: flex; align-items: center; gap: 8px;">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>
                        <span>Validation failed on server</span>
                    </div>`;
            }
            showToast('Validation failed on server', 'error');
        }
    } catch (err) {
        const resultEl = document.getElementById('test-rule-result-container');
        if (resultEl) {
            resultEl.style.display = 'block';
            resultEl.style.background = 'rgba(239, 68, 68, 0.1)';
            resultEl.style.border = '1px solid rgba(239, 68, 68, 0.3)';
            resultEl.style.color = 'var(--danger)';
            resultEl.innerHTML = `
                <div style="display: flex; align-items: center; gap: 8px;">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>
                    <span>Network error testing expression</span>
                </div>`;
        }
        showToast('Network error testing expression', 'error');
    } finally {
        testBtn.disabled = false;
        testBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polygon points="5 3 19 12 5 21 5 3"></polygon></svg> Test Expression';
    }
}

async function saveTestPayloadOnly() {
    const workflowId = document.getElementById('test-rule-workflow').value;
    const ruleId = document.getElementById('test-rule-id').value;
    const sampleJson = document.getElementById('test-rule-sampleJson').value;

    if (sampleJson) {
        try {
            JSON.parse(sampleJson);
        } catch (e) {
            return showToast('Payload is not valid JSON', 'error');
        }
    }

    const saveBtn = document.getElementById('btn-save-test-payload');
    saveBtn.disabled = true;
    saveBtn.innerHTML = 'Saving...';

    try {
        const res = await fetch(`${API_BASE}api/rules/workflows/${workflowId}/rules/${ruleId}/sample-json`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sampleJson: sampleJson || null })
        });

        if (res.ok) {
            showToast('Sample payload saved successfully', 'success');
            closeTestModal();
            loadRules();
        } else {
            showToast('Failed to save payload', 'error');
        }
    } catch (err) {
        showToast('Error saving payload', 'error');
    } finally {
        saveBtn.disabled = false;
        saveBtn.innerHTML = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg> Save Payload';
    }
}
