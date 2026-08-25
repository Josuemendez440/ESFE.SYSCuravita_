using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA.LN;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ESFE.SYSCURAVITA.UI
{
    public partial class FormSistema : Form
    {
        private readonly AccesosEN? _usuario;
        private readonly PacienteLN _pacienteLN = new PacienteLN();
        private readonly ConsultaLN _consultaLN = new ConsultaLN();
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

            webView21.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView21.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

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

        private async void WebView21_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || _usuario == null) return;

            string scriptPermisos = $"if (typeof aplicarPermisos === 'function') {{ aplicarPermisos('{_usuario.Rol}'); }}";
            await webView21.ExecuteScriptAsync(scriptPermisos);

            if (webView21.Source.AbsolutePath.EndsWith("expedientes.html", StringComparison.OrdinalIgnoreCase))
            {
                await CargarTablaPacientesAsync();
            }
            else if (webView21.Source.AbsolutePath.EndsWith("consulta.html", StringComparison.OrdinalIgnoreCase))
            {
                await CargarListaEsperaConsultaAsync();
            }
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string mensaje = e.TryGetWebMessageAsString();
                string accion = "";
                JsonElement root = default;

                if (mensaje.StartsWith('{'))
                {
                    using JsonDocument doc = JsonDocument.Parse(mensaje);
                    root = doc.RootElement.Clone();

                    if (root.TryGetProperty("Accion", out var aMay)) accion = aMay.GetString() ?? "";
                    else if (root.TryGetProperty("accion", out var aMin)) accion = aMin.GetString() ?? "";
                }
                else
                {
                    accion = mensaje;
                }

                switch (accion.ToLowerInvariant())
                {
                    case "cerrarsesion":
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

                    case "obtener_pacientes":
                        await CargarListaEsperaConsultaAsync();
                        return;

                    case "guardar_expediente":
                        if (root.ValueKind == JsonValueKind.Object)
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
                                await webView21.ExecuteScriptAsync("if (typeof openSuccessModal === 'function') { openSuccessModal('Expediente guardado exitosamente.'); }");
                                await CargarTablaPacientesAsync();
                                await webView21.ExecuteScriptAsync("document.getElementById('createForm')?.reset();");
                            }
                            else
                            {
                                await webView21.ExecuteScriptAsync("if (typeof mostrarAlerta === 'function') { mostrarAlerta('Error', 'No se pudo guardar el expediente.', 'error'); }");
                            }
                        }
                        return;

                    case "guardar_consulta":
                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            int pacienteId = 0;
                            if (root.TryGetProperty("PacienteId", out var pId)) pacienteId = pId.GetInt32();
                            else if (root.TryGetProperty("pacienteId", out var pIdMin)) pacienteId = pIdMin.GetInt32();

                            // Extraer EstadoConsultaId enviada desde JavaScript
                            int estadoConsultaId = 1;
                            if (root.TryGetProperty("EstadoConsultaId", out var eId)) estadoConsultaId = eId.GetInt32();
                            else if (root.TryGetProperty("estadoConsultaId", out var eIdMin)) estadoConsultaId = eIdMin.GetInt32();

                            string diagnostico = "";
                            if (root.TryGetProperty("Diagnostico", out var d)) diagnostico = d.GetString() ?? "";
                            else if (root.TryGetProperty("diagnostico", out var dMin)) diagnostico = dMin.GetString() ?? "";

                            ConsultaEN consulta = new ConsultaEN
                            {
                                PacienteId = pacienteId,
                                EstadoConsultaId = estadoConsultaId,
                                Diagnostico = string.IsNullOrWhiteSpace(diagnostico) ? "Sin diagnóstico especificado" : diagnostico
                            };

                            int resultado = _consultaLN.GuardarConsulta(consulta);

                            if (resultado <= 0)
                            {
                                await webView21.ExecuteScriptAsync("if (typeof mostrarAlerta === 'function') { mostrarAlerta('Error de Base de Datos', 'No se pudo registrar la consulta.', 'error'); }");
                            }
                        }
                        return;

                    case "obtener_historial":
                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            int pacienteId = 0;
                            if (root.TryGetProperty("PacienteId", out var pId)) pacienteId = pId.GetInt32();
                            else if (root.TryGetProperty("pacienteId", out var pIdMin)) pacienteId = pIdMin.GetInt32();

                            var listaDB = _consultaLN.ObtenerHistorial(pacienteId);

                            var historialFiltrado = listaDB.ConvertAll(c => new
                            {
                                fecha = c.FechaConsulta.ToString("dd/MM/yyyy"),
                                hora = c.FechaConsulta.ToString("hh:mm tt"),
                                diagnostico = c.Diagnostico,
                                observaciones = "Consulta procesada correctamente."
                            });

                            var respuesta = new
                            {
                                Accion = "CARGAR_HISTORIAL",
                                Historial = historialFiltrado
                            };

                            string jsonRespuesta = JsonSerializer.Serialize(respuesta);
                            webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
                        }
                        return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en la comunicación con la interfaz: " + ex.Message, "Error de Sistema", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task CargarTablaPacientesAsync()
        {
            var lista = _pacienteLN.ObtenerTodos();
            string jsonLista = JsonSerializer.Serialize(lista);
            await webView21.ExecuteScriptAsync($"if (typeof renderizarTabla === 'function') {{ renderizarTabla({jsonLista}); }}");
        }

        private Task CargarListaEsperaConsultaAsync()
        {
            var lista = _pacienteLN.ObtenerTodos();
            var respuesta = new
            {
                Accion = "CARGAR_LISTA_ESPERA",
                Pacientes = lista
            };
            string jsonRespuesta = JsonSerializer.Serialize(respuesta);
            webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
            return Task.CompletedTask;
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
    }
}