let pagoSeleccionado = null;
let listaPagosCache = [];
let metodoPagoActual = 'Efectivo';

document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) aplicarPermisos(rolGuardado);

    inicializarEventosNavegacion();
    cargarListaPagosLocal();
});

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
        const datos = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
        if (datos.Accion === "CARGAR_PAGOS_PENDIENTES") {
            renderizarListaPagos(datos.Pagos || []);
        } else if (datos.Accion === "PAGO_REGISTRADO") {
            if (datos.Exito) {
                alert(datos.Mensaje || "Factura registrada e impresa correctamente.");
            }
        } else if (datos.Accion === "REGISTRO_ELIMINADO") {
            const idBorrado = datos.PacienteId;

            let listaPagos = JSON.parse(localStorage.getItem('listaPagos') || '[]');
            listaPagos = listaPagos.filter(p => (p.paciente_id || p.id || p.consulta_id) !== idBorrado);
            localStorage.setItem('listaPagos', JSON.stringify(listaPagos));

            if (pagoSeleccionado && (pagoSeleccionado.paciente_id || pagoSeleccionado.id || pagoSeleccionado.consulta_id) === idBorrado) {
                pagoSeleccionado = null;
                document.getElementById('workspacePanelPago').style.display = 'none';
                document.getElementById('emptyWorkspacePago').style.display = 'block';
            }

            renderizarListaPagos(listaPagos);
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

function obtenerSiguienteNumeroFactura() {
    let actual = parseInt(localStorage.getItem('correlativoFactura') || '1');
    return `FAC-${String(actual).padStart(5, '0')}`;
}

function incrementarNumeroFactura() {
    let actual = parseInt(localStorage.getItem('correlativoFactura') || '1');
    localStorage.setItem('correlativoFactura', (actual + 1).toString());
}

function cargarListaPagosLocal() {
    let listaLocal = JSON.parse(localStorage.getItem('listaPagos') || '[]');
    renderizarListaPagos(listaLocal);

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ Accion: "OBTENER_PAGOS_PENDIENTES" }));
    }
}

function renderizarListaPagos(pagos) {
    listaPagosCache = pagos || [];
    const contenedor = document.getElementById('listaPagosPendientes');
    if (!contenedor) return;
    contenedor.innerHTML = '';

    if (listaPagosCache.length === 0) {
        contenedor.innerHTML = `<li style="padding:15px; color:var(--text-muted); font-size:13px; text-align:center;">No hay facturas pendientes</li>`;
        return;
    }

    listaPagosCache.forEach((p) => {
        const li = document.createElement('li');
        li.className = 'queue-item';

        const pId = p.paciente_id || p.id || p.consulta_id;
        if (pagoSeleccionado && (pagoSeleccionado.paciente_id || pagoSeleccionado.id || pagoSeleccionado.consulta_id) === pId) {
            li.classList.add('active');
        }

        const nombre = p.nombreCompleto || `${p.nombres || ''} ${p.apellidos || ''}`.trim() || 'Paciente';
        const monto = parseFloat(p.monto_consulta || p.monto || 25.00).toFixed(2);

        li.onclick = () => seleccionarPago(p, li);
        li.innerHTML = `
            <div style="display:flex; justify-content:space-between; align-items:center;">
                <div>
                    <strong>${nombre}</strong><br>
                    <small style="color:var(--text-muted);">${p.especialidad_nombre || 'Consulta General'}</small>
                </div>
                <span style="font-weight:700; color:var(--primary);">$${monto}</span>
            </div>
        `;
        contenedor.appendChild(li);
    });
}

function seleccionarPago(p, elementoHtml) {
    document.querySelectorAll('.queue-item').forEach(el => el.classList.remove('active'));
    if (elementoHtml) elementoHtml.classList.add('active');

    pagoSeleccionado = p;

    const nombre = p.nombreCompleto || `${p.nombres || ''} ${p.apellidos || ''}`.trim();
    const monto = parseFloat(p.monto_consulta || p.monto || 25.00).toFixed(2);

    document.getElementById('emptyWorkspacePago').style.display = 'none';
    const workspace = document.getElementById('workspacePanelPago');
    workspace.style.display = 'flex';

    document.getElementById('lblPagoPaciente').value = nombre;
    document.getElementById('inputNumFactura').value = obtenerSiguienteNumeroFactura();
    document.getElementById('inputFechaPago').value = new Date().toLocaleDateString('es-ES');
    document.getElementById('lblPagoMontoTotal').innerText = `$${monto}`;

    selectMethod('efectivo');
}

function selectMethod(method) {
    const btnEfectivo = document.getElementById('btnEfectivo');
    const btnTarjeta = document.getElementById('btnTarjeta');
    const inputRecibido = document.getElementById('inputMontoRecibido');
    const total = parseFloat(pagoSeleccionado ? (pagoSeleccionado.monto_consulta || pagoSeleccionado.monto || 25) : 0);

    if (method === 'efectivo') {
        metodoPagoActual = 'Efectivo';
        btnEfectivo.classList.add('active');
        btnTarjeta.classList.remove('active');
        inputRecibido.readOnly = false;
        inputRecibido.value = total.toFixed(2);
    } else {
        metodoPagoActual = 'Tarjeta';
        btnTarjeta.classList.add('active');
        btnEfectivo.classList.remove('active');
        inputRecibido.value = total.toFixed(2);
        inputRecibido.readOnly = true;
    }
    calcularCambio();
}

function calcularCambio() {
    if (!pagoSeleccionado) return;

    const total = parseFloat(pagoSeleccionado.monto_consulta || pagoSeleccionado.monto || 25);
    const recibido = parseFloat(document.getElementById('inputMontoRecibido').value) || 0;
    const cambio = recibido - total;

    const inputCambio = document.getElementById('inputCambio');
    if (inputCambio) {
        inputCambio.value = cambio >= 0 ? `$${cambio.toFixed(2)}` : '$0.00';
    }
}

function procesarPago() {
    if (!pagoSeleccionado) {
        alert("Selecciona un paciente para generar la factura.");
        return;
    }

    const total = parseFloat(pagoSeleccionado.monto_consulta || pagoSeleccionado.monto || 25);
    const recibido = parseFloat(document.getElementById('inputMontoRecibido').value) || 0;

    if (metodoPagoActual === 'Efectivo' && recibido < total) {
        alert("El monto recibido no puede ser menor al total de la consulta.");
        return;
    }

    const numFactura = obtenerSiguienteNumeroFactura();
    const pId = pagoSeleccionado.paciente_id || pagoSeleccionado.id || pagoSeleccionado.consulta_id;
    const nombre = pagoSeleccionado.nombreCompleto || `${pagoSeleccionado.nombres || ''} ${pagoSeleccionado.apellidos || ''}`.trim();

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            Accion: "PROCESAR_PAGO_FACTURA",
            NumeroFactura: numFactura,
            PacienteId: pId,
            Paciente: nombre,
            MetodoPago: metodoPagoActual,
            MontoTotal: total,
            MontoRecibido: recibido,
            Cambio: metodoPagoActual === 'Tarjeta' ? 0 : (recibido - total)
        }));
    }

    incrementarNumeroFactura();

    let listaLocal = JSON.parse(localStorage.getItem('listaPagos') || '[]');
    listaLocal = listaLocal.filter(p => (p.paciente_id || p.id || p.consulta_id) !== pId);
    localStorage.setItem('listaPagos', JSON.stringify(listaLocal));

    pagoSeleccionado = null;
    document.getElementById('workspacePanelPago').style.display = 'none';
    document.getElementById('emptyWorkspacePago').style.display = 'block';

    renderizarListaPagos(listaLocal);
}

function openLogoutModal() {
    document.getElementById('logoutModal').style.display = 'flex';
}

function closeLogoutModal() {
    document.getElementById('logoutModal').style.display = 'none';
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

    if (rolNorm === 'admin' || rolNorm === 'administrador') return;

    if (rolNorm === 'doctor' || rolNorm === 'medico') {
        if (menuExpedientes) menuExpedientes.style.display = 'none';
        if (menuPago) menuPago.style.display = 'none';
    } else if (rolNorm === 'expedientes' || rolNorm === 'recepcion' || rolNorm.includes('recepcion')) {
        if (menuConsulta) menuConsulta.style.display = 'none';
        if (menuPago) menuPago.style.display = 'none';
    } else if (rolNorm === 'caja' || rolNorm.includes('caja')) {
        if (menuExpedientes) menuExpedientes.style.display = 'none';
        if (menuConsulta) menuConsulta.style.display = 'none';
    }
}