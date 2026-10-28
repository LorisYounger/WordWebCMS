document.querySelectorAll('.language-nav select[name=culture]').forEach(select => select.addEventListener('change', () => select.form.requestSubmit()));
const ui = JSON.parse(document.getElementById('ui-text').textContent);
document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', event => {
    if (form.dataset.confirm && !confirm(form.dataset.confirm)) event.preventDefault();
}));

const editor = document.getElementById('editor');
if (editor) {
    const language = document.getElementById('content-language');
    const showLanguage = () => editor.querySelectorAll('[data-content-language]').forEach(group => { group.hidden = group.dataset.contentLanguage !== language.value; });
    language.addEventListener('change', showLanguage);
    showLanguage();
    const status = document.getElementById('save-status');
    const version = editor.elements['Draft.Version'];
    const snapshot = () => JSON.stringify(Array.from(new FormData(editor).entries()).filter(([key]) => key !== 'Draft.Version' && key !== '__RequestVerificationToken' && key !== 'contentLanguage'));
    let saved = snapshot(), timer, saving = null, submitting = false, conflict = false;
    function save() {
        if (saving || submitting || conflict || snapshot() === saved) return;
        const sent = snapshot();
        status.textContent = ui['保存中…'];
        saving = (async () => {
          try {
            const response = await fetch(location.pathname + '?handler=Autosave', { method: 'POST', body: new FormData(editor) });
            if (!response.ok) { conflict = response.headers.get('X-Cms-Draft-Conflict') === 'true'; throw new Error(await response.text()); }
            const result = await response.json();
            version.value = result.version;
            saved = sent;
            status.textContent = ui['草稿已保存 · '] + new Date(result.savedUtc).toLocaleTimeString();
          } catch (error) { status.textContent = ui['保存失败：'] + error.message; }
          finally { saving = null; if (!submitting && !conflict && snapshot() !== sent) timer = setTimeout(save, 1500); }
        })();
        return saving;
    }
    editor.addEventListener('input', event => { if (event.target === language) return; clearTimeout(timer); status.textContent = ui['有未保存修改']; timer = setTimeout(save, 1500); });
    editor.addEventListener('submit', async event => {
        if (submitting) return;
        if (event.submitter?.dataset.confirm && !confirm(event.submitter.dataset.confirm)) { event.preventDefault(); return; }
        clearTimeout(timer);
        if (conflict) { event.preventDefault(); status.textContent = ui['草稿版本冲突，请复制文字后刷新页面。']; return; }
        // Allow the original submit when no request is pending; requestSubmit inside
        // the same submit event is ignored by the browser's reentrancy guard.
        if (!saving) { submitting = true; return; }
        event.preventDefault();
        const submitter = event.submitter;
        submitting = true;
        await saving;
        if (conflict) { submitting = false; status.textContent = ui['草稿版本冲突，请复制文字后刷新页面。']; return; }
        editor.requestSubmit(submitter);
    });
    window.addEventListener('beforeunload', event => { if (!submitting && snapshot() !== saved) { event.preventDefault(); event.returnValue = ''; } });
    const previewPanel = document.getElementById('body-preview');
    const preview = document.getElementById('preview');
    let previewRequest = 0;
    async function updatePreview() {
        const request = ++previewRequest;
        preview.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch(location.pathname + '?handler=Preview', { method: 'POST', body: new FormData(editor) });
            if (!response.ok || response.redirected) throw new Error('Preview failed');
            const html = await response.text();
            if (request === previewRequest) preview.innerHTML = html;
        } catch {
            if (request === previewRequest) preview.textContent = ui['预览失败，请检查登录状态后重试。'];
        } finally {
            if (request === previewRequest) preview.removeAttribute('aria-busy');
        }
    }
    previewPanel.addEventListener('toggle', () => { if (previewPanel.open) updatePreview(); });
    language.addEventListener('change', () => { if (previewPanel.open) updatePreview(); });
    editor.querySelectorAll('#preview-button, .translation-preview').forEach(button => button.addEventListener('click', () => {
        if (previewPanel.open) updatePreview();
        else previewPanel.open = true;
    }));
}
