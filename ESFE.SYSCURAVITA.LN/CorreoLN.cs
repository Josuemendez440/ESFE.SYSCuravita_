using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.LN
{
    public class CorreoLN
    {
        // Configuración de Servidor SMTP (se puede ajustar según el proveedor de correo)
        private static string SmtpHost = "smtp.gmail.com";
        private static int SmtpPort = 587;
        private static bool SmtpEnableSsl = true;
        private static string SmtpUsuario = "slmdz2007s@gmail.com";
        private static string SmtpPassword = "vkbhqurqscsmlttm"; // Contraseña de Aplicación de Google

        /// <summary>
        /// Valida el formato de una dirección de correo electrónico.
        /// </summary>
        public static bool EsCorreoValido(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var addr = new MailAddress(email.Trim());
                return addr.Address == email.Trim();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Envía el comprobante de factura por correo electrónico al paciente con el PDF adjunto.
        /// </summary>
        public static RespuestaEnvioCorreoDTO EnviarFactura(CorreoDTO dto)
        {
            if (dto == null)
            {
                return new RespuestaEnvioCorreoDTO
                {
                    Exito = false,
                    Mensaje = "Los datos del correo no pueden estar vacíos."
                };
            }

            if (!EsCorreoValido(dto.Destinatario))
            {
                return new RespuestaEnvioCorreoDTO
                {
                    Exito = false,
                    Mensaje = "La dirección de correo electrónico ingresada no es válida."
                };
            }

            try
            {
                string asunto = string.IsNullOrWhiteSpace(dto.Asunto)
                    ? $"Factura Electrónica {dto.NumeroFactura} - Clínica Curavita"
                    : dto.Asunto;

                string cuerpoHtml = GenerarPlantillaHtmlCorreo(dto);

                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(
                        string.IsNullOrWhiteSpace(SmtpUsuario) ? "facturacion@curavita.com" : SmtpUsuario,
                        "Clínica Curavita - Facturación"
                    );
                    mail.To.Add(dto.Destinatario.Trim());
                    mail.Subject = asunto;
                    mail.Body = cuerpoHtml;
                    mail.IsBodyHtml = true;

                    // Adjuntar archivo PDF si existe
                    if (!string.IsNullOrWhiteSpace(dto.RutaArchivoPdf) && File.Exists(dto.RutaArchivoPdf))
                    {
                        Attachment adjunto = new Attachment(dto.RutaArchivoPdf);
                        mail.Attachments.Add(adjunto);
                    }

                    // Envío por SMTP
                    using (SmtpClient smtp = new SmtpClient(SmtpHost, SmtpPort))
                    {
                        smtp.EnableSsl = SmtpEnableSsl;
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtp.UseDefaultCredentials = false;

                        if (!string.IsNullOrWhiteSpace(SmtpUsuario) && !string.IsNullOrWhiteSpace(SmtpPassword))
                        {
                            smtp.Credentials = new NetworkCredential(SmtpUsuario, SmtpPassword);
                        }

                        // Timeout de 10 segundos
                        smtp.Timeout = 10000;
                        smtp.Send(mail);
                    }
                }

                return new RespuestaEnvioCorreoDTO
                {
                    Exito = true,
                    Mensaje = $"Factura enviada exitosamente a {dto.Destinatario}."
                };
            }
            catch (SmtpException smtpEx)
            {
                System.Diagnostics.Debug.WriteLine("Error SMTP: " + smtpEx.Message);
                return new RespuestaEnvioCorreoDTO
                {
                    Exito = false,
                    Mensaje = $"No se pudo conectar con el servidor de correo: {smtpEx.Message}. Verifique su conexión o configuración."
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al enviar correo: " + ex.Message);
                return new RespuestaEnvioCorreoDTO
                {
                    Exito = false,
                    Mensaje = $"Error al enviar el correo: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Genera la plantilla HTML corporativa para el correo electrónico.
        /// </summary>
        private static string GenerarPlantillaHtmlCorreo(CorreoDTO dto)
        {
            string nombrePaciente = string.IsNullOrWhiteSpace(dto.PacienteNombre) ? "Estimado(a) Paciente" : dto.PacienteNombre;
            string numFactura = string.IsNullOrWhiteSpace(dto.NumeroFactura) ? "N/A" : dto.NumeroFactura;
            string monto = dto.MontoTotal > 0 ? $"${dto.MontoTotal:F2}" : "$0.00";
            string fecha = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");

            return $@"
            <!DOCTYPE html>
            <html lang='es'>
            <head>
                <meta charset='UTF-8'>
                <style>
                    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f8f9; margin: 0; padding: 20px; color: #2c3e50; }}
                    .email-card {{ max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.06); border: 1px solid #d9e6e8; }}
                    .email-header {{ background: linear-gradient(135deg, #277c95 0%, #1c909b 100%); padding: 30px 25px; text-align: center; color: #ffffff; }}
                    .email-header h1 {{ margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -0.5px; }}
                    .email-header p {{ margin: 6px 0 0 0; font-size: 13px; opacity: 0.9; text-transform: uppercase; letter-spacing: 0.5px; }}
                    .email-body {{ padding: 30px 25px; }}
                    .greeting {{ font-size: 16px; font-weight: 700; color: #277c95; margin-bottom: 12px; }}
                    .text-p {{ font-size: 14px; line-height: 1.6; color: #475569; margin-bottom: 20px; }}
                    .invoice-badge-box {{ background: #eef5f7; border: 1.5px solid #1c909b; border-radius: 8px; padding: 16px 20px; margin-bottom: 24px; }}
                    .invoice-row {{ display: flex; justify-content: space-between; margin-bottom: 8px; font-size: 13.5px; }}
                    .invoice-row:last-child {{ margin-bottom: 0; padding-top: 8px; border-top: 1px dashed #b2cfd6; font-weight: 700; font-size: 15px; color: #1c909b; }}
                    .email-footer {{ background: #fafcfc; border-top: 1px solid #e2e8f0; padding: 20px 25px; text-align: center; font-size: 11.5px; color: #7f8c8d; }}
                </style>
            </head>
            <body>
                <div class='email-card'>
                    <div class='email-header'>
                        <h1>CLÍNICA CURAVITA</h1>
                        <p>Comprobante de Facturación Electrónica</p>
                    </div>
                    <div class='email-body'>
                        <div class='greeting'>Hola, {System.Net.WebUtility.HtmlEncode(nombrePaciente)}</div>
                        <p class='text-p'>
                            Le agradecemos su visita a <b>Clínica Curavita</b>. Adjunto a este correo encontrará el comprobante oficial en formato PDF correspondiente a su atención médica.
                        </p>
                        <div class='invoice-badge-box'>
                            <div style='margin-bottom: 6px; font-size: 13px; color: #5a7b85;'><b>N° de Factura:</b> {System.Net.WebUtility.HtmlEncode(numFactura)}</div>
                            <div style='margin-bottom: 6px; font-size: 13px; color: #5a7b85;'><b>Fecha y Hora:</b> {fecha}</div>
                            <div style='font-size: 15px; color: #1c909b;'><b>Total Facturado:</b> {monto}</div>
                        </div>
                        <p class='text-p' style='font-size: 12.5px; color: #64748b;'>
                            <i>Nota: Este es un correo automático generado por el sistema ESFE SYSCURAVITA. Si tiene alguna duda o consulta sobre su factura, puede comunicarse a nuestra central telefónica PBX: (503) 2200-0000.</i>
                        </p>
                    </div>
                    <div class='email-footer'>
                        <p style='margin: 0;'><b>Clínica Curavita</b> &bull; San Salvador, El Salvador</p>
                        <p style='margin: 4px 0 0 0;'>Atención Médica Integral y Especializada</p>
                    </div>
                </div>
            </body>
            </html>";
        }
    }
}
