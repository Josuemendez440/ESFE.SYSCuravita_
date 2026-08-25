using Microsoft.Data.SqlClient;

namespace ESFE.SYSCURAVITA_DAL
{
    public static class ConexionDAL
    {
        private static readonly string conexionStr = "Server=.\\SQLEXPRESS;Database=CURAVITA_DB;Trusted_Connection=True;TrustServerCertificate=True;";

        public static SqlConnection ObtenerConexion()
        {
            return new SqlConnection(conexionStr);
        }
    }
}