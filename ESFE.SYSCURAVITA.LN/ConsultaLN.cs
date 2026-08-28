using System.Collections.Generic;
using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA_DAL;

namespace ESFE.SYSCURAVITA.LN
{
    public static class ConsultaLN
    {
        // Sobrecarga de compatibilidad (3 parámetros)
        public static bool GuardarDiagnostico(int pacienteId, string? codigoExpediente, string? diagnosticoTexto)
        {
            return ConsultaDAL.GuardarDiagnostico(pacienteId, codigoExpediente, diagnosticoTexto);
        }

        // Sobrecarga completa (8 parámetros): reenvía directamente los signos vitales a ConsultaDAL
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
            return ConsultaDAL.GuardarDiagnostico(
                pacienteId,
                codigoExpediente,
                diagnosticoTexto,
                pa,
                fc,
                temperatura,
                peso,
                receta
            );
        }

        public static List<HistorialDTO> ObtenerHistorial(int pacienteId, string? codigoExpediente)
        {
            return ConsultaDAL.ObtenerHistorial(pacienteId, codigoExpediente);
        }
    }
}