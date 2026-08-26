using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA_DAL;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace ESFE.SYSCURAVITA.DAL
{
    public static class PacienteDAL
    {
        public static bool Guardar(PacienteEN pPaciente)
        {
            // Genera automáticamente el código con formato PAC-XXXX en la base de datos
            string queryInsert = @"
                INSERT INTO Pacientes (codigo_expediente, nombres, apellidos, dui_documento, telefono, fecha_nacimiento) 
                VALUES (
                    ISNULL(NULLIF(@codigo_expediente, ''), CONCAT('PAC-', RIGHT('000' + CAST((ISNULL((SELECT MAX(paciente_id) FROM Pacientes), 0) + 1) AS VARCHAR), 4))),
                    @nombres, 
                    @apellidos, 
                    @dui, 
                    @telefono, 
                    @fecha_nacimiento
                )";

            using var conexion = ConexionDAL.ObtenerConexion();
            using var cmd = new SqlCommand(queryInsert, conexion);

            cmd.Parameters.AddWithValue("@codigo_expediente", pPaciente.codigo_expediente ?? string.Empty);
            cmd.Parameters.AddWithValue("@nombres", pPaciente.nombres ?? string.Empty);
            cmd.Parameters.AddWithValue("@apellidos", pPaciente.apellidos ?? string.Empty);
            cmd.Parameters.AddWithValue("@dui", pPaciente.dui_documento ?? string.Empty);
            cmd.Parameters.AddWithValue("@telefono", pPaciente.telefono ?? string.Empty);
            cmd.Parameters.AddWithValue("@fecha_nacimiento", (object?)pPaciente.fecha_nacimiento ?? DBNull.Value);

            conexion.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        public static List<PacienteEN> ObtenerTodos()
        {
            var lista = new List<PacienteEN>();
            string querySelect = "SELECT paciente_id, codigo_expediente, nombres, apellidos, dui_documento, telefono, fecha_nacimiento FROM Pacientes";

            using var conexion = ConexionDAL.ObtenerConexion();
            using var cmd = new SqlCommand(querySelect, conexion);

            conexion.Open();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new PacienteEN
                {
                    paciente_id = reader.GetInt32(0),
                    codigo_expediente = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    nombres = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    apellidos = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    dui_documento = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    telefono = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    fecha_nacimiento = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                });
            }

            return lista;
        }

        public static bool Eliminar(int pacienteId)
        {
            string queryDelete = "DELETE FROM Pacientes WHERE paciente_id = @paciente_id";

            using var conexion = ConexionDAL.ObtenerConexion();
            using var cmd = new SqlCommand(queryDelete, conexion);

            cmd.Parameters.AddWithValue("@paciente_id", pacienteId);

            conexion.Open();
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}