(() => {
    const pad = value => String(value).padStart(2, '0');
    const formatLocal = date =>
        `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;

    // Index page: live search, status filter, and sorting.
    const table = document.getElementById('api-keys-table');
    if (table) {
        const tbody = table.querySelector('tbody');
        const rows = Array.from(tbody.querySelectorAll('tr[data-key-row]'));
        const searchInput = document.getElementById('api-keys-search');
        const sortSelect = document.getElementById('api-keys-sort');
        const filterButtons = Array.from(document.querySelectorAll('[data-key-filter]'));
        const emptyRow = document.getElementById('api-keys-empty-filter');
        const clearBtn = document.getElementById('api-keys-clear-filters');
        const countBadge = document.getElementById('api-keys-visible-count');
        let activeFilter = 'all';

        const compareRows = (a, b, mode) => {
            const num = (el, attr) => Number(el.getAttribute(attr) || '0');
            switch (mode) {
                case 'expires-asc':
                    return num(a, 'data-expires') - num(b, 'data-expires');
                case 'calls-desc':
                    return num(b, 'data-calls') - num(a, 'data-calls');
                case 'used-desc':
                    return num(b, 'data-used') - num(a, 'data-used');
                default:
                    return num(b, 'data-created') - num(a, 'data-created');
            }
        };

        const apply = () => {
            const query = (searchInput?.value || '').trim().toLowerCase();
            const sortMode = sortSelect?.value || 'created-desc';
            const sorted = [...rows].sort((a, b) => compareRows(a, b, sortMode));
            let visible = 0;

            for (const row of sorted) {
                const status = row.getAttribute('data-status') || '';
                const haystack = (row.getAttribute('data-search') || '').toLowerCase();
                const statusMatch = activeFilter === 'all'
                    || (activeFilter === 'active' && (status === 'active' || status === 'expiring'))
                    || status === activeFilter;
                const queryMatch = !query || haystack.includes(query);
                const show = statusMatch && queryMatch;
                row.hidden = !show;
                tbody.appendChild(row);
                if (show) visible++;
            }

            if (emptyRow) {
                tbody.appendChild(emptyRow);
                emptyRow.hidden = visible > 0;
            }
            if (countBadge) {
                countBadge.textContent = String(visible);
            }
        };

        filterButtons.forEach(button => {
            button.addEventListener('click', () => {
                activeFilter = button.getAttribute('data-key-filter') || 'all';
                filterButtons.forEach(btn => {
                    const selected = btn === button;
                    btn.classList.toggle('active', selected);
                    btn.setAttribute('aria-pressed', selected ? 'true' : 'false');
                });
                apply();
            });
        });

        searchInput?.addEventListener('input', apply);
        sortSelect?.addEventListener('change', apply);
        clearBtn?.addEventListener('click', () => {
            if (searchInput) searchInput.value = '';
            activeFilter = 'all';
            filterButtons.forEach(btn => {
                const selected = btn.getAttribute('data-key-filter') === 'all';
                btn.classList.toggle('active', selected);
                btn.setAttribute('aria-pressed', selected ? 'true' : 'false');
            });
            apply();
            searchInput?.focus();
        });
    }

    // Edit page: quick-set expiration presets (capped at 100 years).
    const expireInput = document.querySelector('input[data-utc-input][data-quick-expire-target]');
    const presetButtons = Array.from(document.querySelectorAll('[data-set-expire-days]'));
    if (expireInput && presetButtons.length > 0) {
        presetButtons.forEach(button => {
            button.addEventListener('click', () => {
                const days = Number(button.getAttribute('data-set-expire-days'));
                if (!Number.isFinite(days) || days < 1) return;
                const target = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
                expireInput.value = formatLocal(target);
                expireInput.dispatchEvent(new Event('input', { bubbles: true }));
                expireInput.dispatchEvent(new Event('change', { bubbles: true }));
                presetButtons.forEach(btn => btn.classList.toggle('active', btn === button));
            });
        });

        expireInput.addEventListener('input', event => {
            if (event.isTrusted) {
                presetButtons.forEach(btn => btn.classList.remove('active'));
            }
        });
    }
})();
