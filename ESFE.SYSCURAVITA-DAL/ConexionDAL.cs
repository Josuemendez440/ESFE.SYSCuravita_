using Microsoft.Data.SqlClient;

namespace ESFE.SYSCURAVITA_DAL
{
    public static class ConexionDAL
    {
        private const string Cadena = "Data Source=sql5106.site4now.net;Initial Catalog=db_acdbf3_clinicadb;User Id=db_acdbf3_clinicadb_admin;Password=K9#mP2$xL8!v;Encrypt=True;TrustServerCertificate=True;";

        public static SqlConnection ObtenerConexion() => new(Cadena);
    }
}