let pacienteSeleccionadoId = 0;

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) {
        aplicarPermisos(rolGuardado);
    }
    solicitarPacientes();
});

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        const datos = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (datos.Accion === "CARGAR_LISTA_ESPERA") {
            renderizarListaEspera(datos.Pacientes);
        }
    });
}

function solicitarPacientes() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ Accion: "OBTENER_PACIENTES" }));
    }
}

function renderizarListaEspera(pacientes) {
    const lista = document.getElementById('listaEspera');
    if (!lista) return;
    lista.innerHTML = '';

    pacientes.forEach((p, index) => {
        const li = document.createElement('li');
        li.className = `queue-item ${index === 0 ? 'active' : ''}`;
        li.onclick = () => seleccionarPaciente(p, li);
        li.innerHTML = `
            <b>${p.nombres} ${p.apellidos}</b>
            <span style="color:var(--text-muted); font-size:11px;">Exp: ${p.codigo_expediente}</span>
        `;
        lista.appendChild(li);
        if (index === 0) seleccionarPaciente(p, li);
    });
}

function seleccionarPaciente(p, elementoHtml) {
    document.querySelectorAll('.queue-item').forEach(el => el.classList.remove('active'));
    if (elementoHtml) elementoHtml.classList.add('active');

    pacienteSeleccionadoId = p.paciente_id;
    document.getElementById('lblNombrePaciente').innerText = `${p.nombres} ${p.apellidos}`;
    document.getElementById('lblExpediente').innerText = p.codigo_expediente;

    if (p.fecha_nacimiento) {
        const fechaNac = new Date(p.fecha_nacimiento);
        const edad = new Date().getFullYear() - fechaNac.getFullYear();
        document.getElementById('lblEdad').innerText = edad;
    } else {
        document.getElementById('lblEdad').innerText = 'N/A';
    }
}

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

function finalizarConsulta() {
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
        if (txtDiag) txtDiag.value = '';
        const tbody = document.getElementById('tbodyReceta');
        if (tbody) tbody.innerHTML = '';
        alert("Consulta procesada exitosamente.");
    }
}

function establecerUsuario(rol) {
    localStorage.setItem('usuarioRol', rol);
    aplicarPermisos(rol);
}

function navegar(accion) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(accion);
    }
}

function simulateIncomingEmergency() {
    document.getElementById('emergencyOverlay').style.display = 'flex';
}

function dismissEmergency() {
    document.getElementById('emergencyOverlay').style.display = 'none';
}

function openLogoutModal() {
    document.getElementById('logoutModal').style.display = 'flex';
}

function closeLogoutModal() {
    document.getElementById('logoutModal').style.display = 'none';
}

function openHistoryModal() {
    document.getElementById('historyModal').style.display = 'flex';
}

function closeHistoryModal() {
    document.getElementById('historyModal').style.display = 'none';
}

function confirmLogout() {
    localStorage.removeItem('usuarioRol');
    navegar('cerrarSesion');
}

function aplicarPermisos(rol) {
    const menuExpedientes = document.getElementById('menuExpedientes');
    const menuConsulta = document.getElementById('menuConsulta');
    const menuPago = document.getElementById('menuPago');

    if (menuExpedientes) menuExpedientes.style.display = '';
    if (menuConsulta) menuConsulta.style.display = '';
    if (menuPago) menuPago.style.display = '';

    const rolNorm = (rol || '').toLowerCase().trim();

    if (rolNorm === 'admin' || rolNorm === 'administrador') {
        return;
    }

    if (rolNorm === 'doctor' || rolNorm === 'medico' || rolNorm.includes('medico')) {
        if (menuExpedientes) menuExpedientes.style.display = 'none';
        if (menuPago) menuPago.style.display = 'none';
    }
    else if (rolNorm === 'expedientes' || rolNorm === 'recepcion' || rolNorm.includes('recepcion')) {
        if (menuConsulta) menuConsulta.style.display = 'none';
        if (menuPago) menuPago.style.display = 'none';
    }
    else if (rolNorm === 'caja' || rolNorm.includes('caja')) {
        if (menuExpedientes) menuExpedientes.style.display = 'none';
        if (menuConsulta) menuConsulta.style.display = 'none';
    }
}

document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') {
        closeLogoutModal();
        dismissEmergency();
        closeHistoryModal();
    }
});