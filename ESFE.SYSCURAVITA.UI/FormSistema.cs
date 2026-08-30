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
using Microsoft.Data.SqlClient;
using Microsoft.Web.WebView2.Core;

namespace ESFE.SYSCURAVITA.UI
{
    public partial class FormSistema : Form
    {
        private readonly AccesosEN? _usuario;
        private bool _esCierreDeSesion;
        private readonly string _rutaCarpetaRecetas;
        private readonly string _rutaCarpetaFacturas;
        private Microsoft.Web.WebView2.WinForms.WebView2? _pdfWebView;

        private static readonly List<PacienteEN> _pacientesEnEspera = [];

        public FormSistema()
        {
            InitializeComponent();
            WindowState = FormWindowState.Maximized;

            string rutaRaiz = ObtenerRutaRaizSolucion();
            _rutaCarpetaRecetas = Path.Combine(rutaRaiz, "Recetas");
            _rutaCarpetaFacturas = Path.Combine(rutaRaiz, "Facturas");

            Directory.CreateDirectory(_rutaCarpetaRecetas);
            Directory.CreateDirectory(_rutaCarpetaFacturas);

            InicializarCorrelativoSecuencia();
        }

        private static string ObtenerRutaRaizSolucion()
        {
            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "ESFE.SYSCURAVITA.slnx")) ||
                        File.Exists(Path.Combine(dir.FullName, "ESFE.SYSCURAVITA.sln")) ||
                        Directory.Exists(Path.Combine(dir.FullName, "ESFE.SYSCURAVITA.UI")))
                    {
                        return dir.FullName;
                    }
                    dir = dir.Parent;
                }
            }
            catch { }

            return AppDomain.CurrentDomain.BaseDirectory;
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

            try
            {
                _pdfWebView = new Microsoft.Web.WebView2.WinForms.WebView2
                {
                    Visible = false,
                    Width = 1024,
                    Height = 768
                };
                Controls.Add(_pdfWebView);
                await _pdfWebView.EnsureCoreWebView2Async(null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error inicializando WebView2 para generación de PDFs: " + ex.Message);
            }

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

                        string motivoConsulta = ObtenerString(root, "motivoConsulta", "motivo_consulta", "especialidadNombre", "especialidad");
                        if (string.IsNullOrWhiteSpace(motivoConsulta))
                        {
                            motivoConsulta = "Consulta General";
                        }

                        var todosPacientes = PacienteLN.ObtenerTodos();
                        var paciente = todosPacientes.FirstOrDefault(p => p.paciente_id == pacienteId);

                        if (paciente != null)
                        {
                            if (!_pacientesEnEspera.Any(p => p.paciente_id == paciente.paciente_id))
                            {
                                _pacientesEnEspera.Add(paciente);
                            }
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
                        decimal montoConsulta = ObtenerDecimal(root, "MontoConsulta", "montoConsulta", "monto_consulta", "Monto", "monto");

                        // CAPTURA DEL MOTIVO DE CONSULTA
                        string motivoConsulta = ObtenerString(root, "MotivoConsulta", "motivoConsulta", "motivo_consulta", "Especialidad", "especialidad");
                        if (string.IsNullOrWhiteSpace(motivoConsulta))
                        {
                            motivoConsulta = "Consulta General";
                        }

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
                        var medicamentos = new List<(string Medicamento, string Dosis)>();

                        if (root.TryGetProperty("Medicamentos", out var medsProp) && medsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var med in medsProp.EnumerateArray())
                            {
                                string medNombre = ObtenerString(med, "Medicamento", "medicamento", "nombre");
                                string medDosis = ObtenerString(med, "IndicacionesDosis", "indicacionesDosis", "dosis", "indicaciones");

                                if (!string.IsNullOrWhiteSpace(medNombre))
                                {
                                    medicamentos.Add((medNombre, medDosis));
                                }
                            }
                        }

                        // PASO DEL PARÁMETRO 'motivoConsulta' A LA CAPA LN
                        bool guardado = ConsultaLN.GuardarDiagnostico(
                            pacienteId,
                            codigoExpediente,
                            diagnostico,
                            pa,
                            fc,
                            temp,
                            peso,
                            montoConsulta,
                            medicamentos,
                            motivoConsulta
                        );

                        if (guardado)
                        {
                            int nuevoCorrelativo = ObtenerYSiguienteCorrelativo();
                            string numeroReceta = $"REC-{nuevoCorrelativo:D5}";
                            string numeroFactura = $"FAC-{nuevoCorrelativo:D5}";

                            try
                            {
                                // Obtener nombre del paciente
                                string nombrePaciente = ObtenerString(root, "PacienteNombre", "pacienteNombre", "NombrePaciente", "nombrePaciente", "Paciente", "paciente");
                                if (string.IsNullOrWhiteSpace(nombrePaciente))
                                {
                                    var pObj = PacienteLN.ObtenerTodos().FirstOrDefault(p => p.paciente_id == pacienteId);
                                    nombrePaciente = pObj != null ? $"{pObj.nombres} {pObj.apellidos}".Trim() : "Paciente";
                                }

                                string htmlReceta = GenerarHtmlReceta(
                                    numeroReceta,
                                    nombrePaciente,
                                    codigoExpediente,
                                    motivoConsulta,
                                    diagnostico,
                                    pa,
                                    fc,
                                    temp,
                                    peso,
                                    medicamentos
                                );

                                string codLimpio = string.Join("_", (string.IsNullOrWhiteSpace(codigoExpediente) ? "PAC" : codigoExpediente).Split(Path.GetInvalidFileNameChars()));
                                string nombreArchivoReceta = $"Receta_{numeroReceta}_{codLimpio}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                                string rutaPdfReceta = Path.Combine(_rutaCarpetaRecetas, nombreArchivoReceta);

                                await GenerarPdfDesdeHtmlAsync(htmlReceta, rutaPdfReceta);
                            }
                            catch (Exception exReceta)
                            {
                                System.Diagnostics.Debug.WriteLine("Error al generar PDF de receta: " + exReceta.Message);
                            }

                            var respuesta = new
                            {
                                Accion = "CONSULTA_GUARDADA",
                                Exito = true,
                                NumeroReceta = numeroReceta,
                                NumeroFactura = numeroFactura,
                                Correlativo = nuevoCorrelativo,
                                PacienteId = pacienteId
                            };

                            webView21.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(respuesta));
                        }
                        else
                        {
                            var respuesta = new
                            {
                                Accion = "CONSULTA_GUARDADA",
                                Exito = false
                            };
                            webView21.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(respuesta));
                        }
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
                    else if (accion.Equals("OBTENER_SIGUIENTE_NUMERO_FACTURA", StringComparison.OrdinalIgnoreCase))
                    {
                        string sigNumero = ObtenerSiguienteNumeroFactura();
                        var respFac = new
                        {
                            Accion = "SIGUIENTE_NUMERO_FACTURA",
                            NumeroFactura = sigNumero
                        };
                        webView21.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(respFac));
                    }
                    else if (accion.Equals("PROCESAR_PAGO_FACTURA", StringComparison.OrdinalIgnoreCase))
                    {
                        string numFacProcesar = ObtenerString(root, "NumeroFactura", "numeroFactura");
                        if (string.IsNullOrWhiteSpace(numFacProcesar) || numFacProcesar.Equals("FAC-00001", StringComparison.OrdinalIgnoreCase))
                        {
                            numFacProcesar = ObtenerSiguienteNumeroFactura();
                        }

                        var solicitud = new SolicitudPagoDTO
                        {
                            Accion = accion,
                            NumeroFactura = numFacProcesar,
                            PacienteId = ObtenerInt(root, "PacienteId", "pacienteId"),
                            Paciente = ObtenerString(root, "Paciente", "paciente"),
                            MetodoPago = ObtenerString(root, "MetodoPago", "metodoPago"),
                            MontoTotal = ObtenerDecimal(root, "MontoTotal", "montoTotal"),
                            MontoRecibido = ObtenerDecimal(root, "MontoRecibido", "montoRecibido"),
                            Cambio = ObtenerDecimal(root, "Cambio", "cambio")
                        };

                        RespuestaPagoDTO respuesta = FacturaLN.ProcesarCobro(solicitud);

                        if (respuesta.Exito)
                        {
                            try
                            {
                                string codigoExpediente = ObtenerString(root, "CodigoExpediente", "codigoExpediente", "codigo_expediente");
                                if (string.IsNullOrWhiteSpace(codigoExpediente))
                                {
                                    var pObj = PacienteLN.ObtenerTodos().FirstOrDefault(p => p.paciente_id == solicitud.PacienteId);
                                    codigoExpediente = pObj?.codigo_expediente ?? "N/A";
                                }

                                string especialidad = ObtenerString(root, "Especialidad", "especialidad", "especialidad_nombre", "MotivoConsulta", "motivoConsulta");
                                if (string.IsNullOrWhiteSpace(especialidad))
                                {
                                    especialidad = "Consulta Médica General";
                                }

                                string htmlFactura = GenerarHtmlFactura(
                                    solicitud.NumeroFactura,
                                    solicitud.Paciente,
                                    codigoExpediente,
                                    especialidad,
                                    solicitud.MetodoPago,
                                    solicitud.MontoTotal,
                                    solicitud.MontoRecibido,
                                    solicitud.Cambio
                                );

                                string facLimpio = string.Join("_", (string.IsNullOrWhiteSpace(solicitud.NumeroFactura) ? "FAC" : solicitud.NumeroFactura).Split(Path.GetInvalidFileNameChars()));
                                string nombreArchivoFactura = $"Factura_{facLimpio}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                                string rutaPdfFactura = Path.Combine(_rutaCarpetaFacturas, nombreArchivoFactura);

                                bool pdfGenerado = await GenerarPdfDesdeHtmlAsync(htmlFactura, rutaPdfFactura);
                                if (pdfGenerado)
                                {
                                    respuesta.RutaPdf = rutaPdfFactura;
                                }
                            }
                            catch (Exception exFactura)
                            {
                                System.Diagnostics.Debug.WriteLine("Error al generar PDF de factura: " + exFactura.Message);
                            }
                        }

                        string jsonRespuesta = JsonSerializer.Serialize(respuesta);
                        webView21.CoreWebView2.PostWebMessageAsJson(jsonRespuesta);
                    }
                    else if (accion.Equals("ENVIAR_FACTURA_CORREO", StringComparison.OrdinalIgnoreCase) || accion.Equals("ENVIAR_CORREO_FACTURA", StringComparison.OrdinalIgnoreCase))
                    {
                        string numFactura = ObtenerString(root, "NumeroFactura", "numeroFactura");
                        string correo = ObtenerString(root, "Correo", "correo", "Destinatario", "destinatario");
                        string paciente = ObtenerString(root, "Paciente", "paciente", "PacienteNombre", "pacienteNombre");
                        decimal montoTotal = ObtenerDecimal(root, "MontoTotal", "montoTotal");
                        string rutaPdf = ObtenerString(root, "RutaPdf", "rutaPdf");

                        if (string.IsNullOrWhiteSpace(rutaPdf) || !File.Exists(rutaPdf))
                        {
                            if (Directory.Exists(_rutaCarpetaFacturas))
                            {
                                string facLimpio = string.Join("_", (string.IsNullOrWhiteSpace(numFactura) ? "FAC" : numFactura).Split(Path.GetInvalidFileNameChars()));
                                var matchFiles = Directory.GetFiles(_rutaCarpetaFacturas, $"Factura_{facLimpio}*.pdf")
                                                          .OrderByDescending(f => File.GetCreationTime(f))
                                                          .FirstOrDefault();
                                if (matchFiles != null)
                                {
                                    rutaPdf = matchFiles;
                                }
                            }
                        }

                        var correoDto = new CorreoDTO
                        {
                            Destinatario = correo,
                            NumeroFactura = numFactura,
                            PacienteNombre = paciente,
                            MontoTotal = montoTotal,
                            RutaArchivoPdf = rutaPdf
                        };

                        var respEnvio = FacturaLN.EnviarFacturaPorCorreo(correoDto);
                        webView21.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(respEnvio));
                    }
                    else if (accion.Equals("ABRIR_PDF_FACTURA", StringComparison.OrdinalIgnoreCase) || accion.Equals("IMPRIMIR_PDF_FACTURA", StringComparison.OrdinalIgnoreCase))
                    {
                        string numFactura = ObtenerString(root, "NumeroFactura", "numeroFactura");
                        string rutaPdf = ObtenerString(root, "RutaPdf", "rutaPdf");

                        if (string.IsNullOrWhiteSpace(rutaPdf) || !File.Exists(rutaPdf))
                        {
                            if (Directory.Exists(_rutaCarpetaFacturas))
                            {
                                string facLimpio = string.Join("_", (string.IsNullOrWhiteSpace(numFactura) ? "FAC" : numFactura).Split(Path.GetInvalidFileNameChars()));
                                var matchFiles = Directory.GetFiles(_rutaCarpetaFacturas, $"Factura_{facLimpio}*.pdf")
                                                          .OrderByDescending(f => File.GetCreationTime(f))
                                                          .FirstOrDefault();
                                if (matchFiles != null)
                                {
                                    rutaPdf = matchFiles;
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(rutaPdf) && File.Exists(rutaPdf))
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = rutaPdf,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception exPdf)
                            {
                                MessageBox.Show("No se pudo abrir el archivo PDF: " + exPdf.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show("No se encontró el archivo PDF generado de la factura.", "Atención", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
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
                    if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out int valInt))
                        return valInt;

                    if (elem.ValueKind == JsonValueKind.String && int.TryParse(elem.GetString(), out int parsedInt))
                        return parsedInt;
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
                    if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDecimal(out decimal valDec))
                        return valDec;

                    if (elem.ValueKind == JsonValueKind.String && decimal.TryParse(elem.GetString(), out decimal parsedDec))
                        return parsedDec;
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

        #region Generación Dinámica de PDFs (Recetas y Facturas)
        private async Task<bool> GenerarPdfDesdeHtmlAsync(string htmlContenido, string rutaArchivoPdf)
        {
            try
            {
                if (_pdfWebView == null || _pdfWebView.CoreWebView2 == null)
                {
                    _pdfWebView = new Microsoft.Web.WebView2.WinForms.WebView2
                    {
                        Visible = false,
                        Width = 1024,
                        Height = 768
                    };
                    Controls.Add(_pdfWebView);
                    await _pdfWebView.EnsureCoreWebView2Async(null);
                }

                _pdfWebView.CoreWebView2.Navigate("about:blank");
                await Task.Delay(150);

                string scriptHtml = $"document.open(); document.write({JsonSerializer.Serialize(htmlContenido)}); document.close();";
                await _pdfWebView.ExecuteScriptAsync(scriptHtml);

                await Task.Delay(350);

                var printSettings = _pdfWebView.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true;
                printSettings.Orientation = CoreWebView2PrintOrientation.Portrait;
                printSettings.MarginTop = 0.4;
                printSettings.MarginBottom = 0.4;
                printSettings.MarginLeft = 0.4;
                printSettings.MarginRight = 0.4;

                return await _pdfWebView.CoreWebView2.PrintToPdfAsync(rutaArchivoPdf, printSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al generar PDF en '{rutaArchivoPdf}': {ex.Message}");
                return false;
            }
        }

        #region Autoincremento Seguro y Persistente de Facturas y Recetas (No se reinicia al borrar pacientes)
        private static readonly object _correlativoLock = new object();
        private static int _ultimoCorrelativo = -1;

        private void InicializarCorrelativoSecuencia()
        {
            lock (_correlativoLock)
            {
                if (_ultimoCorrelativo >= 0) return;

                int maxCorrelativo = 0;
                string rutaArchivoJson = Path.Combine(ObtenerRutaRaizSolucion(), "correlativo_secuencia.json");

                // 1. Leer desde archivo JSON persistente si existe
                try
                {
                    if (File.Exists(rutaArchivoJson))
                    {
                        string json = File.ReadAllText(rutaArchivoJson);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("UltimoCorrelativo", out var prop) && prop.TryGetInt32(out int val))
                        {
                            maxCorrelativo = Math.Max(maxCorrelativo, val);
                        }
                    }
                }
                catch { }

                // 2. Escanear carpeta de Facturas existentes
                try
                {
                    if (Directory.Exists(_rutaCarpetaFacturas))
                    {
                        var files = Directory.GetFiles(_rutaCarpetaFacturas, "*.pdf");
                        foreach (var file in files)
                        {
                            string filename = Path.GetFileName(file);
                            var match = System.Text.RegularExpressions.Regex.Match(filename, @"FAC-(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                            {
                                maxCorrelativo = Math.Max(maxCorrelativo, num);
                            }
                        }
                    }
                }
                catch { }

                // 3. Escanear carpeta de Recetas existentes
                try
                {
                    if (Directory.Exists(_rutaCarpetaRecetas))
                    {
                        var files = Directory.GetFiles(_rutaCarpetaRecetas, "*.pdf");
                        foreach (var file in files)
                        {
                            string filename = Path.GetFileName(file);
                            var match = System.Text.RegularExpressions.Regex.Match(filename, @"REC-(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                            {
                                maxCorrelativo = Math.Max(maxCorrelativo, num);
                            }
                        }
                    }
                }
                catch { }

                // 4. Escanear base de datos (Pagos y Recetas)
                try
                {
                    using var conn = ConexionDAL.ObtenerConexion();
                    conn.Open();
                    using var cmd1 = new SqlCommand("SELECT ISNULL(MAX(pago_id), 0) FROM [dbo].[Pagos]", conn);
                    var res1 = cmd1.ExecuteScalar();
                    if (res1 != null && res1 != DBNull.Value)
                    {
                        maxCorrelativo = Math.Max(maxCorrelativo, Convert.ToInt32(res1));
                    }

                    using var cmd2 = new SqlCommand("SELECT ISNULL(MAX(receta_id), 0) FROM [dbo].[Recetas]", conn);
                    var res2 = cmd2.ExecuteScalar();
                    if (res2 != null && res2 != DBNull.Value)
                    {
                        maxCorrelativo = Math.Max(maxCorrelativo, Convert.ToInt32(res2));
                    }
                }
                catch { }

                _ultimoCorrelativo = maxCorrelativo;
                GuardarCorrelativoSecuencia(_ultimoCorrelativo);
            }
        }

        private static void GuardarCorrelativoSecuencia(int valor)
        {
            try
            {
                string rutaArchivoJson = Path.Combine(ObtenerRutaRaizSolucion(), "correlativo_secuencia.json");
                var obj = new { UltimoCorrelativo = valor, Actualizado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                File.WriteAllText(rutaArchivoJson, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private int ObtenerYSiguienteCorrelativo()
        {
            lock (_correlativoLock)
            {
                if (_ultimoCorrelativo < 0) InicializarCorrelativoSecuencia();
                _ultimoCorrelativo++;
                GuardarCorrelativoSecuencia(_ultimoCorrelativo);
                return _ultimoCorrelativo;
            }
        }

        private int ObtenerCorrelativoActual()
        {
            lock (_correlativoLock)
            {
                if (_ultimoCorrelativo < 0) InicializarCorrelativoSecuencia();
                return _ultimoCorrelativo;
            }
        }

        private string ObtenerSiguienteNumeroFactura()
        {
            int corr = ObtenerCorrelativoActual() + 1;
            return $"FAC-{corr:D5}";
        }

        private string ObtenerSiguienteNumeroReceta()
        {
            int corr = ObtenerCorrelativoActual() + 1;
            return $"REC-{corr:D5}";
        }
        #endregion

        private static string? _logoBase64Cache;
        private static string ObtenerLogoBase64()
        {
            if (_logoBase64Cache != null) return _logoBase64Cache;
            try
            {
                string rutaRaiz = ObtenerRutaRaizSolucion();
                string rutaLogo = Path.Combine(rutaRaiz, "ESFE.SYSCURAVITA.UI", "Views", "logo.png");
                if (!File.Exists(rutaLogo))
                {
                    rutaLogo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", "logo.png");
                }
                if (File.Exists(rutaLogo))
                {
                    byte[] bytes = File.ReadAllBytes(rutaLogo);
                    _logoBase64Cache = Convert.ToBase64String(bytes);
                    return _logoBase64Cache;
                }
            }
            catch { }
            return string.Empty;
        }

        private string GenerarHtmlReceta(
            string numeroReceta,
            string nombrePaciente,
            string codigoExpediente,
            string motivoEspecialidad,
            string diagnostico,
            string pa,
            string fc,
            string temp,
            string peso,
            List<(string Medicamento, string Dosis)> medicamentos)
        {
            var sbMeds = new System.Text.StringBuilder();
            if (medicamentos != null && medicamentos.Count > 0)
            {
                int count = 1;
                foreach (var (med, dosis) in medicamentos)
                {
                    sbMeds.Append($@"
                        <tr>
                            <td style='text-align:center; font-weight:700; color:#1c909b;'>{count++}</td>
                            <td style='font-weight:700; color:#277c95;'>{System.Net.WebUtility.HtmlEncode(med)}</td>
                            <td style='color:#475569;'>{System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(dosis) ? "Según indicación médica" : dosis)}</td>
                        </tr>");
                }
            }
            else
            {
                sbMeds.Append(@"
                    <tr>
                        <td colspan='3' style='text-align:center; color:#7f8c8d; padding:16px; font-style:italic;'>
                            No se prescribieron medicamentos específicos para esta consulta.
                        </td>
                    </tr>");
            }

            string fechaActual = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");
            string logoBase64 = ObtenerLogoBase64();
            string logoHtml = !string.IsNullOrEmpty(logoBase64)
                ? $"<img src='data:image/png;base64,{logoBase64}' alt='Curavita' class='logo-img'>"
                : @"<div style='width: 40px; height: 40px; background: #277c95; border-radius: 8px; display: flex; align-items: center; justify-content: center;'>
                    <svg width='22' height='22' viewBox='0 0 24 24' fill='none' stroke='#ffffff' stroke-width='2.5' stroke-linecap='round' stroke-linejoin='round'>
                        <path d='M22 12h-4l-3 9L9 3l-3 9H2'></path>
                    </svg>
                   </div>";

            return $@"<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <title>Receta Médica - {System.Net.WebUtility.HtmlEncode(numeroReceta)}</title>
    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap' rel='stylesheet'>
    <style>
        @page {{ size: A4; margin: 12mm; }}
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            color: #2c3e50;
            background-color: #ffffff;
            padding: 10px;
            font-size: 13px;
            line-height: 1.5;
        }}
        .receta-container {{
            max-width: 780px;
            margin: 0 auto;
            border: 1px solid #d0dcde;
            border-radius: 10px;
            padding: 26px 30px;
            background: #ffffff;
        }}
        .header-section {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding-bottom: 14px;
        }}
        .brand-info {{
            display: flex;
            align-items: center;
            gap: 16px;
        }}
        .logo-img {{
            max-height: 48px;
            width: auto;
            object-fit: contain;
        }}
        .brand-title {{
            font-size: 21px;
            font-weight: 800;
            color: #277c95;
            letter-spacing: -0.5px;
            line-height: 1.1;
        }}
        .brand-sub {{
            font-size: 11px;
            color: #1c909b;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-top: 3px;
        }}
        .brand-contact {{
            font-size: 10.5px;
            color: #7f8c8d;
            margin-top: 2px;
        }}
        .doc-badge {{
            border: 1.5px solid #1c909b;
            border-radius: 8px;
            padding: 10px 18px;
            text-align: center;
            background: #f2f9fa;
            min-width: 170px;
        }}
        .doc-badge-title {{
            font-size: 11px;
            font-weight: 800;
            color: #277c95;
            text-transform: uppercase;
            letter-spacing: 0.8px;
        }}
        .doc-badge-num {{
            font-size: 16px;
            font-weight: 800;
            color: #1c909b;
            margin: 3px 0;
        }}
        .doc-badge-date {{
            font-size: 10px;
            color: #5a7b85;
            font-weight: 600;
        }}
        .brand-divider {{
            height: 3px;
            background: linear-gradient(90deg, #277c95 0%, #1c909b 50%, #b2cfd6 100%);
            border-radius: 2px;
            margin-bottom: 20px;
        }}
        .patient-card {{
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 14px 18px;
            margin-bottom: 18px;
        }}
        .patient-grid {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px 20px;
        }}
        .field-label {{
            font-size: 11px;
            font-weight: 700;
            color: #5a7b85;
            text-transform: uppercase;
            letter-spacing: 0.4px;
        }}
        .field-value {{
            font-size: 13.5px;
            font-weight: 600;
            color: #2c3e50;
        }}
        .vitals-bar {{
            background: #eef5f7;
            border-left: 3.5px solid #1c909b;
            border-radius: 0 6px 6px 0;
            padding: 8px 14px;
            margin-bottom: 18px;
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
            font-size: 12.5px;
            color: #277c95;
        }}
        .vitals-bar b {{
            color: #1c909b;
        }}
        .section-heading {{
            font-size: 12px;
            font-weight: 800;
            color: #277c95;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
        }}
        .diag-box {{
            background: #ffffff;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            padding: 12px 16px;
            font-size: 13px;
            line-height: 1.5;
            color: #2c3e50;
            margin-bottom: 22px;
        }}
        .med-table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 28px;
            border-radius: 6px;
            overflow: hidden;
            border: 1px solid #e2e8f0;
        }}
        .med-table th {{
            background: #277c95;
            color: #ffffff;
            font-weight: 700;
            text-transform: uppercase;
            font-size: 11px;
            letter-spacing: 0.5px;
            padding: 10px 14px;
            text-align: left;
        }}
        .med-table td {{
            padding: 11px 14px;
            border-bottom: 1px solid #e2e8f0;
            font-size: 13px;
        }}
        .med-table tr:nth-child(even) td {{
            background-color: #f8fafc;
        }}
        .footer-sign-area {{
            display: flex;
            justify-content: space-between;
            align-items: flex-end;
            margin-top: 30px;
            padding-top: 15px;
        }}
        .indicaciones-extra {{
            font-size: 11px;
            color: #7f8c8d;
            max-width: 400px;
        }}
        .sign-box {{
            width: 230px;
            text-align: center;
        }}
        .sign-line {{
            border-top: 1.5px dashed #7f8c8d;
            margin-bottom: 6px;
        }}
        .sign-doc {{
            font-size: 12.5px;
            font-weight: 700;
            color: #277c95;
        }}
        .sign-sub {{
            font-size: 10.5px;
            color: #7f8c8d;
        }}
        .doc-footer {{
            border-top: 1px solid #e2e8f0;
            margin-top: 24px;
            padding-top: 10px;
            display: flex;
            justify-content: space-between;
            font-size: 10.5px;
            color: #7f8c8d;
        }}
    </style>
</head>
<body>
    <div class='receta-container'>
        <div class='header-section'>
            <div class='brand-info'>
                {logoHtml}
                <div>
                    <div class='brand-title'>CLÍNICA CURAVITA</div>
                    <div class='brand-sub'>Atención Médica Integral y Especializada</div>
                    <div class='brand-contact'>ESFE SYSCURAVITA &bull; PBX: (503) 2200-0000 &bull; San Salvador, El Salvador</div>
                </div>
            </div>
            <div class='doc-badge'>
                <div class='doc-badge-title'>RECETA MÉDICA</div>
                <div class='doc-badge-num'>{System.Net.WebUtility.HtmlEncode(numeroReceta)}</div>
                <div class='doc-badge-date'>Fecha: {fechaActual}</div>
            </div>
        </div>

        <div class='brand-divider'></div>

        <div class='patient-card'>
            <div class='patient-grid'>
                <div>
                    <div class='field-label'>Paciente</div>
                    <div class='field-value'>{System.Net.WebUtility.HtmlEncode(nombrePaciente)}</div>
                </div>
                <div>
                    <div class='field-label'>N° de Expediente</div>
                    <div class='field-value'>{System.Net.WebUtility.HtmlEncode(codigoExpediente)}</div>
                </div>
                <div>
                    <div class='field-label'>Especialidad / Motivo</div>
                    <div class='field-value'>{System.Net.WebUtility.HtmlEncode(motivoEspecialidad)}</div>
                </div>
                <div>
                    <div class='field-label'>Modalidad de Atención</div>
                    <div class='field-value'>Consulta Externa</div>
                </div>
            </div>
        </div>

        <div class='vitals-bar'>
            <span><b>P.A:</b> {System.Net.WebUtility.HtmlEncode(pa)}</span>
            <span>&bull;</span>
            <span><b>F.C:</b> {System.Net.WebUtility.HtmlEncode(fc)} lpm</span>
            <span>&bull;</span>
            <span><b>Temp:</b> {System.Net.WebUtility.HtmlEncode(temp)} °C</span>
            <span>&bull;</span>
            <span><b>Peso:</b> {System.Net.WebUtility.HtmlEncode(peso)} kg</span>
        </div>

        <div class='section-heading'>Diagnóstico Clínico</div>
        <div class='diag-box'>
            {System.Net.WebUtility.HtmlEncode(diagnostico)}
        </div>

        <div class='section-heading'>Prescripción Farmacológica</div>
        <table class='med-table'>
            <thead>
                <tr>
                    <th style='width: 45px; text-align:center;'>#</th>
                    <th style='width: 42%;'>Medicamento y Presentación</th>
                    <th>Posología / Indicaciones de Uso</th>
                </tr>
            </thead>
            <tbody>
                {sbMeds}
            </tbody>
        </table>

        <div class='footer-sign-area'>
            <div class='indicaciones-extra'>
                * Siga estrictamente la dosis y horarios prescritos.<br>
                * No suspenda el tratamiento sin previa indicación médica.<br>
                * En caso de reacciones adversas consulte a emergencias.
            </div>
            <div class='sign-box'>
                <div style='height: 40px;'></div>
                <div class='sign-line'></div>
                <div class='sign-doc'>Dr(a). Médico Tratante</div>
                <div class='sign-sub'>Firma y Sello Profesional</div>
            </div>
        </div>

        <div class='doc-footer'>
            <span>ESFE SYSCURAVITA - Sistema Integral de Gestión Hospitalaria</span>
            <span>Documento Médico Oficial &bull; Válido por 30 días</span>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerarHtmlFactura(
            string numeroFactura,
            string nombrePaciente,
            string codigoExpediente,
            string especialidadConcepto,
            string metodoPago,
            decimal montoTotal,
            decimal montoRecibido,
            decimal cambio)
        {
            decimal subtotal = Math.Round(montoTotal / 1.13m, 2);
            decimal iva = Math.Round(montoTotal - subtotal, 2);
            string fechaActual = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");
            string logoBase64 = ObtenerLogoBase64();
            string logoHtml = !string.IsNullOrEmpty(logoBase64)
                ? $"<img src='data:image/png;base64,{logoBase64}' alt='Curavita' class='logo-img'>"
                : @"<div style='width: 40px; height: 40px; background: #277c95; border-radius: 8px; display: flex; align-items: center; justify-content: center;'>
                    <svg width='22' height='22' viewBox='0 0 24 24' fill='none' stroke='#ffffff' stroke-width='2.5' stroke-linecap='round' stroke-linejoin='round'>
                        <path d='M22 12h-4l-3 9L9 3l-3 9H2'></path>
                    </svg>
                   </div>";

            return $@"<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <title>Factura - {System.Net.WebUtility.HtmlEncode(numeroFactura)}</title>
    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap' rel='stylesheet'>
    <style>
        @page {{ size: A4; margin: 12mm; }}
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            color: #2c3e50;
            background-color: #ffffff;
            padding: 10px;
            font-size: 13px;
            line-height: 1.5;
        }}
        .factura-container {{
            max-width: 780px;
            margin: 0 auto;
            border: 1px solid #d0dcde;
            border-radius: 10px;
            padding: 26px 30px;
            background: #ffffff;
        }}
        .header-section {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding-bottom: 14px;
        }}
        .brand-info {{
            display: flex;
            align-items: center;
            gap: 16px;
        }}
        .logo-img {{
            max-height: 48px;
            width: auto;
            object-fit: contain;
        }}
        .brand-title {{
            font-size: 21px;
            font-weight: 800;
            color: #277c95;
            letter-spacing: -0.5px;
            line-height: 1.1;
        }}
        .brand-sub {{
            font-size: 11px;
            color: #1c909b;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-top: 3px;
        }}
        .brand-contact {{
            font-size: 10.5px;
            color: #7f8c8d;
            margin-top: 2px;
        }}
        .doc-badge {{
            border: 1.5px solid #1c909b;
            border-radius: 8px;
            padding: 10px 18px;
            text-align: center;
            background: #f2f9fa;
            min-width: 170px;
        }}
        .doc-badge-title {{
            font-size: 11px;
            font-weight: 800;
            color: #277c95;
            text-transform: uppercase;
            letter-spacing: 0.8px;
        }}
        .doc-badge-num {{
            font-size: 16px;
            font-weight: 800;
            color: #1c909b;
            margin: 3px 0;
        }}
        .doc-badge-date {{
            font-size: 10px;
            color: #5a7b85;
            font-weight: 600;
        }}
        .brand-divider {{
            height: 3px;
            background: linear-gradient(90deg, #277c95 0%, #1c909b 50%, #b2cfd6 100%);
            border-radius: 2px;
            margin-bottom: 20px;
        }}
        .patient-card {{
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 14px 18px;
            margin-bottom: 20px;
        }}
        .patient-grid {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px 20px;
        }}
        .field-label {{
            font-size: 11px;
            font-weight: 700;
            color: #5a7b85;
            text-transform: uppercase;
            letter-spacing: 0.4px;
        }}
        .field-value {{
            font-size: 13.5px;
            font-weight: 600;
            color: #2c3e50;
        }}
        .section-heading {{
            font-size: 12px;
            font-weight: 800;
            color: #277c95;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
        }}
        .service-table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 22px;
            border-radius: 6px;
            overflow: hidden;
            border: 1px solid #e2e8f0;
        }}
        .service-table th {{
            background: #277c95;
            color: #ffffff;
            font-weight: 700;
            text-transform: uppercase;
            font-size: 11px;
            letter-spacing: 0.5px;
            padding: 10px 14px;
            text-align: left;
        }}
        .service-table td {{
            padding: 12px 14px;
            border-bottom: 1px solid #e2e8f0;
            font-size: 13px;
        }}
        .service-table tr:nth-child(even) td {{
            background-color: #f8fafc;
        }}
        .totals-summary {{
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            margin-bottom: 20px;
        }}
        .payment-status-badge {{
            background: #eef5f7;
            border-left: 3.5px solid #1c909b;
            border-radius: 0 6px 6px 0;
            padding: 12px 16px;
            max-width: 380px;
            font-size: 12.5px;
            color: #277c95;
        }}
        .payment-status-badge b {{
            color: #1c909b;
        }}
        .totals-box {{
            width: 280px;
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 14px 18px;
        }}
        .total-row {{
            display: flex;
            justify-content: space-between;
            font-size: 12.5px;
            color: #5a7b85;
            margin-bottom: 6px;
        }}
        .total-row.highlight {{
            font-size: 15px;
            font-weight: 800;
            color: #1c909b;
            border-top: 1.5px solid #d0dcde;
            border-bottom: 1.5px solid #d0dcde;
            padding: 8px 0;
            margin: 8px 0;
        }}
        .doc-footer {{
            border-top: 1px solid #e2e8f0;
            margin-top: 24px;
            padding-top: 10px;
            display: flex;
            justify-content: space-between;
            font-size: 10.5px;
            color: #7f8c8d;
        }}
    </style>
</head>
<body>
    <div class='factura-container'>
        <div class='header-section'>
            <div class='brand-info'>
                {logoHtml}
                <div>
                    <div class='brand-title'>CLÍNICA CURAVITA</div>
                    <div class='brand-sub'>Comprobante de Pago y Facturación</div>
                    <div class='brand-contact'>ESFE SYSCURAVITA &bull; PBX: (503) 2200-0000 &bull; San Salvador, El Salvador</div>
                </div>
            </div>
            <div class='doc-badge'>
                <div class='doc-badge-title'>FACTURA ELECTRÓNICA</div>
                <div class='doc-badge-num'>{System.Net.WebUtility.HtmlEncode(numeroFactura)}</div>
                <div class='doc-badge-date'>Fecha: {fechaActual}</div>
            </div>
        </div>

        <div class='brand-divider'></div>

        <div class='patient-card'>
            <div class='patient-grid'>
                <div>
                    <div class='field-label'>Paciente / Cliente</div>
                    <div class='field-value'>{System.Net.WebUtility.HtmlEncode(nombrePaciente)}</div>
                </div>
                <div>
                    <div class='field-label'>N° de Expediente</div>
                    <div class='field-value'>{System.Net.WebUtility.HtmlEncode(codigoExpediente)}</div>
                </div>
                <div>
                    <div class='field-label'>Método de Pago</div>
                    <div class='field-value'>{System.Net.WebUtility.HtmlEncode(metodoPago)}</div>
                </div>
                <div>
                    <div class='field-label'>Estado del Comprobante</div>
                    <div class='field-value' style='color:#1c909b; font-weight:700;'>CANCELADO / PAGADO</div>
                </div>
            </div>
        </div>

        <div class='section-heading'>Detalle del Servicio Facturado</div>
        <table class='service-table'>
            <thead>
                <tr>
                    <th style='width: 50px; text-align:center;'>Cant.</th>
                    <th>Concepto / Especialidad del Servicio</th>
                    <th style='width: 110px; text-align:right;'>Precio Unit.</th>
                    <th style='width: 110px; text-align:right;'>Total</th>
                </tr>
            </thead>
            <tbody>
                <tr>
                    <td style='text-align:center; font-weight:700; color:#1c909b;'>1</td>
                    <td>
                        <b style='color:#277c95;'>Consulta Médica Especializada</b><br>
                        <span style='color:#7f8c8d; font-size:12px;'>Especialidad: {System.Net.WebUtility.HtmlEncode(especialidadConcepto)}</span>
                    </td>
                    <td style='text-align:right; color:#5a7b85;'>${subtotal:F2}</td>
                    <td style='text-align:right; font-weight:700; color:#277c95;'>${subtotal:F2}</td>
                </tr>
            </tbody>
        </table>

        <div class='totals-summary'>
            <div class='payment-status-badge'>
                <b>Constancia de Pago:</b> Se acredita la cancelación total del servicio mediante <b>{System.Net.WebUtility.HtmlEncode(metodoPago)}</b>. Gracias por confiar en los servicios médicos de Clínica Curavita.
            </div>
            <div class='totals-box'>
                <div class='total-row'>
                    <span>Subtotal:</span>
                    <span>${subtotal:F2}</span>
                </div>
                <div class='total-row'>
                    <span>IVA (13%):</span>
                    <span>${iva:F2}</span>
                </div>
                <div class='total-row highlight'>
                    <span>TOTAL:</span>
                    <span>${montoTotal:F2}</span>
                </div>
                <div class='total-row'>
                    <span>Monto Recibido:</span>
                    <span>${montoRecibido:F2}</span>
                </div>
                <div class='total-row'>
                    <span>Cambio / Vuelto:</span>
                    <span>${cambio:F2}</span>
                </div>
            </div>
        </div>

        <div class='doc-footer'>
            <span>ESFE SYSCURAVITA - Módulo de Facturación y Caja</span>
            <span>Gracias por su preferencia &bull; Comprobante Fiscal Oficial</span>
        </div>
    </div>
</body>
</html>";
        }
        #endregion

        private void webView21_Click(object? sender, EventArgs e) { }
    }
}