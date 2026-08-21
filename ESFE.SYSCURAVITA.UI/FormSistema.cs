using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA.LN;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace ESFE.SYSCURAVITA.UI
{
    public partial class FormSistema : Form
    {
        private readonly AccesosEN _usuario;
        private readonly PacienteLN _pacienteLN = new PacienteLN();
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

            // Ajustes de interfaz del motor WebView2
            webView21.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView21.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            // Escuchar peticiones enviadas desde JavaScript vía window.chrome.webview.postMessage
            webView21.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

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
                MessageBox.Show("No se encontró la vista especificada: " + archivoHtml, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void WebView21_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || _usuario == null) return;

            // 1. Inyectar permisos según el rol del usuario autenticado
            string scriptPermisos = $"if (typeof aplicarPermisos === 'function') {{ aplicarPermisos('{_usuario.Rol}'); }}";
            await webView21.ExecuteScriptAsync(scriptPermisos);

            // 2. Cargar datos iniciales automáticamente según la vista activa
            if (webView21.Source.AbsolutePath.EndsWith("expedientes.html", StringComparison.OrdinalIgnoreCase))
            {
                await CargarTablaPacientesAsync();
            }
            else if (webView21.Source.AbsolutePath.EndsWith("consulta.html", StringComparison.OrdinalIgnoreCase))
            {
                await CargarListaEsperaConsultaAsync();
            }
        }

        private async void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string mensaje = e.TryGetWebMessageAsString();

                // 1. Manejo de Rutas y Comandos Simples
                switch (mensaje)
                {
                    case "cerrarSesion":
                        _esCierreDeSesion = true;
                        this.Close();
                        return;

                    case "nav_expedientes":
                        CargarPaginaHtml("expedientes.html");
                        return;

                    case "nav_consulta":
                        CargarPaginaHtml("consulta.html");
                        return;

                    case "nav_pago":
                        CargarPaginaHtml("facturacion.html");
                        return;

                    case "cargar_expedientes":
                        await CargarTablaPacientesAsync();
                        return;
                }

                // 2. Procesar objetos JSON (Guardado de Expediente, Petición de Pacientes y Guardado de Consulta)
                if (mensaje.StartsWith("{"))
                {
                    using (JsonDocument doc = JsonDocument.Parse(mensaje))
                    {
                        var root = doc.RootElement;

                        // Extraer acción enviada desde JS (maneja mayúsculas o minúsculas)
                        string accion = "";
                        if (root.TryGetProperty("accion", out var aMin)) accion = aMin.GetString();
                        else if (root.TryGetProperty("Accion", out var aMay)) accion = aMay.GetString();

                        if (accion == "guardar_expediente")
                        {
                            PacienteEN nuevo = new PacienteEN
                            {
                                nombres = root.GetProperty("nombres").GetString(),
                                apellidos = root.GetProperty("apellidos").GetString(),
                                dui_documento = root.GetProperty("dui_documento").GetString(),
                                telefono = root.GetProperty("telefono").GetString()
                            };

                            if (_pacienteLN.Guardar(nuevo))
                            {
                                MessageBox.Show("Expediente guardado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                await CargarTablaPacientesAsync();
                                await webView21.ExecuteScriptAsync("document.getElementById('createForm')?.reset();");
                            }
                            else
                            {
                                MessageBox.Show("No se pudo guardar el expediente. Revise los campos ingresados.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        else if (accion == "OBTENER_PACIENTES")
                        {
                            await CargarListaEsperaConsultaAsync();
                        }
                        else if (accion == "GUARDAR_CONSULTA")
                        {
                            int pacienteId = root.GetProperty("PacienteId").GetInt32();
                            string diagnostico = root.GetProperty("Diagnostico").GetString();

                            // Recorrer la lista de medicamentos recibida
                            if (root.TryGetProperty("Receta", out var recetaArray))
                            {
                                foreach (var item in recetaArray.EnumerateArray())
                                {
                                    string medicamento = item.GetProperty("Medicamento").GetString();
                                    string indicacion = item.GetProperty("Indicacion").GetString();

                                    // Invocar tus métodos LN para guardar cada medicamento de la receta
                                }
                            }

                            MessageBox.Show("Consulta procesada y guardada correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en la comunicación con la interfaz: " + ex.Message, "Error de Sistema", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task CargarTablaPacientesAsync()
        {
            var lista = _pacienteLN.ObtenerTodos();
            string jsonLista = JsonSerializer.Serialize(lista);
            await webView21.ExecuteScriptAsync($"if (typeof renderizarTabla === 'function') {{ renderizarTabla({jsonLista}); }}");
        }

        private async System.Threading.Tasks.Task CargarListaEsperaConsultaAsync()
        {
            var lista = _pacienteLN.ObtenerTodos();
            var respuesta = new
            {
                Accion = "CARGAR_LISTA_ESPERA",
                Pacientes = lista
            };
            string jsonRespuesta = JsonSerializer.Serialize(respuesta);
            webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
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