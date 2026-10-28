// Optional browser acceptance: set WWCMS_BROWSER_CHECKS=1 and make playwright available to Node.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const [baseUrl, theme, username] = process.argv.slice(2);
(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    const output = path.resolve('artifacts/feature-ui');
    fs.mkdirSync(output, { recursive: true });
    try {
        for (const timezoneId of ['UTC', 'Asia/Shanghai', 'America/Los_Angeles']) {
            const context = await browser.newContext({ timezoneId, viewport: { width: 1440, height: 1000 } });
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
        console.log('PASS: ' + theme + ' desktop/mobile UI, avatar links, icons, hover and three browser time zones');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
