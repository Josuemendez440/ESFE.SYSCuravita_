using System;
using Microsoft.Data.SqlClient;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.DAL
{
    public class ValidarCredencialesDAL
    {
        // Ahora recibe el objeto pUsuario como parámetro
        public AccesosEN ValidarCredenciales(AccesosEN pUsuario)
        {
            AccesosEN usuario = null;

            using (SqlConnection conn = ConexionDAL.ObtenerConexion())
            {
                string query = "SELECT usuario_id, correo, nombres, apellidos, rol_id " +
                               "FROM Usuarios " +
                               "WHERE correo = @Correo AND password_hash = @Contrasena";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Correo", pUsuario.correo);
                    cmd.Parameters.AddWithValue("@Contrasena", pUsuario.password_hash);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            usuario = new AccesosEN
                            {
                                usuario_id = Convert.ToInt32(reader["usuario_id"]),
                                correo = reader["correo"].ToString(),
                                nombres = reader["nombres"].ToString(),
                                apellidos = reader["apellidos"].ToString(),
                                rol_id = Convert.ToInt32(reader["rol_id"])
                            };
                        }
                    }
                }
            }

            return usuario;
        }
    }
}