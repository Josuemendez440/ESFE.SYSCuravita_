const correoInput = document.getElementById('txtCorreo');
const passwordInput = document.getElementById('txtContrasena');
const chkRecordar = document.getElementById('chkRecordar');
const togglePassword = document.getElementById('togglePassword');
const eyeIcon = document.getElementById('eyeIcon');
const eyeOffIcon = document.getElementById('eyeOffIcon');
const btnLogin = document.getElementById('btnLogin');
const errorMsg = document.getElementById('errorMsg');
const lnkOlvidaste = document.getElementById('lnkOlvidaste');

// Alternar visibilidad de contraseña
togglePassword.addEventListener('click', function (e) {
    e.stopPropagation();
    const estaOculto = passwordInput.getAttribute('type') === 'password';

    if (estaOculto) {
        passwordInput.setAttribute('type', 'text');
        eyeOffIcon.style.display = 'none';
        eyeIcon.style.display = 'block';
        togglePassword.setAttribute('title', 'Ocultar contraseña');
    } else {
        passwordInput.setAttribute('type', 'password');
        eyeIcon.style.display = 'none';
        eyeOffIcon.style.display = 'block';
        togglePassword.setAttribute('title', 'Mostrar contraseña');
    }
});

// Procesar y transmitir datos a C# vía WebView2
function ejecutarInicioSesion() {
    const correoVal = correoInput.value.trim();
    const contrasenaVal = passwordInput.value.trim();

    // Validar que no haya campos vacíos antes de enviar
    if (!correoVal || !contrasenaVal) {
        errorMsg.style.display = 'block';
        return;
    }

    errorMsg.style.display = 'none';

    // Objeto JSON enriquecido para C#
    const payload = {
        accion: "iniciar_sesion",
        correo: correoVal,
        contrasena: contrasenaVal,
        recordar: chkRecordar ? chkRecordar.checked : false
    };

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(payload));
    } else {
        console.log("Modo desarrollo/browser detectado:", payload);
    }
}

// Acción del enlace "Olvidaste tu contraseña"
lnkOlvidaste.addEventListener('click', function (e) {
    e.preventDefault();
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({ accion: "recuperar_contrasena" }));
    }
});

// Enviar formulario al presionar click
btnLogin.addEventListener('click', ejecutarInicioSesion);

// Enviar formulario al pulsar 'Enter' en cualquier input
passwordInput.addEventListener('keypress', (e) => { if (e.key === 'Enter') ejecutarInicioSesion(); });
correoInput.addEventListener('keypress', (e) => { if (e.key === 'Enter') ejecutarInicioSesion(); });