using System;
using Microsoft.Data.SqlClient;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA_DAL
{
    public class ValidarCredencialesDAL
    {
        public static AccesosEN? ValidarCredenciales(AccesosEN pUsuario)
        {
            AccesosEN? usuario = null;

            using var conn = ConexionDAL.ObtenerConexion();
            string query = "SELECT usuario_id, correo, nombres, apellidos, rol_id " +
                           "FROM Usuarios " +
                           "WHERE correo = @Correo AND password_hash = @Contrasena";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Correo", pUsuario.correo ?? string.Empty);
            cmd.Parameters.AddWithValue("@Contrasena", pUsuario.password_hash ?? string.Empty);

            conn.Open();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                usuario = new AccesosEN
                {
                    usuario_id = Convert.ToInt32(reader["usuario_id"]),
                    correo = reader["correo"]?.ToString() ?? string.Empty,
                    nombres = reader["nombres"]?.ToString() ?? string.Empty,
                    apellidos = reader["apellidos"]?.ToString() ?? string.Empty,
                    rol_id = Convert.ToInt32(reader["rol_id"])
                };
            }

            return usuario;
        }
    }
}