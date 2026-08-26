using System;

namespace ESFE.SYSCURAVITA.EN
{
    public class PacienteEN
    {
        public int paciente_id { get; set; }
        public string codigo_expediente { get; set; } = string.Empty;
        public string nombres { get; set; } = string.Empty;
        public string apellidos { get; set; } = string.Empty;
        public string dui_documento { get; set; } = string.Empty;
        public string telefono { get; set; } = string.Empty;
        public DateTime? fecha_nacimiento { get; set; }
    }
}