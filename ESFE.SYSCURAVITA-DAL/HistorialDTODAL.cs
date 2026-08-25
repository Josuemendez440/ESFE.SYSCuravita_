using System.Text.Json.Serialization;

namespace ESFE.SYSCURAVITA_DAL
{
    public class HistorialDTO
    {
        [JsonPropertyName("fecha")]
        public string Fecha { get; set; } = string.Empty;

        [JsonPropertyName("hora")]
        public string Hora { get; set; } = string.Empty;

        [JsonPropertyName("diagnostico")]
        public string Diagnostico { get; set; } = string.Empty;

        [JsonPropertyName("observaciones")]
        public string Observaciones { get; set; } = string.Empty;
    }
}