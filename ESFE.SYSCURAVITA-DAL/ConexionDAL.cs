using Microsoft.Data.SqlClient;

namespace ESFE.SYSCURAVITA.DAL
{
    public class ConexionDAL
    {
        private static readonly string conexionStr = @"Server=.\SQLEXPRESS;Database=CURAVITA_DB;Trusted_Connection=True;TrustServerCertificate=True;";

        public static SqlConnection ObtenerConexion()
        {
            return new SqlConnection(conexionStr);
        }
    }
}