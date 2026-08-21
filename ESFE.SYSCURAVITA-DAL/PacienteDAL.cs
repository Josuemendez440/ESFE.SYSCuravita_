using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.DAL
{
    public class PacienteDAL
    {
        public List<PacienteEN> ObtenerTodos()
        {
            List<PacienteEN> lista = new List<PacienteEN>();

            using (SqlConnection conn = ConexionDAL.ObtenerConexion())
            {
                string query = @"SELECT paciente_id, codigo_expediente, nombres, apellidos, 
                                        dui_documento, telefono, direccion, fecha_nacimiento, genero 
                                 FROM Pacientes ORDER BY paciente_id DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new PacienteEN
                            {
                                paciente_id = Convert.ToInt32(reader["paciente_id"]),
                                codigo_expediente = reader["codigo_expediente"].ToString(),
                                nombres = reader["nombres"].ToString(),
                                apellidos = reader["apellidos"].ToString(),
                                dui_documento = reader["dui_documento"].ToString(),
                                telefono = reader["telefono"] != DBNull.Value ? reader["telefono"].ToString() : "",
                                direccion = reader["direccion"] != DBNull.Value ? reader["direccion"].ToString() : "",
                                fecha_nacimiento = reader["fecha_nacimiento"] != DBNull.Value ? Convert.ToDateTime(reader["fecha_nacimiento"]) : null,
                                genero = reader["genero"] != DBNull.Value ? reader["genero"].ToString() : ""
                            });
                        }
                    }
                }
            }
            return lista;
        }

        public bool Guardar(PacienteEN pPaciente)
        {
            using (SqlConnection conn = ConexionDAL.ObtenerConexion())
            {
                conn.Open();

                // Generar código único correlativo (Ej: PAC-0001)
                string queryCount = "SELECT ISNULL(MAX(paciente_id), 0) + 1 FROM Pacientes";
                SqlCommand cmdCount = new SqlCommand(queryCount, conn);
                int siguienteId = Convert.ToInt32(cmdCount.ExecuteScalar());
                string codigoExpediente = $"PAC-{siguienteId:D4}";

                string query = @"INSERT INTO Pacientes 
                                (codigo_expediente, nombres, apellidos, dui_documento, telefono, direccion, fecha_nacimiento, genero, fecha_creacion) 
                                VALUES (@Codigo, @Nombres, @Apellidos, @Dui, @Telefono, @Direccion, @FechaNac, @Genero, GETDATE())";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Codigo", codigoExpediente);
                    cmd.Parameters.AddWithValue("@Nombres", pPaciente.nombres);
                    cmd.Parameters.AddWithValue("@Apellidos", pPaciente.apellidos);
                    cmd.Parameters.AddWithValue("@Dui", pPaciente.dui_documento);
                    cmd.Parameters.AddWithValue("@Telefono", (object)pPaciente.telefono ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Direccion", (object)pPaciente.direccion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FechaNac", (object)pPaciente.fecha_nacimiento ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Genero", (object)pPaciente.genero ?? DBNull.Value);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }
}