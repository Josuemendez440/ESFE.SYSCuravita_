let pacientePendiente = null;
let pacienteSeleccionadoId = 0;

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);
    cargarListaEsperaLocal();
    actualizarEstadoVista();
});

// Refresca la lista de espera cuando la pantalla recupera el foco
window.addEventListener('focus', cargarListaEsperaLocal);

// Receptor de mensajes WebView2 (C#)
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        const datos = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (datos.Accion === "CARGAR_HISTORIAL") {
            renderizarHistorial(datos.Historial);
        } else if (datos.Accion === "CARGAR_LISTA_ESPERA" || datos.Accion === "CARGAR_EXPEDIENTES") {
            const lista = datos.Lista || datos.Expedientes || datos.Pacientes || [];
            localStorage.setItem('listaEspera', JSON.stringify(lista));
            renderizarListaEspera(lista);
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

// Controla la visibilidad del área de trabajo según si hay un expediente seleccionado
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

// Cargar la lista de espera local filtrando registros inválidos
function cargarListaEsperaLocal() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ Accion: "OBTENER_LISTA_ESPERA" }));
    }

    let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');
    listaLocal = listaLocal.filter(p => {
        const codigo = p.codigo_expediente || p.codigo || '';
        return codigo !== 'PAC-00' && codigo !== '' && codigo !== 'N/A';
    });

    renderizarListaEspera(listaLocal);
}

function renderizarListaEspera(pacientes) {
    const lista = document.getElementById('listaEspera');
    if (!lista) return;
    lista.innerHTML = '';

    if (!pacientes || pacientes.length === 0) {
        lista.innerHTML = `<li style="padding:20px; color:var(--text-muted); font-size:13px; text-align:center;">No hay registros</li>`;
        return;
    }

    pacientes.forEach((p) => {
        const li = document.createElement('li');
        li.className = 'queue-item';
        if (pacienteSeleccionadoId && (p.paciente_id === pacienteSeleccionadoId || p.id === pacienteSeleccionadoId)) {
            li.classList.add('active');
        }
        li.onclick = () => solicitarConfirmacionAceptar(p, li);
        li.innerHTML = `
            <b>${p.nombres} ${p.apellidos || ''}</b>
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

    document.getElementById('lblNombrePaciente').innerText = `${p.nombres} ${p.apellidos || ''}`;
    document.getElementById('lblExpediente').innerText = p.codigo_expediente || p.codigo || 'N/A';

    if (p.fecha_nacimiento) {
        const fechaNac = new Date(p.fecha_nacimiento);
        const edad = new Date().getFullYear() - fechaNac.getFullYear();
        document.getElementById('lblEdad').innerText = edad;
    } else {
        document.getElementById('lblEdad').innerText = p.edad || 'N/A';
    }

    const modalTitle = document.querySelector('.history-modal-title');
    const modalSub = document.querySelector('.history-modal-sub');
    if (modalTitle) modalTitle.innerText = `Expediente de ${p.nombres} ${p.apellidos || ''}`;
    if (modalSub) modalSub.innerText = `Código: ${p.codigo_expediente || p.codigo || 'N/A'}`;

    obtenerHistorialCompleto();
    actualizarEstadoVista();
    closeAcceptModal();
}

// Solicita el historial acumulado
function obtenerHistorialCompleto() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "OBTENER_HISTORIAL",
            PacienteId: pacienteSeleccionadoId,
            Codigo: document.getElementById('lblExpediente').innerText
        }));
    } else {
        renderizarHistorial([]);
    }
}

// Renderiza combinando el historial local acumulado y los datos de C#
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

// Agregar medicamentos a la tabla de prescripción
function agregarMedicamento() {
    const medInput = document.getElementById('txtMedicamento');
    const indInput = document.getElementById('txtIndicacion');
    if (!medInput || !indInput || !medInput.value.trim() || !indInput.value.trim()) {
        mostrarToast("Ingrese el medicamento y la indicación.", "error");
        return;
    }

    const tbody = document.getElementById('tbodyReceta');
    if (!tbody) return;
    const tr = document.createElement('tr');
    tr.innerHTML = `
        <td><b style="color: var(--primary);">${medInput.value.trim()}</b></td>
        <td>${indInput.value.trim()}</td>
        <td style="text-align: center;">
            <button class="btn-delete" title="Eliminar medicamento" onclick="this.closest('tr').remove()">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path><line x1="10" y1="11" x2="10" y2="17"></line><line x1="14" y1="11" x2="14" y2="17"></line></svg>
            </button>
        </td>
    `;
    tbody.appendChild(tr);
    medInput.value = '';
    indInput.value = '';
}

// Guarda la consulta, actualiza el historial acumulado, desocupa la pantalla y saca al paciente de la lista
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

    const medicamentos = [];
    document.querySelectorAll('#tbodyReceta tr').forEach(row => {
        medicamentos.push({
            Medicamento: row.cells[0].innerText,
            Indicacion: row.cells[1].innerText
        });
    });

    const fechaActual = new Date().toLocaleDateString('es-ES');
    const horaActual = new Date().toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' });

    // 1. Guardar localmente la nueva consulta para acumular diagnósticos múltiples
    const nuevaEntradaHistorial = {
        fecha: fechaActual,
        hora: horaActual,
        diagnostico: diag,
        observaciones: medicamentos.length > 0
            ? "Receta: " + medicamentos.map(m => `${m.Medicamento} (${m.Indicacion})`).join(', ')
            : 'Sin medicamentos formulados.'
    };

    let historialExistente = JSON.parse(localStorage.getItem(`historial_${pacienteSeleccionadoId}`) || '[]');
    historialExistente.unshift(nuevaEntradaHistorial);
    localStorage.setItem(`historial_${pacienteSeleccionadoId}`, JSON.stringify(historialExistente));

    // 2. Enviar datos al backend C#
    const paquete = {
        Accion: "GUARDAR_CONSULTA",
        PacienteId: pacienteSeleccionadoId,
        Diagnostico: diag,
        Receta: medicamentos
    };

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(paquete));
    }

    mostrarToast("Diagnóstico agregado exitosamente al expediente", "exito");

    // 3. Eliminar paciente de la lista de espera
    let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');
    const targetUid = pacientePendiente.uid;
    const targetCode = pacientePendiente.codigo_expediente || pacientePendiente.codigo;

    listaLocal = listaLocal.filter(p => {
        if (targetUid && p.uid) return p.uid !== targetUid;
        return (p.codigo_expediente || p.codigo) !== targetCode;
    });

    localStorage.setItem('listaEspera', JSON.stringify(listaLocal));
    renderizarListaEspera(listaLocal);

    // 4. Limpiar datos y regresar a la vista sin expediente seleccionado
    pacientePendiente = null;
    pacienteSeleccionadoId = 0;

    const lblNombre = document.getElementById('lblNombrePaciente');
    if (lblNombre) lblNombre.innerText = 'Sin paciente seleccionado';

    const lblExp = document.getElementById('lblExpediente');
    if (lblExp) lblExp.innerText = '---';

    const lblEdad = document.getElementById('lblEdad');
    if (lblEdad) lblEdad.innerText = '--';

    if (txtDiag) txtDiag.value = '';
    const tbody = document.getElementById('tbodyReceta');
    if (tbody) tbody.innerHTML = '';

    actualizarEstadoVista();
}

// Modales y navegación
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