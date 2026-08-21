using System.Collections.Generic;
using ESFE.SYSCURAVITA.DAL;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.LN
{
    public class PacienteLN
    {
        private readonly PacienteDAL _pacienteDAL = new PacienteDAL();

        public List<PacienteEN> ObtenerTodos()
        {
            return _pacienteDAL.ObtenerTodos();
        }

        public bool Guardar(PacienteEN pPaciente)
        {
            if (pPaciente == null || string.IsNullOrWhiteSpace(pPaciente.nombres) || string.IsNullOrWhiteSpace(pPaciente.dui_documento))
            {
                return false;
            }
            return _pacienteDAL.Guardar(pPaciente);
        }
    }
}