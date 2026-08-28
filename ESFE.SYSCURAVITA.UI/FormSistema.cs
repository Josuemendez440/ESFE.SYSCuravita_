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

                // 1. Mensajes en texto plano
                switch (mensaje)
                {
                    case "cerrarSesion":
                    case "cerrar_sesion":
                        _esCierreDeSesion = true;
                        Close();
                        return;

                    case "nav_expedientes":
                    case "expedientes":
                        CargarPaginaHtml("expedientes.html");
                        return;

                    case "nav_consulta":
                    case "consulta":
                        CargarPaginaHtml("consulta.html");
                        return;

                    case "nav_pago":
                    case "pago":
                    case "facturacion":
                        CargarPaginaHtml("facturacion.html");
                        return;

                    case "cargar_expedientes":
                    case "obtener_pacientes":
                        await CargarTablaPacientesAsync();
                        return;
                }

                // 2. Objetos JSON enviados por Frontend JavaScript
                if (mensaje.StartsWith('{'))
                {
                    using var doc = JsonDocument.Parse(mensaje);
                    var root = doc.RootElement;

                    string accion = ObtenerString(root, "accion", "Accion");

                    if (accion.Equals("NAVEGAR", StringComparison.OrdinalIgnoreCase))
                    {
                        string modulo = ObtenerString(root, "modulo", "Modulo");

                        switch (modulo.ToLower())
                        {
                            case "expedientes":
                            case "nav_expedientes":
                                CargarPaginaHtml("expedientes.html");
                                break;
                            case "consulta":
                            case "nav_consulta":
                                CargarPaginaHtml("consulta.html");
                                break;
                            case "pago":
                            case "facturacion":
                            case "nav_pago":
                                CargarPaginaHtml("facturacion.html");
                                break;
                            case "cerrarsesion":
                            case "cerrar_sesion":
                                _esCierreDeSesion = true;
                                Close();
                                break;
                        }
                        return;
                    }
                    else if (accion.Equals("obtener_pacientes", StringComparison.OrdinalIgnoreCase))
                    {
                        await CargarListaEsperaConsultaAsync();
                    }
                    else if (accion.Equals("guardar_expediente", StringComparison.OrdinalIgnoreCase))
                    {
                        DateTime? fechaNacimiento = null;
                        string fNacStr = ObtenerString(root, "fecha_nacimiento", "fechaNacimiento");
                        if (!string.IsNullOrWhiteSpace(fNacStr) && DateTime.TryParse(fNacStr, out DateTime fechaParseada))
                        {
                            fechaNacimiento = fechaParseada;
                        }

                        var nuevo = new PacienteEN
                        {
                            nombres = ObtenerString(root, "nombres"),
                            apellidos = ObtenerString(root, "apellidos"),
                            dui_documento = ObtenerString(root, "dui_documento", "duiDocumento"),
                            telefono = ObtenerString(root, "telefono"),
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
                    else if (accion.Equals("ELIMINAR_PACIENTE", StringComparison.OrdinalIgnoreCase))
                    {
                        int pacienteId = ObtenerInt(root, "PacienteId", "pacienteId");

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
                    else if (accion.Equals("ENVIAR_A_CONSULTA", StringComparison.OrdinalIgnoreCase) || accion.Equals("crear_consulta", StringComparison.OrdinalIgnoreCase))
                    {
                        int pacienteId = ObtenerInt(root, "pacienteId", "PacienteId");

                        var todosPacientes = PacienteLN.ObtenerTodos();
                        var paciente = todosPacientes.FirstOrDefault(p => p.paciente_id == pacienteId);

                        if (paciente != null && !_pacientesEnEspera.Any(p => p.paciente_id == paciente.paciente_id))
                        {
                            _pacientesEnEspera.Add(paciente);
                        }
                    }
                    else if (accion.Equals("REMOVER_DE_CONSULTA", StringComparison.OrdinalIgnoreCase))
                    {
                        int pacienteId = ObtenerInt(root, "pacienteId", "PacienteId");

                        _pacientesEnEspera.RemoveAll(p => p.paciente_id == pacienteId);
                        await CargarListaEsperaConsultaAsync();
                    }
                    else if (accion.Equals("OBTENER_PACIENTES", StringComparison.OrdinalIgnoreCase))
                    {
                        await CargarListaEsperaConsultaAsync();
                    }
                    else if (accion.Equals("GUARDAR_CONSULTA", StringComparison.OrdinalIgnoreCase))
                    {
                        int pacienteId = ObtenerInt(root, "PacienteId", "pacienteId");
                        string codigoExpediente = ObtenerString(root, "CodigoExpediente", "codigoExpediente", "codigo_expediente");
                        string diagnostico = ObtenerString(root, "Diagnostico", "diagnostico");

                        // Signos Vitales
                        string paSistolica = ObtenerString(root, "PresionSistolica", "PA");
                        string paDiastolica = ObtenerString(root, "PresionDiastolica");
                        string pa = (!string.IsNullOrEmpty(paSistolica) && !string.IsNullOrEmpty(paDiastolica))
                            ? $"{paSistolica}/{paDiastolica}"
                            : (!string.IsNullOrEmpty(paSistolica) ? paSistolica : "N/A");

                        string fc = ObtenerString(root, "FC", "fc");
                        if (string.IsNullOrEmpty(fc)) fc = "N/A";

                        string temp = ObtenerString(root, "Temp", "Temperatura", "temp");
                        if (string.IsNullOrEmpty(temp)) temp = "N/A";

                        string peso = ObtenerString(root, "Peso", "peso");
                        if (string.IsNullOrEmpty(peso)) peso = "N/A";

                        // Receta
                        string recetaText = "";
                        if (root.TryGetProperty("Medicamentos", out var medsProp) && medsProp.ValueKind == JsonValueKind.Array)
                        {
                            var listaMeds = new List<string>();
                            foreach (var med in medsProp.EnumerateArray())
                            {
                                string medNombre = ObtenerString(med, "Medicamento", "medicamento");
                                string medDosis = ObtenerString(med, "IndicacionesDosis", "indicacionesDosis", "dosis");
                                if (!string.IsNullOrEmpty(medNombre))
                                {
                                    listaMeds.Add(string.IsNullOrEmpty(medDosis) ? medNombre : $"{medNombre} ({medDosis})");
                                }
                            }
                            recetaText = string.Join(", ", listaMeds);
                        }
                        else
                        {
                            recetaText = ObtenerString(root, "Receta", "receta");
                        }

                        bool guardado = ConsultaLN.GuardarDiagnostico(pacienteId, codigoExpediente, diagnostico, pa, fc, temp, peso, recetaText);

                        var respuesta = new
                        {
                            Accion = "CONSULTA_GUARDADA",
                            Exito = guardado
                        };

                        webView21.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(respuesta));
                    }
                    else if (accion.Equals("OBTENER_HISTORIAL", StringComparison.OrdinalIgnoreCase))
                    {
                        int pacienteId = ObtenerInt(root, "PacienteId", "pacienteId");
                        string codigoExpediente = ObtenerString(root, "CodigoExpediente", "codigoExpediente", "codigo_expediente");

                        var historialFiltrado = ConsultaLN.ObtenerHistorial(pacienteId, codigoExpediente);

                        var respuesta = new
                        {
                            Accion = "CARGAR_HISTORIAL",
                            Historial = historialFiltrado
                        };

                        string jsonRespuesta = JsonSerializer.Serialize(respuesta);
                        webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
                    }
                    else if (accion.Equals("PROCESAR_PAGO_FACTURA", StringComparison.OrdinalIgnoreCase))
                    {
                        var solicitud = new SolicitudPagoDTO
                        {
                            Accion = accion,
                            NumeroFactura = ObtenerString(root, "NumeroFactura", "numeroFactura"),
                            PacienteId = ObtenerInt(root, "PacienteId", "pacienteId"),
                            Paciente = ObtenerString(root, "Paciente", "paciente"),
                            MetodoPago = ObtenerString(root, "MetodoPago", "metodoPago"),
                            MontoTotal = ObtenerDecimal(root, "MontoTotal", "montoTotal"),
                            MontoRecibido = ObtenerDecimal(root, "MontoRecibido", "montoRecibido"),
                            Cambio = ObtenerDecimal(root, "Cambio", "cambio")
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

        #region Métodos Auxiliares para Parsing Seguro de JSON
        private static string ObtenerString(JsonElement root, params string[] propiedades)
        {
            foreach (var prop in propiedades)
            {
                if (root.TryGetProperty(prop, out var elem))
                {
                    if (elem.ValueKind == JsonValueKind.String) return elem.GetString() ?? string.Empty;
                    if (elem.ValueKind == JsonValueKind.Number) return elem.ToString();
                    if (elem.ValueKind == JsonValueKind.True || elem.ValueKind == JsonValueKind.False) return elem.GetBoolean().ToString();
                }
            }
            return string.Empty;
        }

        private static int ObtenerInt(JsonElement root, params string[] propiedades)
        {
            foreach (var prop in propiedades)
            {
                if (root.TryGetProperty(prop, out var elem))
                {
                    if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out int valInt)) return valInt;
                    if (elem.ValueKind == JsonValueKind.String && int.TryParse(elem.GetString(), out int parsedInt)) return parsedInt;
                }
            }
            return 0;
        }

        private static decimal ObtenerDecimal(JsonElement root, params string[] propiedades)
        {
            foreach (var prop in propiedades)
            {
                if (root.TryGetProperty(prop, out var elem))
                {
                    if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDecimal(out decimal valDec)) return valDec;
                    if (elem.ValueKind == JsonValueKind.String && decimal.TryParse(elem.GetString(), out decimal parsedDec)) return parsedDec;
                }
            }
            return 0m;
        }
        #endregion

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

        private void webView21_Click(object? sender, EventArgs e) { }
    }
}