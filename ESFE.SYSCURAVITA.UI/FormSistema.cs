using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA.LN;
using ESFE.SYSCURAVITA_DAL;
using Microsoft.Web.WebView2.Core;

namespace ESFE.SYSCURAVITA.UI
{
    public partial class FormSistema : Form
    {
        private readonly AccesosEN? _usuario;
        private bool _esCierreDeSesion;

        private static readonly List<PacienteEN> _pacientesEnEspera = new();

        public FormSistema()
        {
            InitializeComponent();
            WindowState = FormWindowState.Maximized;
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

            await webView21.CoreWebView2.Profile.ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.CacheStorage |
                CoreWebView2BrowsingDataKinds.LocalStorage |
                CoreWebView2BrowsingDataKinds.WebSql
            );

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

                switch (mensaje)
                {
                    case "cerrarSesion":
                        _esCierreDeSesion = true;
                        Close();
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

                if (mensaje.StartsWith('{'))
                {
                    using var doc = JsonDocument.Parse(mensaje);
                    var root = doc.RootElement;

                    string accion = string.Empty;
                    if (root.TryGetProperty("accion", out var aMin)) accion = aMin.GetString() ?? string.Empty;
                    else if (root.TryGetProperty("Accion", out var aMay)) accion = aMay.GetString() ?? string.Empty;

                    if (accion == "guardar_expediente")
                    {
                        DateTime? fechaNacimiento = null;
                        if (root.TryGetProperty("fecha_nacimiento", out var fNac) && !string.IsNullOrWhiteSpace(fNac.GetString()))
                        {
                            if (DateTime.TryParse(fNac.GetString(), out DateTime fechaParseada))
                            {
                                fechaNacimiento = fechaParseada;
                            }
                        }

                        var nuevo = new PacienteEN
                        {
                            nombres = root.TryGetProperty("nombres", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                            apellidos = root.TryGetProperty("apellidos", out var a) ? a.GetString() ?? string.Empty : string.Empty,
                            dui_documento = root.TryGetProperty("dui_documento", out var d) ? d.GetString() ?? string.Empty : string.Empty,
                            telefono = root.TryGetProperty("telefono", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                            fecha_nacimiento = fechaNacimiento
                        };

                        if (PacienteLN.Guardar(nuevo))
                        {
                            await CargarTablaPacientesAsync();
                            await webView21.ExecuteScriptAsync("document.getElementById('createForm')?.reset();");
                        }
                        else
                        {
                            MessageBox.Show("No se pudo guardar el expediente. Revise los campos ingresados.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else if (accion == "ELIMINAR_PACIENTE")
                    {
                        int pacienteId = 0;
                        if (root.TryGetProperty("PacienteId", out var pId)) pacienteId = pId.GetInt32();
                        else if (root.TryGetProperty("pacienteId", out var pIdMin)) pacienteId = pIdMin.GetInt32();

                        if (PacienteLN.Eliminar(pacienteId))
                        {
                            _pacientesEnEspera.RemoveAll(p => p.paciente_id == pacienteId);

                            var eventoEliminado = new
                            {
                                Accion = "REGISTRO_ELIMINADO",
                                PacienteId = pacienteId
                            };

                            string jsonNotificacion = JsonSerializer.Serialize(eventoEliminado);
                            webView21.CoreWebView2.PostWebMessageAsJson(jsonNotificacion);
                            await CargarTablaPacientesAsync();
                        }
                    }
                    else if (accion == "ENVIAR_A_CONSULTA")
                    {
                        int pacienteId = 0;
                        if (root.TryGetProperty("pacienteId", out var pId)) pacienteId = pId.GetInt32();
                        else if (root.TryGetProperty("PacienteId", out var pIdMay)) pacienteId = pIdMay.GetInt32();

                        var todosPacientes = PacienteLN.ObtenerTodos();
                        var paciente = todosPacientes.FirstOrDefault(p => p.paciente_id == pacienteId);

                        if (paciente != null && !_pacientesEnEspera.Any(p => p.paciente_id == paciente.paciente_id))
                        {
                            _pacientesEnEspera.Add(paciente);
                        }
                    }
                    else if (accion == "REMOVER_DE_CONSULTA")
                    {
                        int pacienteId = 0;
                        if (root.TryGetProperty("pacienteId", out var pId)) pacienteId = pId.GetInt32();
                        else if (root.TryGetProperty("PacienteId", out var pIdMay)) pacienteId = pIdMay.GetInt32();

                        _pacientesEnEspera.RemoveAll(p => p.paciente_id == pacienteId);
                        await CargarListaEsperaConsultaAsync();
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

                        string codigoExpediente = string.Empty;
                        if (root.TryGetProperty("codigoExpediente", out var cExp)) codigoExpediente = cExp.GetString() ?? string.Empty;
                        else if (root.TryGetProperty("codigo_expediente", out var cExp2)) codigoExpediente = cExp2.GetString() ?? string.Empty;

                        string diagnostico = string.Empty;
                        if (root.TryGetProperty("Diagnostico", out var diag)) diagnostico = diag.GetString() ?? string.Empty;
                        else if (root.TryGetProperty("diagnostico", out var dMin)) diagnostico = dMin.GetString() ?? string.Empty;

                        // Extracción de Signos Vitales y Receta enviados desde JavaScript
                        string pa = root.TryGetProperty("PA", out var pPA) ? pPA.GetString() ?? "N/A" : "N/A";
                        string fc = root.TryGetProperty("FC", out var pFC) ? pFC.GetString() ?? "N/A" : "N/A";
                        string temp = root.TryGetProperty("Temperatura", out var pTemp) ? pTemp.GetString() ?? "N/A" : "N/A";
                        string peso = root.TryGetProperty("Peso", out var pPeso) ? pPeso.GetString() ?? "N/A" : "N/A";
                        string receta = root.TryGetProperty("Receta", out var pReceta) ? pReceta.GetString() ?? "" : "";

                        // Se envían todos los parámetros completos a la Capa de Negocio / DAL
                        bool guardado = ConsultaLN.GuardarDiagnostico(pacienteId, codigoExpediente, diagnostico, pa, fc, temp, peso, receta);

                        var respuesta = new
                        {
                            Accion = "CONSULTA_GUARDADA",
                            Exito = guardado
                        };

                        webView21.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(respuesta));
                    }
                    else if (accion == "OBTENER_HISTORIAL")
                    {
                        int pacienteId = 0;
                        if (root.TryGetProperty("PacienteId", out var pId)) pacienteId = pId.GetInt32();
                        else if (root.TryGetProperty("pacienteId", out var pIdMin)) pacienteId = pIdMin.GetInt32();

                        string codigoExpediente = string.Empty;
                        if (root.TryGetProperty("codigoExpediente", out var cExp)) codigoExpediente = cExp.GetString() ?? string.Empty;
                        else if (root.TryGetProperty("codigo_expediente", out var cExp2)) codigoExpediente = cExp2.GetString() ?? string.Empty;

                        var historialFiltrado = ConsultaLN.ObtenerHistorial(pacienteId, codigoExpediente);

                        var respuesta = new
                        {
                            Accion = "CARGAR_HISTORIAL",
                            Historial = historialFiltrado
                        };

                        string jsonRespuesta = JsonSerializer.Serialize(respuesta);
                        webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
                    }
                    else if (accion == "PROCESAR_PAGO_FACTURA")
                    {
                        var solicitud = new SolicitudPagoDTO
                        {
                            Accion = accion,
                            NumeroFactura = root.TryGetProperty("NumeroFactura", out var nFac) ? nFac.GetString() ?? string.Empty : string.Empty,
                            Paciente = root.TryGetProperty("Paciente", out var pac) ? pac.GetString() ?? string.Empty : string.Empty,
                            MetodoPago = root.TryGetProperty("MetodoPago", out var met) ? met.GetString() ?? string.Empty : string.Empty,
                            MontoTotal = root.TryGetProperty("MontoTotal", out var mnt) ? mnt.GetDecimal() : 0m
                        };

                        RespuestaPagoDTO respuesta = FacturaLN.ProcesarCobro(solicitud);

                        string jsonRespuesta = JsonSerializer.Serialize(respuesta);
                        webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en la comunicación con la interfaz: " + ex.Message, "Error de Sistema", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task CargarTablaPacientesAsync()
        {
            var lista = PacienteLN.ObtenerTodos();
            string jsonLista = JsonSerializer.Serialize(lista);
            await webView21.ExecuteScriptAsync($"if (typeof renderizarTabla === 'function') {{ renderizarTabla({jsonLista}); }}");
        }

        private async Task CargarListaEsperaConsultaAsync()
        {
            var respuesta = new
            {
                Accion = "CARGAR_LISTA_ESPERA",
                Pacientes = _pacientesEnEspera
            };
            string jsonRespuesta = JsonSerializer.Serialize(respuesta);
            webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
            await Task.CompletedTask;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        { base.OnFormClosing(e); if (_esCierreDeSesion) { if (Application.OpenForms["Form1"] is Form1 loginForm) { loginForm.LimpiarYLlamar(); } } else { Application.Exit(); } }

        private void webView21_Click(object? sender, EventArgs e) { }
    }
}