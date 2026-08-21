document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);
});

// 1. Invocado directamente desde C#
function renderizarTabla(pacientes) {
    const tbody = document.getElementById('patientsTable').getElementsByTagName('tbody')[0];
    tbody.innerHTML = '';

    if (!pacientes || pacientes.length === 0) return;

    pacientes.forEach(p => {
        const codigo = p.codigo || p.Codigo || `PAC-0${p.id || p.Id || 0}`;
        const nombreCompleto = p.nombreCompleto || `${p.nombres || p.Nombres || ''} ${p.apellidos || p.Apellidos || ''}`.trim();
        const dui = p.dui_documento || p.Dui_documento || p.dui || 'N/A';

        agregarFilaTabla(codigo, nombreCompleto, dui);
    });
}

function agregarFilaTabla(code, name, dui) {
    const table = document.getElementById('patientsTable').getElementsByTagName('tbody')[0];
    const newRow = table.insertRow();

    newRow.innerHTML = `
        <td><b style="color: var(--primary);">${code}</b></td>
        <td><b>${name}</b></td>
        <td style="color: var(--text-muted);">${dui}</td>
        <td style="text-align: right;">
            <button class="btn-action" onclick="selectPatient('${code}', '${name}')">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
                Seleccionar
            </button>
        </td>
    `;
}

// 2. Envía objeto JSON para guardar expediente
function handleCreate(e) {
    e.preventDefault();

    const inputTel = document.getElementById('inputTelefono');

    const payload = {
        accion: "guardar_expediente",
        nombres: document.getElementById('inputNombres').value,
        apellidos: document.getElementById('inputApellidos').value,
        dui_documento: document.getElementById('inputDUI').value,
        telefono: inputTel ? inputTel.value : ""
    };

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(payload));
    }
}

// 3. Selección y envío a lista de espera
function selectPatient(code, name) {
    const banner = document.getElementById('selectionBanner');
    const text = document.getElementById('selectedPatientText');

    if (banner && text) {
        text.textContent = `Paciente agregado a espera: ${code} - ${name}`;
        banner.style.display = 'flex';
    }

    let listaEspera = JSON.parse(localStorage.getItem('listaEspera') || '[]');

    if (!listaEspera.some(p => (p.codigo_expediente || p.codigo) === code)) {
        listaEspera.push({
            paciente_id: Date.now(),
            codigo_expediente: code,
            nombres: name,
            apellidos: '',
            fecha_nacimiento: null
        });
        localStorage.setItem('listaEspera', JSON.stringify(listaEspera));
    }

    setTimeout(() => {
        navegar('nav_consulta');
    }, 400);
}

// 4. Comandos simples hacia C#
function navegar(accion) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(accion);
    }
}

function establecerUsuario(rol) {
    localStorage.setItem('usuarioRol', rol);
    aplicarPermisos(rol);
}

// 5. Filtrado local
function filterTable() {
    const input = document.getElementById('searchInput').value.toLowerCase();
    const table = document.getElementById('patientsTable');
    const trs = table.getElementsByTagName('tr');

    for (let i = 1; i < trs.length; i++) {
        const rowText = trs[i].textContent.toLowerCase();
        trs[i].style.display = rowText.includes(input) ? '' : 'none';
    }
}

// 6. Control de Modales
function openLogoutModal() { document.getElementById('logoutModal').style.display = 'flex'; }
function closeLogoutModal() { document.getElementById('logoutModal').style.display = 'none'; }
function confirmLogout() {
    localStorage.removeItem('usuarioRol');
    navegar('cerrarSesion');
}

function openSuccessModal(mensaje) {
    if (mensaje) document.getElementById('successModalText').textContent = mensaje;
    document.getElementById('successModal').style.display = 'flex';
}
function closeSuccessModal() { document.getElementById('successModal').style.display = 'none'; }

// 7. Permisos por Rol
function aplicarPermisos(rol) {
    const menuExpedientes = document.getElementById('menuExpedientes');
    const menuConsulta = document.getElementById('menuConsulta');
    const menuPago = document.getElementById('menuPago');

    if (menuExpedientes) menuExpedientes.style.display = '';
    if (menuConsulta) menuConsulta.style.display = '';
    if (menuPago) menuPago.style.display = '';

    const rolNorm = (rol || '').toLowerCase().trim();

    if (rolNorm === 'admin' || rolNorm === 'administrador') return;

    if (rolNorm === 'doctor' || rolNorm === 'medico') {
        if (menuExpedientes) menuExpedientes.style.display = 'none';
        if (menuPago) menuPago.style.display = 'none';
    }
    else if (rolNorm === 'expedientes' || rolNorm === 'recepcion' || rolNorm.includes('recepcion')) {
        if (menuConsulta) menuConsulta.style.display = 'none';
        if (menuPago) menuPago.style.display = 'none';
    }
    else if (rolNorm === 'caja') {
        if (menuExpedientes) menuExpedientes.style.display = 'none';
        if (menuConsulta) menuConsulta.style.display = 'none';
    }
}