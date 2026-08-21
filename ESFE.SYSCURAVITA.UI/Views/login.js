const correoInput = document.getElementById('txtCorreo');
const passwordInput = document.getElementById('txtContrasena');
const togglePassword = document.getElementById('togglePassword');
const eyeIcon = document.getElementById('eyeIcon');
const eyeOffIcon = document.getElementById('eyeOffIcon');
const btnLogin = document.getElementById('btnLogin');

// Mostrar / Ocultar contraseña
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

// Función para procesar y enviar los datos a C#
function ejecutarInicioSesion() {
    const correoVal = correoInput.value.trim();
    const contrasenaVal = passwordInput.value.trim();

    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify({
            correo: correoVal,
            contrasena: contrasenaVal
        }));
    }
}

// Evento Click del botón
btnLogin.addEventListener('click', ejecutarInicioSesion);

// Permitir presionar la tecla Enter desde la contraseña
passwordInput.addEventListener('keypress', function (e) {
    if (e.key === 'Enter') {
        ejecutarInicioSesion();
    }
});