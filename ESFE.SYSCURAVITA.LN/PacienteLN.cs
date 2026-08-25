using System.Collections.Generic;
using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA_DAL;

namespace ESFE.SYSCURAVITA.LN
{
    public static class PacienteLN
    {
        public static List<PacienteEN> ObtenerTodos()
        {
            return PacienteDAL.ObtenerTodos();
        }

        public static bool Guardar(PacienteEN? pPaciente)
        {
            if (pPaciente == null ||
                string.IsNullOrWhiteSpace(pPaciente.nombres) ||
                string.IsNullOrWhiteSpace(pPaciente.dui_documento))
            {
                return false;
            }
            return PacienteDAL.Guardar(pPaciente);
        }
    }
}