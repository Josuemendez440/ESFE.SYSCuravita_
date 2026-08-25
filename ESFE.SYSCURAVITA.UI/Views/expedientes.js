let pacientesCache = [];

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);
    actualizarEstadoBotones();
});

// Refresca el estado de los botones cuando la pestaña o vista recupera el foco
window.addEventListener('focus', actualizarEstadoBotones);

// 1. Renderizado de tabla e identificación individual
function renderizarTabla(pacientes) {
    if (pacientes) pacientesCache = pacientes;
    const table = document.getElementById('patientsTable');
    if (!table) return;
    const tbody = table.getElementsByTagName('tbody')[0];
    if (!tbody) return;

    tbody.innerHTML = '';
    if (!pacientesCache || pacientesCache.length === 0) return;

    const listaEspera = JSON.parse(localStorage.getItem('listaEspera') || '[]');

    pacientesCache.forEach((p, index) => {
        const id = p.paciente_id || p.id || p.PacienteId || p.Id || (index + 1);
        const codigo = p.codigo_expediente || p.codigo || p.Codigo || `PAC-0${id}`;
        const nombres = p.nombres || p.Nombres || '';
        const apellidos = p.apellidos || p.Apellidos || '';
        const nombreCompleto = p.nombreCompleto || `${nombres} ${apellidos}`.trim() || 'Sin Nombre';
        const dui = p.dui_documento || p.Dui_documento || p.dui || p.Dui || 'N/A';

        // Identificador único compuesto para diferenciar pacientes aunque repitan código
        const uid = `${codigo}_${dui}_${nombreCompleto}`;

        // Determinar si ESTE paciente específico ya está en la cola de espera
        const enEspera = listaEspera.some(item => {
            const itemUid = item.uid || `${item.codigo_expediente || item.codigo}_${item.dui_documento || item.dui || 'N/A'}_${item.nombres}`;
            return itemUid === uid;
        });

        const pData = {
            uid: uid,
            paciente_id: id,
            codigo_expediente: codigo,
            nombres: nombreCompleto,
            apellidos: apellidos,
            dui_documento: dui,
            fecha_nacimiento: p.fecha_nacimiento || null
        };

        agregarFilaTabla(tbody, pData, enEspera);
    });
}

function agregarFilaTabla(tbody, pData, enEspera) {
    const newRow = tbody.insertRow();
    const disabledAttr = enEspera ? 'disabled style="opacity: 0.6; cursor: not-allowed;"' : '';
    const btnText = enEspera ? 'En Espera' : 'Seleccionar';
    const pJson = JSON.stringify(pData).replace(/"/g, '&quot;');

    newRow.innerHTML = `
        <td><b style="color: var(--primary);">${pData.codigo_expediente}</b></td>
        <td><b>${pData.nombres}</b></td>
        <td style="color: var(--text-muted);">${pData.dui_documento}</td>
        <td style="text-align: right;">
            <button class="btn-action" ${disabledAttr} onclick="selectPatient(this, ${pJson})">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
                ${btnText}
            </button>
        </td>
    `;
}

function actualizarEstadoBotones() {
    if (pacientesCache && pacientesCache.length > 0) {
        renderizarTabla(pacientesCache);
    }
}

// 2. Selección individual sin cambiar de vista (permite seleccionar múltiples)
function selectPatient(btnElement, paciente) {
    const banner = document.getElementById('selectionBanner');
    const text = document.getElementById('selectedPatientText');

    if (banner && text) {
        text.textContent = `Paciente agregado a espera: ${paciente.codigo_expediente} - ${paciente.nombres}`;
        banner.style.display = 'flex';
    }

    let listaEspera = JSON.parse(localStorage.getItem('listaEspera') || '[]');

    const yaExiste = listaEspera.some(item => item.uid === paciente.uid);

    if (!yaExiste) {
        listaEspera.push(paciente);
        localStorage.setItem('listaEspera', JSON.stringify(listaEspera));
    }

    // Deshabilitar inmediatamente solo el botón seleccionado
    if (btnElement) {
        btnElement.disabled = true;
        btnElement.style.opacity = '0.6';
        btnElement.style.cursor = 'not-allowed';
        btnElement.innerHTML = `
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
            En Espera
        `;
    }
}

// 3. Envío para crear expediente
function handleCreate(e) {
    e.preventDefault();

    const inputTel = document.getElementById('inputTelefono');
    const inputFecha = document.getElementById('inputFechaNacimiento');

    const payload = {
        accion: "guardar_expediente",
        nombres: document.getElementById('inputNombres').value,
        apellidos: document.getElementById('inputApellidos').value,
        dui_documento: document.getElementById('inputDUI').value,
        telefono: inputTel ? inputTel.value : "",
        fecha_nacimiento: inputFecha ? inputFecha.value : null
    };

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(payload));
    }
}

// 4. Utilidades generales
function navegar(accion) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(accion);
    }
}

function establecerUsuario(rol) {
    localStorage.setItem('usuarioRol', rol);
    aplicarPermisos(rol);
}

function filterTable() {
    const input = document.getElementById('searchInput').value.toLowerCase();
    const table = document.getElementById('patientsTable');
    if (!table) return;

    const trs = table.getElementsByTagName('tr');
    for (let i = 1; i < trs.length; i++) {
        const rowText = trs[i].textContent.toLowerCase();
        trs[i].style.display = rowText.includes(input) ? '' : 'none';
    }
}

// Control de Modales
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

// Permisos por Rol
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