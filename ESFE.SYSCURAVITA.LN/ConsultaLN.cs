using System.Collections.Generic;
using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA_DAL;

namespace ESFE.SYSCURAVITA.LN
{
    public static class ConsultaLN
    {
        // Sobrecarga original (3 parámetros) para mantener compatibilidad
        public static bool GuardarDiagnostico(int pacienteId, string? codigoExpediente, string? diagnosticoTexto)
        {
            return ConsultaDAL.GuardarDiagnostico(pacienteId, codigoExpediente, diagnosticoTexto);
        }

        // Nueva sobrecarga (8 parámetros) recibida desde FormSistema
        public static bool GuardarDiagnostico(
            int pacienteId,
            string? codigoExpediente,
            string? diagnosticoTexto,
            string? pa,
            string? fc,
            string? temperatura,
            string? peso,
            string? receta)
        {
            // Formatea los signos vitales y la receta junto con el diagnóstico
            string signosYReceta = $"PA: {pa ?? "N/A"} | FC: {fc ?? "N/A"} | Temp: {temperatura ?? "N/A"} | Peso: {peso ?? "N/A"}\nReceta: {receta ?? "Sin medicamentos"}";
            string diagnosticoCompleto = string.IsNullOrWhiteSpace(diagnosticoTexto)
                ? signosYReceta
                : $"{diagnosticoTexto}\n{signosYReceta}";

            return ConsultaDAL.GuardarDiagnostico(pacienteId, codigoExpediente, diagnosticoCompleto);
        }

        public static List<HistorialDTO> ObtenerHistorial(int pacienteId, string? codigoExpediente)
        {
            return ConsultaDAL.ObtenerHistorial(pacienteId, codigoExpediente);
        }
    }
}