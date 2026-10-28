// Run without MySQL: NODE_PATH must make playwright available.
const { chromium } = require('playwright');
const { expect } = require('playwright/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    try {
        const page = await browser.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        const ui = { '预览失败，请检查登录状态后重试。': 'Preview failed' };
        await page.route('http://preview.test/editor', route => route.fulfill({ contentType: 'text/html', body: `
            <meta charset="utf-8"><script id="ui-text" type="application/json">${JSON.stringify(ui)}</script>
            <span id="save-status"></span><form id="editor">
            <input name="Draft.Version" value="1"><input name="__RequestVerificationToken" value="test-token">
            <select id="content-language" name="contentLanguage"><option value="">Default</option><option value="en">English</option></select>
            <div data-content-language=""><textarea name="Draft.Markdown">Default body</textarea><button type="button" id="preview-button">Update</button></div>
            <div data-content-language="en"><textarea name="Translations[en].Markdown">English body</textarea><button type="button" class="translation-preview">Update</button></div>
            <details id="body-preview"><summary>Preview</summary><div id="preview"></div></details></form>` }));
        let mode = 'ok', calls = 0;
        await page.route('http://preview.test/editor?handler=Preview', async route => {
            calls++;
            const form = await new Response(route.request().postDataBuffer(), {
                headers: { 'content-type': route.request().headers()['content-type'] }
            }).formData();
            assert.equal(form.get('__RequestVerificationToken'), 'test-token');
            if (mode === 'network') return route.abort();
            if (mode === 'http') return route.fulfill({ status: 403 });
            if (mode === 'redirect') return route.fulfill({ status: 302, headers: { location: '/login' } });
            const body = form.get(form.get('contentLanguage') ? 'Translations[en].Markdown' : 'Draft.Markdown');
            await route.fulfill({ contentType: 'text/html', body: '<h2>' + body + '</h2>' });
        });
        await page.route('http://preview.test/login', route => route.fulfill({ contentType: 'text/html', body: 'Sign in' }));
        await page.goto('http://preview.test/editor');
        await page.addScriptTag({ content: fs.readFileSync('WordWebCMS/wwwroot/js/admin.js', 'utf8') });
        await page.locator('#body-preview summary').click();
        await expect(page.locator('#preview h2')).toHaveText('Default body');
        assert.equal(calls, 1);
        await page.locator('#content-language').selectOption('en');
        await expect(page.locator('#preview h2')).toHaveText('English body');
        await page.locator('textarea[name="Translations[en].Markdown"]').evaluate(el => { el.value = 'Unsaved translation'; });
        await page.locator('.translation-preview').click();
        await expect(page.locator('#preview h2')).toHaveText('Unsaved translation');
        for (mode of ['network', 'http', 'redirect']) {
            await page.locator('.translation-preview').click();
            await expect(page.locator('#preview')).toHaveText('Preview failed');
            await expect(page.locator('#preview')).not.toHaveAttribute('aria-busy');
        }
        mode = 'ok';
        await page.locator('#body-preview summary').click();
        await page.locator('#content-language').selectOption('');
        await page.locator('#preview-button').click();
        await expect(page.locator('#preview h2')).toHaveText('Default body');
        assert.deepEqual(errors, []);
        console.log('PASS: preview expansion, current unsaved text, language switching, refresh, network/HTTP/login errors and recovery');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
