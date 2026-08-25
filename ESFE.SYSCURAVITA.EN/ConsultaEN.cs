using System;

namespace ESFE.SYSCURAVITA.EN
{
    public class ConsultaEN
    {
        public int ConsultaId { get; set; }
        public int PacienteId { get; set; }
        public int EstadoConsultaId { get; set; } // Propiedad requerida agregada
        public string? Diagnostico { get; set; }
        public DateTime FechaConsulta { get; set; }
    }
}