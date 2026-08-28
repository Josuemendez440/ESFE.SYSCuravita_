let pacientePendiente = null;
let pacienteSeleccionadoId = 0;
let listaReceta = [];

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);

    inicializarEventosNavegacion();
    cargarListaEsperaLocal();
    renderizarTablaReceta();
    actualizarEstadoVista();
    aplicarValidacionesCampos();
});

window.addEventListener('focus', cargarListaEsperaLocal);
window.addEventListener('storage', cargarListaEsperaLocal);

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

function obtenerValorCampo(ids) {
    for (let id of ids) {
        let el = document.getElementById(id) || document.querySelector(id);
        if (el && el.value && el.value.trim() !== '') {
            return el.value.trim();
        }
    }
    return 'N/A';
}

function aplicarValidacionesCampos() {
    const inputsTexto = document.querySelectorAll('#txtDiagnostico, #inputMedicamentoNombre, #inputMedicamentoDosis');
    inputsTexto.forEach(input => {
        if (!input) return;
        input.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/[^a-zA-Z0-9áéíóúÁÉÍÓÚñÑ\s.,/()-]/g, '');
        });
    });

    const inputPA = document.getElementById('inputPA');
    if (inputPA) {
        inputPA.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/[^0-9/]/g, '');
        });
    }

    const inputFC = document.getElementById('inputFC');
    if (inputFC) {
        inputFC.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/[^0-9]/g, '');
        });
    }

    const inputsDecimales = document.querySelectorAll('#inputTemp, #inputPeso');
    inputsDecimales.forEach(input => {
        if (!input) return;
        input.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/[^0-9.]/g, '');
            const partes = e.target.value.split('.');
            if (partes.length > 2) {
                e.target.value = partes[0] + '.' + partes.slice(1).join('');
            }
        });
    });
}

// --- Receptor de mensajes de WebView2 (C# -> JS) ---
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        try {
            const datos = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
            const accion = datos.Accion || datos.accion;

            if (accion === "CARGAR_HISTORIAL") {
                renderizarHistorial(datos.Historial || datos.historial);
            } else if (accion === "CARGAR_LISTA_ESPERA") {
                const listaServidor = datos.Pacientes || datos.pacientes || [];
                let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');

                listaServidor.forEach(p => {
                    const pId = String(p.paciente_id || p.pacienteId || p.id);
                    if (!listaLocal.some(item => String(item.paciente_id || item.pacienteId || item.id) === pId)) {
                        listaLocal.push(p);
                    }
                });

                listaLocal = filtrarListaEspera(listaLocal);
                localStorage.setItem('listaEspera', JSON.stringify(listaLocal));
                renderizarListaEspera(listaLocal);
            } else if (accion === "CONSULTA_GUARDADA") {
                const exito = datos.Exito !== undefined ? datos.Exito : datos.exito;
                if (exito) {
                    mostrarToast("Consulta procesada y guardada exitosamente en la BD", "exito");
                    if (pacienteSeleccionadoId) {
                        obtenerHistorialCompleto();
                    }
                } else {
                    mostrarToast("Error al guardar la consulta en la base de datos", "error");
                }
            } else if (accion === "REGISTRO_ELIMINADO") {
                const idBorrado = String(datos.PacienteId || datos.pacienteId);

                let listaEspera = JSON.parse(localStorage.getItem('listaEspera') || '[]');
                listaEspera = listaEspera.filter(p => String(p.paciente_id || p.pacienteId || p.id) !== idBorrado);
                localStorage.setItem('listaEspera', JSON.stringify(listaEspera));
                localStorage.removeItem(`historial_${idBorrado}`);

                if (String(pacienteSeleccionadoId) === idBorrado) {
                    limpiarWorkspaceConsulta();
                }

                renderizarListaEspera(listaEspera);
            }
        } catch (err) {
            console.error("Error procesando mensaje WebView2:", err);
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
        const codigo = p.codigo_expediente || p.codigoExpediente || p.codigo || '';
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
        const pId = String(p.paciente_id || p.pacienteId || p.id);
        if (pacienteSeleccionadoId && pId === String(pacienteSeleccionadoId)) {
            li.classList.add('active');
        }

        const nombreMostrar = p.nombreCompleto || `${p.nombres || ''} ${p.apellidos || ''}`.trim() || 'Sin Nombre';
        const especialidadTag = (p.especialidad_nombre || p.especialidadNombre) ? `<span class="badge-urg">${p.especialidad_nombre || p.especialidadNombre}</span>` : '';

        li.onclick = () => solicitarConfirmacionAceptar(p, li);
        li.innerHTML = `
            ${especialidadTag}
            <b>${nombreMostrar}</b>
            <span style="color:var(--text-muted); font-size:11px;">Exp: ${p.codigo_expediente || p.codigoExpediente || p.codigo || 'N/A'}</span>
        `;
        lista.appendChild(li);
    });
}

function solicitarConfirmacionAceptar(p, elementoHtml) {
    document.querySelectorAll('.queue-item').forEach(el => el.classList.remove('active'));
    if (elementoHtml) elementoHtml.classList.add('active');

    pacientePendiente = p;

    const modal = document.getElementById('acceptPatientModal');
    if (modal) {
        const nombreMostrar = p.nombreCompleto || `${p.nombres || ''} ${p.apellidos || ''}`.trim();
        document.getElementById('acceptPatientName').innerText = nombreMostrar;
        document.getElementById('acceptPatientCode').innerText = p.codigo_expediente || p.codigoExpediente || p.codigo || 'N/A';
        modal.style.display = 'flex';
    } else {
        confirmarAceptarPaciente();
    }
}

function closeAcceptModal() {
    const modal = document.getElementById('acceptPatientModal');
    if (modal) modal.style.display = 'none';
}

function calcularEdad(fechaCadena) {
    if (!fechaCadena) return 'N/A';
    const partes = fechaCadena.split('T')[0].split('-');
    if (partes.length < 3) return 'N/A';

    const anio = parseInt(partes[0], 10);
    const mes = parseInt(partes[1], 10) - 1;
    const dia = parseInt(partes[2], 10);

    const nac = new Date(anio, mes, dia);
    const hoy = new Date();
    let edad = hoy.getFullYear() - nac.getFullYear();
    const m = hoy.getMonth() - nac.getMonth();

    if (m < 0 || (m === 0 && hoy.getDate() < nac.getDate())) {
        edad--;
    }
    return isNaN(edad) ? 'N/A' : edad;
}

function confirmarAceptarPaciente() {
    if (!pacientePendiente) return;
    const p = pacientePendiente;

    const idBruto = p.paciente_id || p.pacienteId || p.id || 0;
    pacienteSeleccionadoId = String(idBruto);

    const nombreMostrar = p.nombreCompleto || `${p.nombres || ''} ${p.apellidos || ''}`.trim();
    document.getElementById('lblNombrePaciente').innerText = nombreMostrar;
    document.getElementById('lblExpediente').innerText = p.codigo_expediente || p.codigoExpediente || p.codigo || 'N/A';

    const fechaNac = p.fecha_nacimiento || p.fechaNacimiento;
    document.getElementById('lblEdad').innerText = fechaNac ? calcularEdad(fechaNac) : (p.edad || 'N/A');

    const modalTitle = document.querySelector('.history-modal-title');
    const modalSub = document.querySelector('.history-modal-sub');
    if (modalTitle) modalTitle.innerText = `Expediente de ${nombreMostrar}`;
    if (modalSub) modalSub.innerText = `Código: ${p.codigo_expediente || p.codigoExpediente || p.codigo || 'N/A'}`;

    obtenerHistorialCompleto();
    actualizarEstadoVista();
    closeAcceptModal();
}

function obtenerHistorialCompleto() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "OBTENER_HISTORIAL",
            PacienteId: parseInt(pacienteSeleccionadoId, 10) || 0,
            codigoExpediente: document.getElementById('lblExpediente').innerText
        }));
    } else {
        renderizarHistorial([]);
    }
}

function renderizarHistorial(historialServidor) {
    const container = document.querySelector('.history-modal-body');
    if (!container) return;

    let historialCompleto = historialServidor || [];

    if (historialCompleto.length === 0) {
        container.innerHTML = `<div style="padding: 20px; text-align: center; color: var(--text-muted);">El paciente no registra consultas previas.</div>`;
        return;
    }

    container.innerHTML = historialCompleto.map(item => {
        const fecha = item.fecha || item.Fecha || 'FECHA NO REGISTRADA';
        const hora = item.hora || item.Hora || '';

        let rawDiag = item.diagnostico || item.Diagnostico || 'Sin diagnóstico';
        let diagnosticoPuro = rawDiag;
        let vitals = item.vitals || item.Vitals || '';
        let receta = item.receta || item.Receta || item.observaciones || item.Observaciones || '';

        if (rawDiag.includes('PA:') || rawDiag.includes('Receta:')) {
            let partesReceta = rawDiag.split(/Receta:/i);
            if (partesReceta.length > 1 && partesReceta[1].trim() !== '') {
                receta = partesReceta[1].trim();
            }

            let parteAntesReceta = partesReceta[0];
            let partesPA = parteAntesReceta.split(/PA:/i);

            diagnosticoPuro = partesPA[0].trim();
            if (partesPA.length > 1) {
                vitals = 'PA: ' + partesPA[1].trim();
            }
        }

        diagnosticoPuro = diagnosticoPuro.replace(/[\r\n]+/g, ' ').trim();
        if (!diagnosticoPuro) diagnosticoPuro = "Sin diagnóstico";

        let recetaTexto = 'Sin medicamentos formulados.';
        if (receta && receta !== 'Sin medicamentos' && receta !== 'Sin medicamentos formulados.') {
            recetaTexto = receta.replace(/^Receta:\s*/i, '');
        }

        return `
            <div class="history-card" style="border: 1px solid #e2e8f0; border-radius: 12px; padding: 16px; margin-bottom: 12px; background: #ffffff;">
                <div class="history-card-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px;">
                    <div class="history-date" style="display: flex; align-items: center; gap: 6px; font-weight: 700; color: #334155;">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="16" height="16"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect><line x1="16" y1="2" x2="16" y2="6"></line><line x1="8" y1="2" x2="8" y2="6"></line><line x1="3" y1="10" x2="21" y2="10"></line></svg>
                        <span>${fecha}</span>
                    </div>
                    ${hora ? `<span class="history-time" style="background: #e2e8f0; color: #475569; font-size: 12px; font-weight: 600; padding: 2px 8px; border-radius: 6px;">${hora}</span>` : ''}
                </div>

                <span class="history-label" style="font-size: 11px; font-weight: 800; color: #64748b; letter-spacing: 0.5px;">DIAGNÓSTICO EMITIDO:</span>
                <h4 class="history-diag-title" style="font-size: 18px; font-weight: 800; color: #0f172a; margin: 4px 0 8px 0;">${diagnosticoPuro}</h4>
                ${vitals ? `<p style="font-size: 13px; font-weight: 700; color: #0369a1; margin: 0 0 4px 0;">${vitals}</p>` : ''}
                <p style="font-size: 13px; color: #64748b; margin: 0;"><b>Receta:</b> ${recetaTexto}</p>
            </div>
        `;
    }).join('');
}

function agregarMedicamentoTabla(e) {
    if (e) e.preventDefault();
    const elNombre = document.getElementById('inputMedicamentoNombre');
    const elDosis = document.getElementById('inputMedicamentoDosis');

    const nombre = elNombre ? elNombre.value.trim() : '';
    const dosis = elDosis ? elDosis.value.trim() : '';

    if (!nombre) {
        mostrarToast("Ingrese el nombre del medicamento.", "error");
        return;
    }

    listaReceta.push({ medicamento: nombre, indicaciones: dosis });

    if (elNombre) elNombre.value = '';
    if (elDosis) elDosis.value = '';

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
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="16" height="16"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path><line x1="10" y1="11" x2="10" y2="17"></line><line x1="14" y1="11" x2="14" y2="17"></line></svg>
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

    const paTexto = obtenerValorCampo(['inputPA']);
    const fcTexto = obtenerValorCampo(['inputFC']);
    const tempTexto = obtenerValorCampo(['inputTemp']);
    const pesoTexto = obtenerValorCampo(['inputPeso']);

    const regexPA = /^(\d+)\/(\d+)$/;
    const matchPA = paTexto.match(regexPA);

    if (!matchPA) {
        mostrarToast("La Presión Arterial no permite letras. Use formato ej: 120/80.", "error");
        return;
    }

    const sistolica = parseInt(matchPA[1], 10);
    const diastolica = parseInt(matchPA[2], 10);

    if (sistolica < 40 || diastolica < 30) {
        mostrarToast("Ingrese una Presión Arterial válida (no se permite solo ceros).", "error");
        return;
    }

    if (!/^\d+$/.test(fcTexto)) {
        mostrarToast("La Frecuencia Cardíaca solo acepta números.", "error");
        return;
    }

    const fc = parseInt(fcTexto, 10);
    if (fc <= 0) {
        mostrarToast("La Frecuencia Cardíaca no puede ser cero.", "error");
        return;
    }

    if (!/^\d+(\.\d+)?$/.test(tempTexto)) {
        mostrarToast("La Temperatura solo acepta valores numéricos.", "error");
        return;
    }

    const temp = parseFloat(tempTexto);
    if (temp < 25.0 || temp > 45.0) {
        mostrarToast("Ingrese una Temperatura válida (no se permite solo ceros).", "error");
        return;
    }

    if (!/^\d+(\.\d+)?$/.test(pesoTexto)) {
        mostrarToast("El Peso solo acepta valores numéricos.", "error");
        return;
    }

    const peso = parseFloat(pesoTexto);
    if (peso <= 0) {
        mostrarToast("El Peso debe ser mayor a cero.", "error");
        return;
    }

    const nombreCompleto = pacientePendiente.nombreCompleto || `${pacientePendiente.nombres || ''} ${pacientePendiente.apellidos || ''}`.trim();
    const codigoExp = pacientePendiente.codigo_expediente || pacientePendiente.codigoExpediente || pacientePendiente.codigo || 'N/A';

    // Capturar el monto real proveniente del objeto del paciente/cita o usar 25.00 de respaldo
    const montoCalculado = parseFloat(
        pacientePendiente.monto_consulta ||
        pacientePendiente.montoConsulta ||
        pacientePendiente.monto ||
        pacientePendiente.precio ||
        25.00
    );

    const nuevoCobro = {
        paciente_id: String(pacienteSeleccionadoId),
        codigo_expediente: codigoExp,
        nombreCompleto: nombreCompleto,
        especialidad_nombre: pacientePendiente.especialidad_nombre || pacientePendiente.especialidadNombre || 'Consulta Médica',
        monto_consulta: montoCalculado
    };

    let listaPagos = JSON.parse(localStorage.getItem('listaPagos') || '[]');
    const yaExiste = listaPagos.some(p => String(p.paciente_id || p.pacienteId || p.id) === String(nuevoCobro.paciente_id));
    if (!yaExiste) {
        listaPagos.push(nuevoCobro);
        localStorage.setItem('listaPagos', JSON.stringify(listaPagos));
    }

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "GUARDAR_CONSULTA",
            ConsultaId: 0,
            PacienteId: parseInt(pacienteSeleccionadoId, 10) || 0,
            CodigoExpediente: String(codigoExp),
            Diagnostico: String(diag),
            PresionSistolica: sistolica,
            PresionDiastolica: diastolica,
            FC: fc,
            Temp: temp,
            Peso: peso,
            MontoConsulta: montoCalculado, // <--- CAMBIO: Se agrega el monto al JSON enviado a C#
            Monto: montoCalculado,          // <--- CAMBIO: Alias por compatibilidad
            Medicamentos: listaReceta.map(m => ({
                Medicamento: String(m.medicamento),
                IndicacionesDosis: String(m.indicaciones || 'Sin indicaciones')
            }))
        }));

        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "REMOVER_DE_CONSULTA",
            PacienteId: parseInt(pacienteSeleccionadoId, 10) || 0
        }));
    }

    mostrarToast("Consulta procesada y enviada a Módulo de Pago", "exito");

    let listaLocal = JSON.parse(localStorage.getItem('listaEspera') || '[]');
    listaLocal = listaLocal.filter(p => String(p.paciente_id || p.pacienteId || p.id) !== String(pacienteSeleccionadoId));
    localStorage.setItem('listaEspera', JSON.stringify(listaLocal));
    renderizarListaEspera(listaLocal);

    limpiarWorkspaceConsulta();
}

function limpiarWorkspaceConsulta() {
    const diag = document.getElementById('txtDiagnostico');
    if (diag) diag.value = '';

    const inputsLimpiar = document.querySelectorAll('#inputPA, #inputFC, #inputTemp, #inputPeso');
    inputsLimpiar.forEach(inp => { if (inp) inp.value = ''; });

    listaReceta = [];
    renderizarTablaReceta();

    pacientePendiente = null;
    pacienteSeleccionadoId = 0;

    document.getElementById('lblNombrePaciente').innerText = 'Sin paciente seleccionado';
    document.getElementById('lblExpediente').innerText = '---';
    document.getElementById('lblEdad').innerText = '--';

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
        window.chrome.webview.postMessage(JSON.stringify({
            accion: "NAVEGAR",
            modulo: String(accion),
            rol: String(localStorage.getItem('usuarioRol') || 'admin')
        }));
    } else {
        if (accion === 'cerrarSesion' || accion === 'cerrar_sesion') {
            window.location.href = 'login.html';
        } else {
            window.location.href = `${accion}.html`;
        }
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