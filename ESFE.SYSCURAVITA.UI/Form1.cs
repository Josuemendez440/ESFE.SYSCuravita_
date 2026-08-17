using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA.LN;
using Microsoft.Web.WebView2.Core;

namespace ESFE.SYSCURAVITA.UI
{
    public partial class Form1 : Form
    {
        private readonly AutenticacionLN _usuarioLN = new AutenticacionLN();

        public Form1()
        {
            InitializeComponent();

            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.ClientSize = new Size(460, 620);

            CargarVistaLogin();
        }

        private async void CargarVistaLogin()
        {
            await webView21.EnsureCoreWebView2Async(null);
            webView21.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            string rutaHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "login.html");
            if (File.Exists(rutaHtml))
            {
                webView21.Source = new Uri(rutaHtml);
            }
            else
            {
                MessageBox.Show("No se encontró el archivo login.html en la carpeta Views", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string jsonMensaje = e.TryGetWebMessageAsString();
                var datos = JsonSerializer.Deserialize<PeticionLogin>(jsonMensaje);

                if (datos != null)
                {
                    if (string.IsNullOrWhiteSpace(datos.correo) || string.IsNullOrWhiteSpace(datos.contrasena))
                    {
                        MessageBox.Show("Por favor, ingrese su correo y contraseña.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // 1. Armamos el objeto AccesosEN con las credenciales ingresadas
                    AccesosEN datosLogin = new AccesosEN
                    {
                        correo = datos.correo,
                        password_hash = datos.contrasena
                    };

                    // 2. Enviamos el objeto único a la Capa de Lógica de Negocio
                    AccesosEN usuarioValido = _usuarioLN.Autenticar(datosLogin);

                    if (usuarioValido != null)
                    {
                        // Ocultar formulario de Login
                        this.Hide();

                        // Abrir FormSistema pasando el usuario autenticado
                        FormSistema sistema = new FormSistema(usuarioValido);
                        sistema.Show();
                        sistema.BringToFront();
                        sistema.Activate();
                    }
                    else
                    {
                        MessageBox.Show("Correo o contraseña incorrectos.", "Acceso Denegado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al procesar el inicio de sesión: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Muestra el Login y vacía las cajas de texto en JavaScript al cerrar sesión.
        /// </summary>
        public async void LimpiarYLlamar()
        {
            this.Show();
            this.BringToFront();
            this.Activate();

            if (webView21 != null && webView21.CoreWebView2 != null)
            {
                string scriptLimpiar = @"
                    if (document.getElementById('txtCorreo')) document.getElementById('txtCorreo').value = '';
                    if (document.getElementById('txtContrasena')) document.getElementById('txtContrasena').value = '';
                ";
                await webView21.ExecuteScriptAsync(scriptLimpiar);
            }
        }

        private void webView21_Click(object sender, EventArgs e)
        {
        }
    }

    public class PeticionLogin
    {
        public string correo { get; set; }
        public string contrasena { get; set; }
    }
}