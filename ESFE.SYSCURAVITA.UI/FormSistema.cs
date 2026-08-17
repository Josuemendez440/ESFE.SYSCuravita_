using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA.LN;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Windows.Forms;

namespace ESFE.SYSCURAVITA.UI
{
    public partial class FormSistema : Form
    {
        private readonly AccesosEN _usuario;
        private bool _esCierreDeSesion = false;

        public FormSistema()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
        }

        public FormSistema(AccesosEN usuario) : this()
        {
            _usuario = usuario;
            webView21.NavigationCompleted += WebView21_NavigationCompleted;
            CargarVistaInicial();
        }

        private async void CargarVistaInicial()
        {
            if (_usuario == null) return;

            await webView21.EnsureCoreWebView2Async(null);

            // Oculta la barra de estado (URL amarilla/negra)
            webView21.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Desactiva el menú contextual del clic derecho (con 's' al final)
            webView21.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            webView21.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // Si es Admin y no tiene vista asignada, entra por defecto a expedientes
            string vistaInicial = !string.IsNullOrEmpty(_usuario.VistaHtml)
                ? _usuario.VistaHtml
                : "expedientes.html";

            CargarPaginaHtml(vistaInicial);
        }

        private void CargarPaginaHtml(string archivoHtml)
        {
            string rutaHtml = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", archivoHtml);
            if (File.Exists(rutaHtml))
            {
                webView21.Source = new Uri(rutaHtml);
            }
            else
            {
                MessageBox.Show("No se encontró el archivo vista: " + archivoHtml, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void WebView21_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && _usuario != null)
            {
                // Inyecta el rol del usuario a la función JavaScript de la vista actual
                string script = $"if (typeof aplicarPermisos === 'function') {{ aplicarPermisos('{_usuario.Rol}'); }}";
                await webView21.ExecuteScriptAsync(script);
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string mensaje = e.TryGetWebMessageAsString();

            switch (mensaje)
            {
                case "cerrarSesion":
                    _esCierreDeSesion = true;
                    this.Close();
                    break;

                case "nav_expedientes":
                    CargarPaginaHtml("expedientes.html");
                    break;

                case "nav_consulta":
                    CargarPaginaHtml("consulta.html");
                    break;

                case "nav_pago":
                    CargarPaginaHtml("facturacion.html");
                    break;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (_esCierreDeSesion)
            {
                if (Application.OpenForms["Form1"] is Form1 loginForm)
                {
                    loginForm.LimpiarYLlamar();
                }
            }
            else
            {
                Application.Exit();
            }
        }

        private void webView21_Click(object sender, EventArgs e)
        {
        }
    }
}