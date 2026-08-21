using System;

namespace ESFE.SYSCURAVITA.EN
{
    public class PacienteEN
    {
        public int paciente_id { get; set; }
        public string codigo_expediente { get; set; }
        public string nombres { get; set; }
        public string apellidos { get; set; }
        public string dui_documento { get; set; }
        public string telefono { get; set; }
        public string direccion { get; set; }
        public DateTime? fecha_nacimiento { get; set; }
        public string genero { get; set; }
        public DateTime fecha_creacion { get; set; }
        public DateTime? fecha_modificacion { get; set; }
    }
}