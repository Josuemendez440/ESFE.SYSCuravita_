using System.Collections.Generic;
using ESFE.SYSCURAVITA.DAL;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.LN
{
    public static class PacienteLN
    {
        public static bool Guardar(PacienteEN pPaciente)
        {
            return PacienteDAL.Guardar(pPaciente);
        }

        public static List<PacienteEN> ObtenerTodos()
        {
            return PacienteDAL.ObtenerTodos();
        }

        public static bool Eliminar(int pacienteId)
        {
            return PacienteDAL.Eliminar(pacienteId);
        }
    }
}