// Run before the stylesheet so a saved appearance does not flash on navigation.
document.documentElement.classList.add('js');
try {
    const saved = localStorage.getItem('milky-appearance');
    if (saved === 'light' || saved === 'dark') document.documentElement.dataset.appearance = saved;
} catch { /* Browsing remains available when storage is disabled. */ }
