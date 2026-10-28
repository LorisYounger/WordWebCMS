// Optional browser acceptance: set WWCMS_BROWSER_CHECKS=1 and make playwright available to Node.
const { chromium } = require('playwright');
const { expect } = require('playwright/test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const [baseUrl, theme, username, postId] = process.argv.slice(2);
(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    const output = path.resolve('artifacts/feature-ui');
    fs.mkdirSync(output, { recursive: true });
    try {
        for (const timezoneId of ['UTC', 'Asia/Shanghai', 'America/Los_Angeles']) {
            const context = await browser.newContext({ timezoneId, viewport: { width: 1440, height: 1000 } });
            await context.route(/^https:\/\/(example\.test|avatar\.exlb\.net)\//, route => route.fulfill({ contentType: 'image/png', path: 'WordWebCMS/wwwroot/WWC.png' }));
            const page = await context.newPage();
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            await page.goto(baseUrl + '/feature-article-0?culture=en', { waitUntil: 'domcontentloaded' });
            await page.locator('time[data-local-time]').first().waitFor();
            const date = page.locator('.article-header time').first();
            await page.waitForFunction(() => document.querySelector('.article-header time')?.title);
            const expected = new Intl.DateTimeFormat('en', { timeZone: timezoneId, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date('2026-10-28T23:30:00Z'));
            assert.equal(await date.textContent(), expected, theme + ': browser-local date in ' + timezoneId);
            await page.locator('.brand').first().hover();
            assert.equal(await page.locator('.brand').first().evaluate(el => getComputedStyle(el).textDecorationLine), 'none');
            await page.evaluate(() => document.fonts.load('16px remixicon'));
            assert.ok(await page.evaluate(() => document.fonts.check('16px remixicon')));
            const author = page.locator('.article-header .author-link');
            assert.equal(await author.locator('img').count(), 1);
            assert.ok(await author.locator('img').evaluate(el => el.getBoundingClientRect().width <= 32));
            if (timezoneId === 'Asia/Shanghai') {
                await page.screenshot({ path: path.join(output, theme + '-article-desktop.png'), fullPage: true });
                const articleUrl = page.url();
                await page.goto(baseUrl + '/?culture=zh-Hans');
                if (theme === 'default') {
                    const edges = await page.evaluate(() => ({
                        brand: document.querySelector('.brand').getBoundingClientRect().left,
                        main: document.querySelector('main').getBoundingClientRect().left,
                        nav: document.querySelector('.site-nav > a, .site-nav > .nav-group > a').getBoundingClientRect().left,
                        heading: document.querySelector('.section-heading').getBoundingClientRect().top,
                        sidebar: document.querySelector('.sidebar h2').getBoundingClientRect().top
                    }));
                    assert.ok(Math.abs(edges.brand - edges.main) < 1 && Math.abs(edges.nav - edges.main) < 1, 'Header, navigation and content edges align');
                    assert.ok(Math.abs(edges.heading - edges.sidebar) < 1, 'Main heading and sidebar align');
                }
                await page.screenshot({ path: path.join(output, theme + '-home-desktop.png'), fullPage: true });
                await page.setViewportSize({ width: 390, height: 844 });
                assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), theme + ': homepage fits mobile');
                await page.screenshot({ path: path.join(output, theme + '-home-mobile.png'), fullPage: true });
                await page.setViewportSize({ width: 1440, height: 1000 });
                await page.goto(articleUrl);
                await author.click();
                await page.waitForURL('**/author/' + username);
                assert.equal(await page.locator('.profile-card h1').textContent(), username);
                assert.equal(await page.locator('.profile-bio script').count(), 0);
                await page.setViewportSize({ width: 390, height: 844 });
                for (const route of ['/author/' + username, '/feature-article-0']) {
                    await page.goto(baseUrl + route, { waitUntil: 'domcontentloaded' });
                    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), theme + ': no horizontal overflow on ' + route);
                    await page.screenshot({ path: path.join(output, theme + (route.startsWith('/author') ? '-profile' : '-article') + '-mobile.png'), fullPage: true });
                }
            }
            assert.deepEqual(errors, []);
            await context.close();
        }
        const page = await browser.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.goto(baseUrl + '/Account/Login?culture=en');
        await page.locator('#Login').fill(username);
        await page.locator('#Password').fill(process.env.WWCMS_TEST_PASSWORD);
        const numbers = (await page.locator('label[for=ChallengeAnswer]').textContent()).match(/\d+/g).map(Number);
        await page.locator('#ChallengeAnswer').fill(String(numbers[0] + numbers[1]));
        await page.locator('button').filter({ hasText: 'Sign in' }).click();
        await page.waitForURL(baseUrl + '/');
        await page.goto(baseUrl + '/Admin/Settings?culture=en');
        const footerBefore = await page.locator('#Settings_FooterMarkdown').inputValue();
        await page.locator('#Settings_HideUserAvatars').check();
        await page.locator('#Settings_FooterMarkdown').fill('Browser footer [Contact](/contact)');
        await page.getByRole('button', { name: 'Save settings', exact: true }).click();
        await expect(page.locator('.notice').first()).toBeVisible();
        await page.goto(baseUrl + '/?culture=zh-Hans');
        await expect(page.locator('.author-link img')).toHaveCount(0);
        await expect(page.locator('.footer-content')).toContainText('Browser footer');
        await page.goto(baseUrl + '/Admin/Settings?culture=en');
        await page.locator('#Settings_HideUserAvatars').uncheck();
        await page.locator('#Settings_FooterMarkdown').fill(footerBefore);
        await page.getByRole('button', { name: 'Save settings', exact: true }).click();
        for (const width of [1440, 390]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const route of ['/Admin/Settings', '/Admin/Users']) {
                await page.goto(baseUrl + route + '?culture=en');
                assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), route + ' fits ' + width);
                await page.screenshot({ path: path.join(output, theme + '-' + route.split('/').pop().toLowerCase() + '-' + width + '.png'), fullPage: true });
            }
        }
        await page.setViewportSize({ width: 1440, height: 1000 });
        page.once('dialog', dialog => dialog.dismiss());
        await page.getByRole('button', { name: 'Remove from site', exact: true }).first().click();
        assert.ok(page.url().includes('/Admin/Users'));

        await page.goto(baseUrl + '/Admin/Edit/' + postId + '?culture=en');
        // Set unsaved values without triggering the separate autosave feature.
        await page.locator('#Draft_Markdown').evaluate(el => { el.value = '## Browser preview'; });
        await page.locator('#body-preview summary').click();
        await expect(page.locator('#preview h2')).toHaveText('Browser preview');
        await page.locator('#translation-body-en').evaluate(el => { el.value = '**Translated preview**'; });
        await page.locator('#content-language').selectOption('en');
        await expect(page.locator('#preview strong')).toHaveText('Translated preview');
        await page.locator('#translation-body-en').evaluate(el => { el.value = '**Updated preview**'; });
        await page.locator('[data-content-language="en"] .translation-preview').click();
        await expect(page.locator('#preview strong')).toHaveText('Updated preview');
        await page.route('**/*handler=Preview', route => route.abort());
        await page.locator('[data-content-language="en"] .translation-preview').click();
        await expect(page.locator('#preview')).toContainText('Preview failed');
        await page.unroute('**/*handler=Preview');
        await page.locator('#body-preview summary').click();
        await page.locator('#content-language').selectOption('');
        await page.locator('#preview-button').click();
        await expect(page.locator('#preview h2')).toHaveText('Browser preview');
        await page.screenshot({ path: path.join(output, theme + '-editor-preview.png'), fullPage: true });
        assert.deepEqual(errors, []);
        await page.close();
        console.log('PASS: ' + theme + ' desktop/mobile UI, avatars, time zones and editor preview open/update/language/error recovery');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
