const separateAccounts = document.getElementById('UseSeparateAccounts');
const accountsDatabase = document.getElementById('accounts-database');
if (separateAccounts && accountsDatabase) {
    const update = () => {
        accountsDatabase.hidden = !separateAccounts.checked;
        accountsDatabase.disabled = !separateAccounts.checked;
    };
    separateAccounts.addEventListener('change', update);
    update();
}
