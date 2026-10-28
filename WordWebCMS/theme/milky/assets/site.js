(() => {
    const root = document.documentElement;
    const appearance = document.querySelector('.appearance-toggle');
    const systemDark = matchMedia('(prefers-color-scheme: dark)');
    const isDark = () => root.dataset.appearance ? root.dataset.appearance === 'dark' : systemDark.matches;
    const updateAppearance = () => appearance.setAttribute('aria-pressed', String(isDark()));
    updateAppearance();
    systemDark.addEventListener('change', updateAppearance);
    appearance.addEventListener('click', () => {
        root.dataset.appearance = isDark() ? 'light' : 'dark';
        try { localStorage.setItem('milky-appearance', root.dataset.appearance); } catch { }
        updateAppearance();
    });

    const menu = document.querySelector('.menu-toggle');
    const nav = document.getElementById('primary-nav');
    const closeMenu = () => {
        menu.setAttribute('aria-expanded', 'false');
        nav.classList.remove('is-open');
    };
    menu.addEventListener('click', () => {
        const open = menu.getAttribute('aria-expanded') !== 'true';
        menu.setAttribute('aria-expanded', String(open));
        nav.classList.toggle('is-open', open);
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        const openDropdown = document.activeElement?.closest('details[open]');
        document.querySelectorAll('.dropdown[open]').forEach(item => item.open = false);
        if (openDropdown) openDropdown.querySelector('summary').focus();
        else if (menu.getAttribute('aria-expanded') === 'true') { closeMenu(); menu.focus(); }
    });
    document.addEventListener('click', event => {
        document.querySelectorAll('.dropdown[open]').forEach(item => {
            if (!item.contains(event.target)) item.open = false;
        });
        if (!event.target.closest('.header-inner')) closeMenu();
    });
    document.querySelector('.search-dropdown').addEventListener('toggle', event => {
        if (event.target.open) document.getElementById('header-q').focus();
    });
    document.querySelectorAll('.primary-nav a').forEach(link => {
        const url = new URL(link.href);
        if (url.origin === location.origin && url.pathname === location.pathname && url.search === location.search) {
            link.setAttribute('aria-current', 'page');
        }
    });
    document.querySelector('.language-nav select').addEventListener('change', event => event.target.form.requestSubmit());

    const ui = JSON.parse(document.getElementById('ui-text').textContent);
    const commentBody = document.getElementById('comment-body');
    document.querySelectorAll('[data-reply]').forEach(button => button.addEventListener('click', () => {
        document.getElementById('parent-id').value = button.dataset.reply;
        document.getElementById('reply-label').textContent = ui['正在回复评论 #'] + button.dataset.reply;
        document.getElementById('cancel-reply').hidden = false;
        commentBody.focus();
    }));
    document.getElementById('cancel-reply')?.addEventListener('click', event => {
        document.getElementById('parent-id').value = '';
        document.getElementById('reply-label').textContent = '';
        event.target.hidden = true;
        commentBody.focus();
    });

    document.querySelector('[data-share]')?.addEventListener('click', async event => {
        const button = event.currentTarget;
        const status = document.querySelector('.share-status');
        const field = document.querySelector('.share-link');
        // Share the current public address; configured canonical hosts can differ in previews.
        const url = new URL(location.pathname, location.origin).href;
        try {
            if (navigator.share) await navigator.share({ title: document.querySelector('h1').textContent, url });
            else { await navigator.clipboard.writeText(url); status.textContent = button.dataset.success; }
        } catch (error) {
            if (error.name === 'AbortError') return;
            field.hidden = false;
            field.value = url;
            field.focus();
            field.select();
            status.textContent = button.dataset.failure;
        }
    });
})();
