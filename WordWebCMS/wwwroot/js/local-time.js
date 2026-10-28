document.querySelectorAll('time[data-local-time][datetime]').forEach(element => {
    const date = new Date(element.getAttribute('datetime'));
    if (Number.isNaN(date.getTime())) return;
    const options = element.dataset.localTime === 'date'
        ? { year: 'numeric', month: '2-digit', day: '2-digit' }
        : { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' };
    element.textContent = new Intl.DateTimeFormat(document.documentElement.lang || undefined, options).format(date);
    element.title = new Intl.DateTimeFormat(document.documentElement.lang || undefined, { dateStyle: 'full', timeStyle: 'long' }).format(date);
});
// GET forms also work without JavaScript; the server redirects old query URLs.
document.querySelectorAll('form[method="get"][action="/"]').forEach(form => {
    if (!form.elements.q) return;
    form.addEventListener('submit', event => {
        event.preventDefault();
        const query = form.elements.q.value.trim();
        location.assign(query ? '/search/' + encodeURIComponent(query) : '/');
    });
});
