let pacientePendiente = null;
let pacienteSeleccionadoId = 0;

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);
    cargarListaEsperaLocal();
});

// Refresca la lista de espera cuando la pantalla recupera el foco[cite: 16]
window.addEventListener('focus', cargarListaEsperaLocal);

// Receptor de mensajes WebView2 (C#)[cite: 16]
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        const datos = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (datos.Accion === "CARGAR_HISTORIAL") {
            renderizarHistorial(datos.Historial);
        }
    });
}

// 1. Cargar únicamente los pacientes seleccionados en la pantalla de Expedientes[cite: 16]
function cargarListaEsperaLocal() {
    let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');

    // Filtra datos obsoletos de prueba (ej. PAC-00)
    listaLocal = listaLocal.filter(p => {
        const codigo = p.codigo_expediente || p.codigo || '';
        return codigo !== 'PAC-00' && codigo !== '' && codigo !== 'N/A';
    });

    localStorage.setItem('listaEspera', JSON.stringify(listaLocal));
    renderizarListaEspera(listaLocal);
}

function renderizarListaEspera(pacientes) {
    const lista = document.getElementById('listaEspera');
    if (!lista) return;
    lista.innerHTML = '';

    if (!pacientes || pacientes.length === 0) {
        lista.innerHTML = `<li style="padding:16px; color:var(--text-muted); font-size:12px; text-align:center;">No hay pacientes seleccionados en espera.</li>`;
        return;
    }

    pacientes.forEach((p) => {
        const li = document.createElement('li');
        li.className = 'queue-item';
        li.onclick = () => solicitarConfirmacionAceptar(p, li);
        li.innerHTML = `
            <b>${p.nombres} ${p.apellidos || ''}</b>
            <span style="color:var(--text-muted); font-size:11px;">Exp: ${p.codigo_expediente || p.codigo || 'N/A'}</span>
        `;
        lista.appendChild(li);
    });
}

// 2. Control del Modal de Confirmación "Aceptar Paciente"[cite: 16]
function solicitarConfirmacionAceptar(p, elementoHtml) {
    document.querySelectorAll('.queue-item').forEach(el => el.classList.remove('active'));
    if (elementoHtml) elementoHtml.classList.add('active');

    pacientePendiente = p;

    const modal = document.getElementById('acceptPatientModal');
    if (modal) {
        document.getElementById('acceptPatientName').innerText = `${p.nombres} ${p.apellidos || ''}`;
        document.getElementById('acceptPatientCode').innerText = p.codigo_expediente || p.codigo || 'N/A';
        modal.style.display = 'flex';
    } else {
        confirmarAceptarPaciente();
    }
}

function closeAcceptModal() {
    const modal = document.getElementById('acceptPatientModal');
    if (modal) modal.style.display = 'none';
}

// 3. Confirmación y carga del paciente al espacio de trabajo[cite: 16]
function confirmarAceptarPaciente() {
    if (!pacientePendiente) return;
    const p = pacientePendiente;

    pacienteSeleccionadoId = p.paciente_id || p.id || 0;

    // Cargar datos en la tarjeta principal
    document.getElementById('lblNombrePaciente').innerText = `${p.nombres} ${p.apellidos || ''}`;
    document.getElementById('lblExpediente').innerText = p.codigo_expediente || p.codigo || 'N/A';

    if (p.fecha_nacimiento) {
        const fechaNac = new Date(p.fecha_nacimiento);
        const edad = new Date().getFullYear() - fechaNac.getFullYear();
        document.getElementById('lblEdad').innerText = edad;
    } else {
        document.getElementById('lblEdad').innerText = p.edad || 'N/A';
    }

    // Actualizar datos del modal de historial
    const modalTitle = document.querySelector('.history-modal-title');
    const modalSub = document.querySelector('.history-modal-sub');
    if (modalTitle) modalTitle.innerText = `Expediente de ${p.nombres} ${p.apellidos || ''}`;
    if (modalSub) modalSub.innerText = `Código: ${p.codigo_expediente || p.codigo || 'N/A'}`;

    // Solicitar historial a C#
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "OBTENER_HISTORIAL",
            PacienteId: pacienteSeleccionadoId,
            Codigo: p.codigo_expediente || p.codigo
        }));
    }

    closeAcceptModal();
}

// 4. Renderizado del historial clínico desde C#[cite: 16]
function renderizarHistorial(historial) {
    const container = document.querySelector('.history-modal-body');
    if (!container) return;

    if (!historial || historial.length === 0) {
        container.innerHTML = `<div style="padding: 20px; text-align: center; color: var(--text-muted);">El paciente no registra consultas previas.</div>`;
        return;
    }

    container.innerHTML = historial.map(item => `
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

// 5. Gestión de Receta[cite: 16]
function agregarMedicamento() {
    const medInput = document.getElementById('txtMedicamento');
    const indInput = document.getElementById('txtIndicacion');
    if (!medInput || !indInput || !medInput.value.trim() || !indInput.value.trim()) {
        alert("Ingrese el medicamento y la indicación.");
        return;
    }

    const tbody = document.getElementById('tbodyReceta');
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

// 6. Finalizar consulta, remover de lista de espera y limpiar interfaz[cite: 16]
function finalizarConsulta() {
    if (!pacientePendiente) {
        alert("Seleccione un paciente de la lista de espera antes de finalizar.");
        return;
    }

    const txtDiag = document.getElementById('txtDiagnostico');
    const diag = txtDiag ? txtDiag.value.trim() : '';
    if (!diag) {
        alert("Escriba un diagnóstico antes de finalizar.");
        return;
    }

    const medicamentos = [];
    document.querySelectorAll('#tbodyReceta tr').forEach(row => {
        medicamentos.push({
            Medicamento: row.cells[0].innerText,
            Indicacion: row.cells[1].innerText
        });
    });

    const paquete = {
        Accion: "GUARDAR_CONSULTA",
        PacienteId: pacienteSeleccionadoId,
        Diagnostico: diag,
        Receta: medicamentos
    };

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(paquete));
    }

    // Remover al paciente finalizado de localStorage usando UID o Código
    let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');
    const targetUid = pacientePendiente.uid;
    const targetCode = pacientePendiente.codigo_expediente || pacientePendiente.codigo;

    listaLocal = listaLocal.filter(p => {
        if (targetUid && p.uid) return p.uid !== targetUid;
        return (p.codigo_expediente || p.codigo) !== targetCode;
    });

    localStorage.setItem('listaEspera', JSON.stringify(listaLocal));
    renderizarListaEspera(listaLocal);

    // Resetear la interfaz
    pacientePendiente = null;
    pacienteSeleccionadoId = 0;
    document.getElementById('lblNombrePaciente').innerText = 'Sin paciente seleccionado';
    document.getElementById('lblExpediente').innerText = '---';
    document.getElementById('lblEdad').innerText = '--';
    if (txtDiag) txtDiag.value = '';
    const tbody = document.getElementById('tbodyReceta');
    if (tbody) tbody.innerHTML = '';

    alert("Consulta procesada exitosamente.");
}

// Modales y Utilidades[cite: 16]
function openHistoryModal() {
    if (!pacienteSeleccionadoId) {
        alert("Seleccione y acepte a un paciente para consultar su historial.");
        return;
    }

    // Solicitar el historial actualizado a C# al abrir el modal
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "OBTENER_HISTORIAL",
            PacienteId: pacienteSeleccionadoId,
            Codigo: document.getElementById('lblExpediente').innerText
        }));
    }

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