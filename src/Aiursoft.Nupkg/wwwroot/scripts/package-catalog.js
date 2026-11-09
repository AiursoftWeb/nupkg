(() => {
    document.querySelectorAll('.package-icon-image').forEach(image => {
        const fallback = () => {
            image.hidden = true;
            image.closest('.package-icon').querySelector('.package-icon-fallback').hidden = false;
        };
        image.addEventListener('error', fallback);
        if (image.complete && image.naturalWidth === 0) fallback();
    });
    const tabs = document.querySelector('.package-tabs');
    const revealActiveTab = () => {
        const active = tabs?.querySelector('.nav-link.active');
        if (!active) return;
        const bounds = active.getBoundingClientRect();
        tabs.scrollLeft += bounds.left - tabs.getBoundingClientRect().left - (tabs.clientWidth - bounds.width) / 2;
    };
    revealActiveTab();
    tabs?.addEventListener('shown.bs.tab', revealActiveTab);
    const format = document.querySelector('#install-format');
    if (!format) return;
    format.addEventListener('change', () => {
        const command = document.querySelector('#install-command');
        command.querySelector('code').textContent = format.selectedOptions[0].dataset.command;
        command.querySelector('.copy-status').textContent = '';
    });
})();
