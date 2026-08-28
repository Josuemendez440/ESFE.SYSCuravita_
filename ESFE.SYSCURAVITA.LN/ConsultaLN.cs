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
            return ConsultaDAL.GuardarDiagnostico(pacienteId, codigoExpediente, diagnosticoTexto, montoConsulta: 0.00m);
        }

        // Sobrecarga completa incluyendo montoConsulta, medicamentos y motivoConsulta
        public static bool GuardarDiagnostico(
            int pacienteId,
            string? codigoExpediente,
            string? diagnosticoTexto,
            string? pa,
            string? fc,
            string? temperatura,
            string? peso,
            decimal montoConsulta,
            List<(string Medicamento, string Dosis)>? medicamentos,
            string? motivoConsulta = "Consulta Médica General")
        {
            return ConsultaDAL.GuardarDiagnostico(
                pacienteId,
                codigoExpediente,
                diagnosticoTexto,
                pa,
                fc,
                temperatura,
                peso,
                montoConsulta,
                medicamentos,
                motivoConsulta: motivoConsulta
            );
        }

        public static List<HistorialDTO> ObtenerHistorial(int pacienteId, string? codigoExpediente)
        {
            return ConsultaDAL.ObtenerHistorial(pacienteId, codigoExpediente);
        }
    }
}