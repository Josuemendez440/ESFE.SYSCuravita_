using System;
using System.IO;
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
                        var nuevo = new PacienteEN
                        {
                            nombres = root.TryGetProperty("nombres", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                            apellidos = root.TryGetProperty("apellidos", out var a) ? a.GetString() ?? string.Empty : string.Empty,
                            dui_documento = root.TryGetProperty("dui_documento", out var d) ? d.GetString() ?? string.Empty : string.Empty,
                            telefono = root.TryGetProperty("telefono", out var t) ? t.GetString() ?? string.Empty : string.Empty
                        };

                        if (PacienteLN.Guardar(nuevo))
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

                        string codigoExpediente = string.Empty;
                        if (root.TryGetProperty("codigoExpediente", out var cExp)) codigoExpediente = cExp.GetString() ?? string.Empty;
                        else if (root.TryGetProperty("codigo_expediente", out var cExp2)) codigoExpediente = cExp2.GetString() ?? string.Empty;

                        string diagnostico = string.Empty;
                        if (root.TryGetProperty("Diagnostico", out var diag)) diagnostico = diag.GetString() ?? string.Empty;
                        else if (root.TryGetProperty("diagnostico", out var dMin)) diagnostico = dMin.GetString() ?? string.Empty;

                        bool guardado = ConsultaLN.GuardarDiagnostico(pacienteId, codigoExpediente, diagnostico);

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

                        // Recibe el RespuestaPagoDTO con el estado e información de la transacción
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
            var lista = PacienteLN.ObtenerTodos();
            var respuesta = new
            {
                Accion = "CARGAR_LISTA_ESPERA",
                Pacientes = lista
            };
            string jsonRespuesta = JsonSerializer.Serialize(respuesta);
            webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
            await Task.CompletedTask;
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

        private void webView21_Click(object? sender, EventArgs e)
        {
        }
    }
}