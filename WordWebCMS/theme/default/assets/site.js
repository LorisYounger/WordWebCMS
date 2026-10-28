document.querySelectorAll('.language-nav select[name=culture]').forEach(select => select.addEventListener('change', () => select.form.requestSubmit()));
const ui = JSON.parse(document.getElementById('ui-text').textContent);
document.querySelectorAll('[data-reply]').forEach(button => button.addEventListener('click', () => {
    document.getElementById('parent-id').value = button.dataset.reply;
    document.getElementById('reply-label').textContent = ui['正在回复评论 #'] + button.dataset.reply;
    document.getElementById('cancel-reply').hidden = false;
    document.getElementById('body').focus();
}));
document.getElementById('cancel-reply')?.addEventListener('click', () => {
    document.getElementById('parent-id').value = '';
    document.getElementById('reply-label').textContent = '';
    document.getElementById('cancel-reply').hidden = true;
});
