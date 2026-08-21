using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA.LN;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
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

        // Almacenamiento temporal en memoria para consultas activas
        private static readonly List<ConsultaEN> _listaConsultasTemp = new List<ConsultaEN>();

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

        private async void WebView21_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
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

        private async void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string mensaje = e.TryGetWebMessageAsString();

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

                if (mensaje.StartsWith("{"))
                {
                    using (JsonDocument doc = JsonDocument.Parse(mensaje))
                    {
                        var root = doc.RootElement;

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
                            int pacienteId = 0;
                            if (root.TryGetProperty("PacienteId", out var pId)) pacienteId = pId.GetInt32();
                            else if (root.TryGetProperty("pacienteId", out var pIdMin)) pacienteId = pIdMin.GetInt32();

                            string diagnostico = "";
                            if (root.TryGetProperty("Diagnostico", out var d)) diagnostico = d.GetString();
                            else if (root.TryGetProperty("diagnostico", out var dMin)) diagnostico = dMin.GetString();

                            _listaConsultasTemp.Add(new ConsultaEN
                            {
                                PacienteId = pacienteId,
                                Diagnostico = string.IsNullOrWhiteSpace(diagnostico) ? "Sin diagnóstico especificado" : diagnostico,
                                Fecha = DateTime.Now.ToString("dd/MM/yyyy"),
                                Hora = DateTime.Now.ToString("hh:mm tt"),
                                Observaciones = "Consulta procesada correctamente."
                            });

                            MessageBox.Show("Consulta procesada y guardada correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else if (accion == "OBTENER_HISTORIAL")
                        {
                            int pacienteId = 0;
                            if (root.TryGetProperty("PacienteId", out var pId)) pacienteId = pId.GetInt32();
                            else if (root.TryGetProperty("pacienteId", out var pIdMin)) pacienteId = pIdMin.GetInt32();

                            var historialFiltrado = _listaConsultasTemp
                                .FindAll(c => c.PacienteId == pacienteId)
                                .ConvertAll(c => new
                                {
                                    fecha = c.Fecha,
                                    hora = c.Hora,
                                    diagnostico = c.Diagnostico,
                                    observaciones = c.Observaciones
                                });

                            var respuesta = new
                            {
                                Accion = "CARGAR_HISTORIAL",
                                Historial = historialFiltrado
                            };

                            string jsonRespuesta = JsonSerializer.Serialize(respuesta);
                            webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
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