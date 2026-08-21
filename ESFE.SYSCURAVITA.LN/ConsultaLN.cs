using ESFE.SYSCURAVITA.DAL;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.LN
{
    public class ConsultaLN
    {
        public int GuardarConsulta(ConsultaEN pConsulta) => ConsultaDAL.GuardarConsulta(pConsulta);
        public List<ConsultaEN> ObtenerHistorial(int pacienteId) => ConsultaDAL.ObtenerHistorial(pacienteId);
    }
}