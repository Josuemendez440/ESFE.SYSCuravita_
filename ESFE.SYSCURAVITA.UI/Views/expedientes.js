let pacientesCache = [];
let pacienteTempSeleccionado = null;
let btnTempElement = null;

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);
    actualizarEstadoBotones();
});

window.addEventListener('focus', actualizarEstadoBotones);
window.addEventListener('storage', actualizarEstadoBotones);

// Renderizado de tabla
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

        const uid = `${codigo}_${dui}_${nombreCompleto}`;

        const enEspera = listaEspera.some(item => {
            const itemId = item.paciente_id || item.id;
            const itemUid = item.uid || `${item.codigo_expediente || item.codigo}_${item.dui_documento || item.dui || 'N/A'}_${item.nombreCompleto || item.nombres}`;
            return itemId === id || itemUid === uid;
        });

        const pData = {
            uid: uid,
            paciente_id: id,
            id: id,
            codigo_expediente: codigo,
            codigo: codigo,
            nombres: nombres,
            apellidos: apellidos,
            nombreCompleto: nombreCompleto,
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
        <td><b>${pData.nombreCompleto}</b></td>
        <td style="color: var(--text-muted);">${pData.dui_documento}</td>
        <td style="text-align: right;">
            <button class="btn-action" ${disabledAttr} onclick="abrirModalEspecialidad(this, ${pJson})">
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

// Modal y Especialidad
function abrirModalEspecialidad(btnElement, paciente) {
    pacienteTempSeleccionado = paciente;
    btnTempElement = btnElement;

    const edad = calcularEdad(paciente.fecha_nacimiento);

    document.getElementById('modalPacienteNombre').innerText = `${paciente.codigo_expediente} - ${paciente.nombreCompleto}`;
    document.getElementById('modalPacienteEdad').innerText = edad;

    evaluarEspecialidadPorEdad(edad);

    document.getElementById('modalEspecialidad').style.display = 'flex';
}

function calcularEdad(fechaNacimientoStr) {
    if (!fechaNacimientoStr) return 18;
    const nacimiento = new Date(fechaNacimientoStr);
    const hoy = new Date();
    let edad = hoy.getFullYear() - nacimiento.getFullYear();
    const mes = hoy.getMonth() - nacimiento.getMonth();

    if (mes < 0 || (mes === 0 && hoy.getDate() < nacimiento.getDate())) {
        edad--;
    }
    return edad < 0 ? 0 : edad;
}

function evaluarEspecialidadPorEdad(edad) {
    const selectEsp = document.getElementById('selectEspecialidad');
    if (!selectEsp) return;

    const opcionPediatria = selectEsp.querySelector('option[value="2"]');

    if (edad < 12) {
        if (opcionPediatria) opcionPediatria.disabled = false;
        selectEsp.value = "2";
    } else {
        if (opcionPediatria) opcionPediatria.disabled = true;
        selectEsp.value = "1";
    }

    actualizarMontoVista();
}

function actualizarMontoVista() {
    const select = document.getElementById('selectEspecialidad');
    if (!select) return;
    const opcion = select.options[select.selectedIndex];
    const precio = opcion ? opcion.dataset.precio : "25.00";
    document.getElementById('lblMontoPreview').innerText = `$${parseFloat(precio).toFixed(2)}`;
}

function closeEspecialidadModal() {
    document.getElementById('modalEspecialidad').style.display = 'none';
    pacienteTempSeleccionado = null;
    btnTempElement = null;
}

// Confirmación de Cita y Envío
function confirmarEnviarAConsulta() {
    if (!pacienteTempSeleccionado) return;

    const select = document.getElementById('selectEspecialidad');
    const opcionSeleccionada = select.options[select.selectedIndex];
    const especialidadNombre = opcionSeleccionada.text.split('(')[0].trim();
    const montoConsulta = parseFloat(opcionSeleccionada.dataset.precio);

    const datosConsulta = {
        ...pacienteTempSeleccionado,
        paciente_id: pacienteTempSeleccionado.paciente_id,
        id: pacienteTempSeleccionado.paciente_id,
        especialidad_id: parseInt(select.value),
        especialidad_nombre: especialidadNombre,
        monto_consulta: montoConsulta,
        edad: document.getElementById('modalPacienteEdad').innerText || 'N/A'
    };

    let listaEspera = JSON.parse(localStorage.getItem('listaEspera') || '[]');
    const yaExiste = listaEspera.some(item => (item.paciente_id || item.id) === datosConsulta.paciente_id);

    if (!yaExiste) {
        listaEspera.push(datosConsulta);
        localStorage.setItem('listaEspera', JSON.stringify(listaEspera));
    }

    if (btnTempElement) {
        btnTempElement.disabled = true;
        btnTempElement.style.opacity = '0.6';
        btnTempElement.style.cursor = 'not-allowed';
        btnTempElement.innerHTML = `
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
            En Espera
        `;
    }

    const banner = document.getElementById('selectionBanner');
    const text = document.getElementById('selectedPatientText');
    if (banner && text) {
        text.textContent = `Paciente enviado a espera: ${datosConsulta.codigo_expediente} - ${datosConsulta.nombreCompleto} (${especialidadNombre})`;
        banner.style.display = 'flex';
    }

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "CREAR_CONSULTA",
            paciente_id: datosConsulta.paciente_id,
            especialidad_id: datosConsulta.especialidad_id,
            monto_consulta: datosConsulta.monto_consulta,
            recepcionista_id: parseInt(localStorage.getItem('usuarioId') || '1')
        }));

        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "AGREGAR_A_ESPERA",
            Paciente: datosConsulta
        }));
    }

    closeEspecialidadModal();
}

// Creación de Expediente
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

    openSuccessModal("El expediente ha sido registrado correctamente.");
    const form = document.getElementById('createForm');
    if (form) form.reset();
}

// Navegación y Utilidades
function navegar(accion) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(accion);
    }
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