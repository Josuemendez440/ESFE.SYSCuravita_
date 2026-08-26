let pacientePendiente = null;
let pacienteSeleccionadoId = 0;
let listaReceta = [];

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);
    cargarListaEsperaLocal();
    renderizarTablaReceta();
    actualizarEstadoVista();
});

// Refrescar lista al cambiar de pestaña o enfoque
window.addEventListener('focus', cargarListaEsperaLocal);
window.addEventListener('storage', cargarListaEsperaLocal);

// Receptor de mensajes WebView2 (C#)
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        const datos = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (datos.Accion === "CARGAR_HISTORIAL") {
            renderizarHistorial(datos.Historial);
        } else if (datos.Accion === "CARGAR_LISTA_ESPERA") {
            const listaServidor = datos.Pacientes || [];
            let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');

            // Combinar servidor y localStorage evitando duplicados por ID
            listaServidor.forEach(p => {
                const pId = p.paciente_id || p.id;
                if (!listaLocal.some(item => (item.paciente_id || item.id) === pId)) {
                    listaLocal.push(p);
                }
            });

            listaLocal = filtrarListaEspera(listaLocal);
            localStorage.setItem('listaEspera', JSON.stringify(listaLocal));
            renderizarListaEspera(listaLocal);
        } else if (datos.Accion === "CONSULTA_GUARDADA") {
            if (datos.Exito) {
                mostrarToast("Consulta procesada y agregada al historial", "exito");
            } else {
                mostrarToast("Error al guardar la consulta en la base de datos", "error");
            }
        }
    });
}

function mostrarToast(mensaje, tipo = "exito") {
    let toast = document.createElement("div");
    toast.className = `toast-notificacion ${tipo}`;
    toast.innerText = mensaje;

    document.body.appendChild(toast);

    setTimeout(() => { toast.classList.add("mostrar"); }, 100);
    setTimeout(() => {
        toast.classList.remove("mostrar");
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function actualizarEstadoVista() {
    const emptyWorkspace = document.getElementById('emptyWorkspace');
    const workspacePanel = document.getElementById('workspacePanel');

    if (pacienteSeleccionadoId && pacientePendiente) {
        if (emptyWorkspace) emptyWorkspace.style.display = 'none';
        if (workspacePanel) workspacePanel.style.display = 'flex';
    } else {
        if (emptyWorkspace) emptyWorkspace.style.display = 'flex';
        if (workspacePanel) workspacePanel.style.display = 'none';
    }
}

function filtrarListaEspera(lista) {
    return (lista || []).filter(p => {
        if (!p) return false;
        const codigo = p.codigo_expediente || p.codigo || '';
        return codigo !== 'PAC-00' && codigo !== '' && codigo !== 'N/A';
    });
}

function cargarListaEsperaLocal() {
    let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');
    listaLocal = filtrarListaEspera(listaLocal);

    renderizarListaEspera(listaLocal);

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ Accion: "OBTENER_PACIENTES" }));
    }
}

function renderizarListaEspera(pacientes) {
    const lista = document.getElementById('listaEspera');
    if (!lista) return;
    lista.innerHTML = '';

    if (!pacientes || pacientes.length === 0) {
        lista.innerHTML = `<li style="padding:20px; color:var(--text-muted); font-size:13px; text-align:center;">No hay pacientes en espera</li>`;
        return;
    }

    pacientes.forEach((p) => {
        const li = document.createElement('li');
        li.className = 'queue-item';
        const pId = p.paciente_id || p.id;
        if (pacienteSeleccionadoId && pId === pacienteSeleccionadoId) {
            li.classList.add('active');
        }

        const nombreMostrar = p.nombreCompleto || `${p.nombres || ''} ${p.apellidos || ''}`.trim() || 'Sin Nombre';
        const especialidadTag = p.especialidad_nombre ? `<span class="badge-urg">${p.especialidad_nombre}</span>` : '';

        li.onclick = () => solicitarConfirmacionAceptar(p, li);
        li.innerHTML = `
            ${especialidadTag}
            <b>${nombreMostrar}</b>
            <span style="color:var(--text-muted); font-size:11px;">Exp: ${p.codigo_expediente || p.codigo || 'N/A'}</span>
        `;
        lista.appendChild(li);
    });
}

function solicitarConfirmacionAceptar(p, elementoHtml) {
    document.querySelectorAll('.queue-item').forEach(el => el.classList.remove('active'));
    if (elementoHtml) elementoHtml.classList.add('active');

    pacientePendiente = p;
    confirmarAceptarPaciente();
}

function closeAcceptModal() {
    const modal = document.getElementById('acceptPatientModal');
    if (modal) modal.style.display = 'none';
}

function confirmarAceptarPaciente() {
    if (!pacientePendiente) return;
    const p = pacientePendiente;

    pacienteSeleccionadoId = p.paciente_id || p.id || 0;

    const nombreMostrar = p.nombreCompleto || `${p.nombres || ''} ${p.apellidos || ''}`.trim();
    document.getElementById('lblNombrePaciente').innerText = nombreMostrar;
    document.getElementById('lblExpediente').innerText = p.codigo_expediente || p.codigo || 'N/A';

    if (p.fecha_nacimiento) {
        const fechaNac = new Date(p.fecha_nacimiento);
        const edad = new Date().getFullYear() - fechaNac.getFullYear();
        document.getElementById('lblEdad').innerText = isNaN(edad) ? (p.edad || 'N/A') : edad;
    } else {
        document.getElementById('lblEdad').innerText = p.edad || 'N/A';
    }

    const modalTitle = document.querySelector('.history-modal-title');
    const modalSub = document.querySelector('.history-modal-sub');
    if (modalTitle) modalTitle.innerText = `Expediente de ${nombreMostrar}`;
    if (modalSub) modalSub.innerText = `Código: ${p.codigo_expediente || p.codigo || 'N/A'}`;

    obtenerHistorialCompleto();
    actualizarEstadoVista();
    closeAcceptModal();
}

function obtenerHistorialCompleto() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "OBTENER_HISTORIAL",
            PacienteId: pacienteSeleccionadoId,
            codigoExpediente: document.getElementById('lblExpediente').innerText
        }));
    } else {
        renderizarHistorial([]);
    }
}

function renderizarHistorial(historialServidor) {
    const container = document.querySelector('.history-modal-body');
    if (!container) return;

    let historialLocal = JSON.parse(localStorage.getItem(`historial_${pacienteSeleccionadoId}`) || '[]');
    let historialCompleto = [...historialLocal, ...(historialServidor || [])];

    if (historialCompleto.length === 0) {
        container.innerHTML = `<div style="padding: 20px; text-align: center; color: var(--text-muted);">El paciente no registra consultas previas.</div>`;
        return;
    }

    container.innerHTML = historialCompleto.map(item => `
        <div class="history-card">
            <div class="history-card-header">
                <div class="history-date">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect><line x1="16" y1="2" x2="16" y2="6"></line><line x1="8" y1="2" x2="8" y2="6"></line><line x1="3" y1="10" x2="21" y2="10"></line></svg>
                    ${item.fecha || 'FECHA NO REGISTRADA'}
                </div>
                <span class="history-time">${item.hora || 'Atendido'}</span>
            </div>
            <span class="history-label">DIAGNÓSTICO EMITIDO:</span>
            <h4 class="history-diag-title">${item.diagnostico || 'Consulta General'}</h4>
            <p class="history-desc">${item.observaciones || item.tratamiento || 'Sin observaciones registradas.'}</p>
        </div>
    `).join('');
}

function agregarMedicamentoTabla() {
    const inputNombre = document.getElementById('inputMedicamentoNombre');
    const inputDosis = document.getElementById('inputMedicamentoDosis');

    const nombre = inputNombre ? inputNombre.value.trim() : '';
    const dosis = inputDosis ? inputDosis.value.trim() : '';

    if (!nombre) {
        mostrarToast("Ingrese el nombre del medicamento.", "error");
        return;
    }

    listaReceta.push({ medicamento: nombre, indicaciones: dosis });

    if (inputNombre) inputNombre.value = '';
    if (inputDosis) inputDosis.value = '';

    renderizarTablaReceta();
}

function eliminarMedicamentoTabla(index) {
    listaReceta.splice(index, 1);
    renderizarTablaReceta();
}

function renderizarTablaReceta() {
    const tbody = document.getElementById('tbodyReceta');
    if (!tbody) return;

    if (listaReceta.length === 0) {
        tbody.innerHTML = `<tr><td colspan="3" style="text-align:center; color: var(--text-muted); font-size: 13px;">No hay medicamentos agregados a la receta.</td></tr>`;
        return;
    }

    tbody.innerHTML = listaReceta.map((item, index) => `
        <tr>
            <td><b>${item.medicamento}</b></td>
            <td>${item.indicaciones || 'Sin indicaciones especificadas'}</td>
            <td style="text-align: center;">
                <button type="button" class="btn-delete" title="Quitar medicamento" onclick="eliminarMedicamentoTabla(${index})">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path><line x1="10" y1="11" x2="10" y2="17"></line><line x1="14" y1="11" x2="14" y2="17"></line></svg>
                </button>
            </td>
        </tr>
    `).join('');
}

function finalizarConsulta() {
    if (!pacientePendiente || !pacienteSeleccionadoId) {
        mostrarToast("Seleccione un expediente de la lista antes de finalizar.", "error");
        return;
    }

    const txtDiag = document.getElementById('txtDiagnostico');
    const diag = txtDiag ? txtDiag.value.trim() : '';

    if (!diag) {
        mostrarToast("Escriba un diagnóstico antes de finalizar.", "error");
        return;
    }

    const fechaActual = new Date().toLocaleDateString('es-ES');
    const horaActual = new Date().toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' });

    // 1. Guardar en historial médico local
    const nuevaEntradaHistorial = {
        fecha: fechaActual,
        hora: horaActual,
        diagnostico: diag,
        observaciones: listaReceta.length > 0
            ? "Receta: " + listaReceta.map(m => `${m.medicamento} (${m.indicaciones})`).join(', ')
            : 'Sin medicamentos formulados.'
    };

    let historialExistente = JSON.parse(localStorage.getItem(`historial_${pacienteSeleccionadoId}`) || '[]');
    historialExistente.unshift(nuevaEntradaHistorial);
    localStorage.setItem(`historial_${pacienteSeleccionadoId}`, JSON.stringify(historialExistente));

    // 2. Registrar paciente en la cola de Cobros Pendientes (Módulo de Pago)
    const nombreCompleto = pacientePendiente.nombreCompleto || `${pacientePendiente.nombres || ''} ${pacientePendiente.apellidos || ''}`.trim();
    const nuevoCobro = {
        paciente_id: pacienteSeleccionadoId,
        codigo_expediente: pacientePendiente.codigo_expediente || pacientePendiente.codigo || 'N/A',
        nombreCompleto: nombreCompleto,
        especialidad_nombre: pacientePendiente.especialidad_nombre || pacientePendiente.especialidad || 'Consulta Médica',
        monto_consulta: pacientePendiente.monto_consulta || pacientePendiente.monto || 35.00
    };

    let listaPagos = JSON.parse(localStorage.getItem('listaPagos') || '[]');
    const yaExiste = listaPagos.some(p => (p.paciente_id || p.id) === nuevoCobro.paciente_id);
    if (!yaExiste) {
        listaPagos.push(nuevoCobro);
        localStorage.setItem('listaPagos', JSON.stringify(listaPagos));
    }

    // 3. Notificar a C#
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "GUARDAR_CONSULTA",
            PacienteId: pacienteSeleccionadoId,
            codigoExpediente: pacientePendiente.codigo_expediente || pacientePendiente.codigo,
            Diagnostico: diag
        }));

        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "REMOVER_DE_CONSULTA",
            PacienteId: pacienteSeleccionadoId
        }));
    }

    mostrarToast("Consulta procesada y enviada a Módulo de Pago", "exito");

    // 4. Remover paciente de la lista de espera local de consulta
    let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');
    listaLocal = listaLocal.filter(p => (p.paciente_id || p.id) !== pacienteSeleccionadoId);
    localStorage.setItem('listaEspera', JSON.stringify(listaLocal));
    renderizarListaEspera(listaLocal);

    limpiarWorkspaceConsulta();
}

function limpiarWorkspaceConsulta() {
    const diag = document.getElementById('txtDiagnostico');
    if (diag) diag.value = '';

    listaReceta = [];
    renderizarTablaReceta();

    pacientePendiente = null;
    pacienteSeleccionadoId = 0;

    const lblNombre = document.getElementById('lblNombrePaciente');
    if (lblNombre) lblNombre.innerText = 'Sin paciente seleccionado';

    const lblExp = document.getElementById('lblExpediente');
    if (lblExp) lblExp.innerText = '---';

    const lblEdad = document.getElementById('lblEdad');
    if (lblEdad) lblEdad.innerText = '--';

    actualizarEstadoVista();
}

function openHistoryModal() {
    if (!pacienteSeleccionadoId) {
        mostrarToast("Seleccione y acepte un expediente para consultar su historial.", "error");
        return;
    }
    obtenerHistorialCompleto();
    document.getElementById('historyModal').style.display = 'flex';
}

function closeHistoryModal() { document.getElementById('historyModal').style.display = 'none'; }
function openLogoutModal() { document.getElementById('logoutModal').style.display = 'flex'; }
function closeLogoutModal() { document.getElementById('logoutModal').style.display = 'none'; }
function confirmLogout() {
    localStorage.removeItem('usuarioRol');
    navegar('cerrarSesion');
}

function navegar(accion) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(accion);
    }
}

function simulateIncomingEmergency() { document.getElementById('emergencyOverlay').style.display = 'flex'; }
function dismissEmergency() { document.getElementById('emergencyOverlay').style.display = 'none'; }

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
    } else if (rolNorm === 'expedientes' || rolNorm === 'recepcion') {
        if (menuConsulta) menuConsulta.style.display = 'none';
        if (menuPago) menuPago.style.display = 'none';
    } else if (rolNorm === 'caja') {
        if (menuExpedientes) menuExpedientes.style.display = 'none';
        if (menuConsulta) menuConsulta.style.display = 'none';
    }
}

document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') {
        closeLogoutModal();
        dismissEmergency();
        closeHistoryModal();
        closeAcceptModal();
    }
});