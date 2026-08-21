// 1. Carga inicial de rol
document.addEventListener('DOMContentLoaded', () => {
    const rolGuardado = localStorage.getItem('usuarioRol');
    if (rolGuardado) {
        aplicarPermisos(rolGuardado);
    }
});

// 2. Asignación de rol desde C#
function establecerUsuario(rol) {
    localStorage.setItem('usuarioRol', rol);
    aplicarPermisos(rol);
}

function navegar(accion) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(accion);
    }
}

function selectMethod(method) {
    const btnEfectivo = document.getElementById('btnEfectivo');
    const btnTarjeta = document.getElementById('btnTarjeta');
    if (method === 'efectivo') {
        btnEfectivo.classList.add('active');
        btnTarjeta.classList.remove('active');
    } else {
        btnTarjeta.classList.add('active');
        btnEfectivo.classList.remove('active');
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

    if (rolNorm === 'doctor' || rolNorm === 'medico') {
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
    }
});