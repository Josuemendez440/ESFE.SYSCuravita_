namespace ESFE.SYSCURAVITA.EN
{
    public class ConsultaEN
    {
        public int Id { get; set; }
        public int PacienteId { get; set; }
        public string Diagnostico { get; set; } = string.Empty;
        public string Fecha { get; set; } = string.Empty;
        public string Hora { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
    }
}