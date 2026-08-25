using System.Collections.Generic;
using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA_DAL;

namespace ESFE.SYSCURAVITA.LN
{
    public static class ConsultaLN
    {
        public static bool GuardarDiagnostico(int pacienteId, string? codigoExpediente, string? diagnosticoTexto)
        {
            return ConsultaDAL.GuardarDiagnostico(pacienteId, codigoExpediente, diagnosticoTexto);
        }

        public static List<HistorialDTO> ObtenerHistorial(int pacienteId, string? codigoExpediente)
        {
            return ConsultaDAL.ObtenerHistorial(pacienteId, codigoExpediente);
        }
    }
}