using ESFE.SYSCURAVITA.EN;
using System.Collections.Generic;

namespace ESFE.SYSCURAVITA.DAL
{
    public class ConsultaDAL
    {
        public static int GuardarConsulta(ConsultaEN pConsulta)
        {
            // Lógica de inserción SQL aquí...
            return 1; // Devuelve las filas afectadas o ID
        }

        public static List<ConsultaEN> ObtenerHistorial(int pacienteId)
        {
            List<ConsultaEN> lista = new List<ConsultaEN>();
            // Lógica de consulta SQL SELECT aquí...
            return lista; // Devuelve la lista de consultas
        }
    }
}