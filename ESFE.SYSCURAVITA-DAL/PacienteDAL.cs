using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA_DAL
{
    public class PacienteDAL
    {
        public static List<PacienteEN> ObtenerTodos()
        {
            var lista = new List<PacienteEN>();

            using var conn = ConexionDAL.ObtenerConexion();
            string query = @"SELECT paciente_id, codigo_expediente, nombres, apellidos, 
                                    dui_documento, telefono, direccion, fecha_nacimiento, genero 
                             FROM Pacientes ORDER BY paciente_id DESC";

            using var cmd = new SqlCommand(query, conn);
            conn.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new PacienteEN
                {
                    paciente_id = Convert.ToInt32(reader["paciente_id"]),
                    codigo_expediente = reader["codigo_expediente"]?.ToString() ?? string.Empty,
                    nombres = reader["nombres"]?.ToString() ?? string.Empty,
                    apellidos = reader["apellidos"]?.ToString() ?? string.Empty,
                    dui_documento = reader["dui_documento"]?.ToString() ?? string.Empty,
                    telefono = reader["telefono"] != DBNull.Value ? reader["telefono"]?.ToString() : string.Empty,
                    direccion = reader["direccion"] != DBNull.Value ? reader["direccion"]?.ToString() : string.Empty,
                    fecha_nacimiento = reader["fecha_nacimiento"] != DBNull.Value ? Convert.ToDateTime(reader["fecha_nacimiento"]) : null,
                    genero = reader["genero"] != DBNull.Value ? reader["genero"]?.ToString() : string.Empty
                });
            }

            return lista;
        }

        public static bool Guardar(PacienteEN pPaciente)
        {
            using var conn = ConexionDAL.ObtenerConexion();
            conn.Open();

            string queryCount = "SELECT ISNULL(MAX(paciente_id), 0) + 1 FROM Pacientes";
            using var cmdCount = new SqlCommand(queryCount, conn);
            int siguienteId = Convert.ToInt32(cmdCount.ExecuteScalar());
            string codigoExpediente = $"PAC-{siguienteId:D4}";

            string query = @"INSERT INTO Pacientes 
                            (codigo_expediente, nombres, apellidos, dui_documento, telefono, direccion, fecha_nacimiento, genero, fecha_creacion) 
                            VALUES (@Codigo, @Nombres, @Apellidos, @Dui, @Telefono, @Direccion, @FechaNac, @Genero, GETDATE())";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Codigo", codigoExpediente);
            cmd.Parameters.AddWithValue("@Nombres", pPaciente.nombres ?? string.Empty);
            cmd.Parameters.AddWithValue("@Apellidos", pPaciente.apellidos ?? string.Empty);
            cmd.Parameters.AddWithValue("@Dui", pPaciente.dui_documento ?? string.Empty);
            cmd.Parameters.AddWithValue("@Telefono", (object?)pPaciente.telefono ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Direccion", (object?)pPaciente.direccion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FechaNac", (object?)pPaciente.fecha_nacimiento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Genero", (object?)pPaciente.genero ?? DBNull.Value);

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}