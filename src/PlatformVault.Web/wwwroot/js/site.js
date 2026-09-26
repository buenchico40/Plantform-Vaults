// PlatformVault.Web · comportamiento de cliente.
// Nunca se guardan valores sensibles, tokens ni la API Key en el navegador (sin localStorage/sessionStorage).
(function () {
    'use strict';

    const pv = window.pv = window.pv || {};

    function antiforgery() {
        const meta = document.querySelector('meta[name="pv-af"]');
        return meta ? meta.getAttribute('content') : '';
    }

    async function postJson(url, body) {
        const response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiforgery(), 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify(body)
        });
        const text = await response.text();
        const data = text ? JSON.parse(text) : {};
        if (!response.ok) {
            const error = new Error(data.message || 'No se pudo completar la operación.');
            error.code = data.code;
            error.status = response.status;
            throw error;
        }
        return data;
    }

    // Celdas del inventario: enlace al detalle y marca de objeto crítico (RN-107).
    pv.objectLinkCell = function (container, options) {
        const a = document.createElement('a');
        a.href = '/Objects/Details/' + encodeURIComponent(options.data.id || options.data.Id);
        a.textContent = options.value;
        container.append(a);
        if ((options.data.criticality || options.data.Criticality) === 'Crítico') {
            const badge = document.createElement('span');
            badge.className = 'pv-badge pv-critical';
            badge.textContent = 'CRÍTICO';
            container.append(badge);
        }
    };

    pv.linkCell = function (prefix) {
        return function (container, options) {
            const a = document.createElement('a');
            a.href = prefix + encodeURIComponent(options.data.id || options.data.Id);
            a.textContent = options.value;
            container.append(a);
        };
    };

    pv.dateCell = function (container, options) {
        container.text(options.value ? new Date(options.value).toLocaleString() : '—');
    };

    // US-016: revelado efímero. Se pide la contraseña (re-autenticación), se muestra el valor 30 s y se borra.
    function setupReveal() {
        document.querySelectorAll('[data-reveal]').forEach(function (button) {
            button.addEventListener('click', async function () {
                const panel = document.getElementById('pv-reveal-panel');
                const password = document.getElementById('pv-reveal-password');
                const output = document.getElementById('pv-reveal-output');
                const status = document.getElementById('pv-reveal-status');
                if (!password.value) { status.textContent = 'Confirme su contraseña para revelar.'; return; }
                button.disabled = true;
                status.textContent = 'Verificando…';
                try {
                    const data = await postJson('/Objects/Reveal/' + encodeURIComponent(button.dataset.reveal), { password: password.value });
                    password.value = '';
                    output.value = data.value;
                    panel.classList.add('pv-revealed');
                    let remaining = data.expiresInSeconds || 30;
                    status.textContent = 'Se ocultará en ' + remaining + ' s.';
                    const timer = setInterval(function () {
                        remaining -= 1;
                        status.textContent = 'Se ocultará en ' + remaining + ' s.';
                        if (remaining <= 0) {
                            clearInterval(timer);
                            output.value = '';
                            panel.classList.remove('pv-revealed');
                            status.textContent = 'Valor ocultado.';
                            button.disabled = false;
                        }
                    }, 1000);
                    data.value = null;
                } catch (e) {
                    password.value = '';
                    status.textContent = e.message;
                    button.disabled = false;
                    if (e.status === 401 && e.code === 'SESSION_INVALID') { window.location.href = '/Account/Login'; }
                }
            });
        });
        const copy = document.getElementById('pv-reveal-copy');
        if (copy) {
            copy.addEventListener('click', async function () {
                const output = document.getElementById('pv-reveal-output');
                if (output.value && navigator.clipboard) {
                    await navigator.clipboard.writeText(output.value);
                    document.getElementById('pv-reveal-status').textContent = 'Copiado. Limpie el portapapeles al terminar.';
                }
            });
        }
    }

    // Selector de usuarios: busca en /Users/Lookup y llena el <select> asociado.
    function setupUserPickers() {
        document.querySelectorAll('[data-user-picker]').forEach(function (input) {
            const select = document.getElementById(input.dataset.userPicker);
            let handle;
            input.addEventListener('input', function () {
                clearTimeout(handle);
                handle = setTimeout(async function () {
                    const response = await fetch('/Users/Lookup?text=' + encodeURIComponent(input.value), { credentials: 'same-origin', cache: 'no-store' });
                    if (!response.ok) { return; }
                    const users = await response.json();
                    const current = select.value;
                    select.innerHTML = '';
                    const empty = document.createElement('option');
                    empty.value = '';
                    empty.textContent = '— seleccione —';
                    select.append(empty);
                    users.forEach(function (u) {
                        const option = document.createElement('option');
                        option.value = u.id;
                        option.textContent = u.text;
                        if (u.id === current) { option.selected = true; }
                        select.append(option);
                    });
                }, 250);
            });
        });
    }

    // Confirmación de acciones críticas.
    function setupConfirmations() {
        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (!window.confirm(form.dataset.confirm)) { event.preventDefault(); }
            });
        });
    }

    // Formulario de alta: muestra los campos del valor según el tipo.
    function setupTypeFields() {
        const type = document.getElementById('Form_Type') || document.getElementById('value-type');
        if (!type) { return; }
        const apply = function () {
            const t = type.value;
            document.querySelectorAll('[data-for-type]').forEach(function (el) {
                el.hidden = el.dataset.forType.split(',').indexOf(t) < 0;
            });
            const subtype = document.getElementById('Form_Subtype');
            if (subtype) {
                Array.from(subtype.options).forEach(function (o) { o.hidden = o.dataset.type !== t; });
                if (subtype.selectedOptions.length && subtype.selectedOptions[0].hidden) {
                    const first = Array.from(subtype.options).find(function (o) { return !o.hidden; });
                    if (first) { subtype.value = first.value; }
                }
            }
        };
        type.addEventListener('change', apply);
        apply();
    }

    document.addEventListener('DOMContentLoaded', function () {
        if (window.DevExpress && DevExpress.localization) { DevExpress.localization.locale('es'); }
        setupReveal();
        setupUserPickers();
        setupConfirmations();
        setupTypeFields();
    });
})();
