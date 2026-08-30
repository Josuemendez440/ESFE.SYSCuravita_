using System;

namespace ESFE.SYSCURAVITA.EN
{
    public class CorreoDTO
    {
        public string Destinatario { get; set; } = string.Empty;
        public string Asunto { get; set; } = string.Empty;
        public string MensajeTexto { get; set; } = string.Empty;
        public string NumeroFactura { get; set; } = string.Empty;
        public string PacienteNombre { get; set; } = string.Empty;
        public decimal MontoTotal { get; set; }
        public string RutaArchivoPdf { get; set; } = string.Empty;
    }

    public class RespuestaEnvioCorreoDTO
    {
        public string Accion { get; set; } = "CORREO_ENVIADO";
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
