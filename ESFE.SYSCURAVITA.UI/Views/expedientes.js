let pacientesCache = [];
let pacienteTempSeleccionado = null;
let btnTempElement = null;

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);

    inicializarEventosNavegacion();
    actualizarEstadoBotones();
    aplicarValidacionesCampos();

    // Notificar a C# que la vista está lista para recibir pacientes
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ accion: "obtener_pacientes" }));
    }
});

window.addEventListener('focus', actualizarEstadoBotones);
window.addEventListener('storage', actualizarEstadoBotones);

// Listener para mensajes entrantes desde C# (WebView2)
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        let datos = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;

        const accion = datos.accion || datos.Accion;
        const listaPacientes = datos.pacientes || datos.Pacientes;

        if (accion === "CARGAR_PACIENTES" || accion === "EXPEDIENTE_GUARDADO") {
            if (listaPacientes) {
                renderizarTabla(listaPacientes);
            }
        } else if (accion === "REGISTRO_ELIMINADO") {
            const idBorrado = datos.pacienteId || datos.PacienteId;
            pacientesCache = pacientesCache.filter(p => (p.paciente_id || p.id) !== idBorrado);
            renderizarTabla(pacientesCache);

            let listaEspera = JSON.parse(localStorage.getItem('listaEspera') || '[]');
            listaEspera = listaEspera.filter(p => (p.paciente_id || p.id) !== idBorrado);
            localStorage.setItem('listaEspera', JSON.stringify(listaEspera));
        }
    });
}

function inicializarEventosNavegacion() {
    const menuExpedientes = document.getElementById('menuExpedientes');
    const menuConsulta = document.getElementById('menuConsulta');
    const menuPago = document.getElementById('menuPago');

    if (menuExpedientes) {
        menuExpedientes.onclick = (e) => {
            e.preventDefault();
            navegar('expedientes');
        };
    }
    if (menuConsulta) {
        menuConsulta.onclick = (e) => {
            e.preventDefault();
            navegar('consulta');
        };
    }
    if (menuPago) {
        menuPago.onclick = (e) => {
            e.preventDefault();
            navegar('pago');
        };
    }
}

function aplicarValidacionesCampos() {
    // Nombres y Apellidos solo letras y espacios
    const inputsSoloTexto = document.querySelectorAll('#inputNombres, #inputApellidos');
    inputsSoloTexto.forEach(input => {
        if (!input) return;
        input.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/[^a-zA-ZáéíóúÁÉÍÓÚñÑ\s]/g, '');
        });
    });

    // Máscara DUI (00000000-0)
    const inputDUI = document.getElementById('inputDUI');
    if (inputDUI) {
        inputDUI.addEventListener('input', (e) => {
            let val = e.target.value.replace(/\D/g, '');
            if (val.length > 8) {
                val = val.substring(0, 8) + '-' + val.substring(8, 9);
            }
            e.target.value = val;
        });
    }

    // Máscara Teléfono (0000-0000)
    const inputTel = document.getElementById('inputTelefono');
    if (inputTel) {
        inputTel.addEventListener('input', (e) => {
            let val = e.target.value.replace(/\D/g, '');
            if (val.length > 4) {
                val = val.substring(0, 4) + '-' + val.substring(4, 8);
            }
            e.target.value = val;
        });
    }

    // Input Búsqueda
    const searchInput = document.getElementById('searchInput');
    if (searchInput) {
        searchInput.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/[^a-zA-Z0-9áéíóúÁÉÍÓÚñÑ\s.,/()-]/g, '');
        });
    }
}

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
            fecha_nacimiento: p.fecha_nacimiento || p.FechaNacimiento || null
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

function handleCreate(e) {
    e.preventDefault();

    const inputTel = document.getElementById('inputTelefono');
    const inputFecha = document.getElementById('inputFechaNacimiento');

    const payload = {
        accion: "guardar_expediente",
        nombres: document.getElementById('inputNombres').value.trim(),
        apellidos: document.getElementById('inputApellidos').value.trim(),
        dui_documento: document.getElementById('inputDUI').value.trim(),
        telefono: inputTel ? inputTel.value.trim() : "",
        fecha_nacimiento: inputFecha ? inputFecha.value : null
    };

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(payload));
    }

    openSuccessModal("El expediente ha sido registrado correctamente.");

    const form = document.getElementById('createForm');
    if (form) form.reset();
}

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
    const partes = fechaNacimientoStr.split('T')[0].split('-');
    if (partes.length < 3) return 18;

    const nacimiento = new Date(partes[0], partes[1] - 1, partes[2]);
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
            accion: "crear_consulta",
            pacienteId: datosConsulta.paciente_id,
            especialidadId: datosConsulta.especialidad_id,
            motivoConsulta: especialidadNombre,
            montoConsulta: datosConsulta.monto_consulta,
            recepcionistaId: parseInt(localStorage.getItem('usuarioId') || '1')
        }));

        window.chrome.webview.postMessage(JSON.stringify({
            accion: "agregar_a_espera",
            paciente: datosConsulta
        }));
    }

    closeEspecialidadModal();
}

function navegar(accion) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            accion: "NAVEGAR",
            modulo: accion,
            rol: localStorage.getItem('usuarioRol') || 'admin'
        }));
    } else {
        if (accion === 'cerrarSesion' || accion === 'cerrar_sesion') {
            window.location.href = 'login.html';
        } else {
            window.location.href = `${accion}.html`;
        }
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
    navegar('cerrar_sesion');
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