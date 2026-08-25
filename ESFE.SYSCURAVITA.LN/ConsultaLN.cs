using System.Collections.Generic;
using ESFE.SYSCURAVITA.DAL;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.LN
{
    public class ConsultaLN
    {
        public int GuardarConsulta(ConsultaEN pConsulta)
        {
            if (pConsulta == null || pConsulta.PacienteId <= 0)
            {
                return 0;
            }
            return ConsultaDAL.GuardarConsulta(pConsulta);
        }

        public List<ConsultaEN> ObtenerHistorial(int pacienteId)
        {
            if (pacienteId <= 0)
            {
                return new List<ConsultaEN>();
            }
            return ConsultaDAL.ObtenerHistorial(pacienteId);
        }
    }
}