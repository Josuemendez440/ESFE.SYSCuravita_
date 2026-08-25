using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.DAL
{
    public static class ConsultaDAL
    {
        public static int GuardarConsulta(ConsultaEN pConsulta)
        {
            using var conn = ConexionDAL.ObtenerConexion();
            conn.Open();
            string query = @"INSERT INTO Consultas (paciente_id, estado_consulta_id, diagnostico, fecha_consulta) 
                             VALUES (@PacienteId, @EstadoConsultaId, @Diagnostico, GETDATE())";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@PacienteId", pConsulta.PacienteId);
            cmd.Parameters.AddWithValue("@EstadoConsultaId", pConsulta.EstadoConsultaId <= 0 ? 1 : pConsulta.EstadoConsultaId);
            cmd.Parameters.AddWithValue("@Diagnostico", (object?)pConsulta.Diagnostico ?? DBNull.Value);

            return cmd.ExecuteNonQuery();
        }

        public static List<ConsultaEN> ObtenerHistorial(int pacienteId)
        {
            List<ConsultaEN> lista = [];

            using var conn = ConexionDAL.ObtenerConexion();
            conn.Open();
            string query = @"SELECT consulta_id, paciente_id, estado_consulta_id, diagnostico, fecha_consulta 
                             FROM Consultas 
                             WHERE paciente_id = @PacienteId 
                             ORDER BY consulta_id DESC";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@PacienteId", pacienteId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new()
                {
                    ConsultaId = reader["consulta_id"] != DBNull.Value ? Convert.ToInt32(reader["consulta_id"]) : 0,
                    PacienteId = reader["paciente_id"] != DBNull.Value ? Convert.ToInt32(reader["paciente_id"]) : 0,
                    EstadoConsultaId = reader["estado_consulta_id"] != DBNull.Value ? Convert.ToInt32(reader["estado_consulta_id"]) : 1,
                    Diagnostico = reader["diagnostico"] != DBNull.Value ? reader["diagnostico"].ToString() ?? string.Empty : string.Empty,
                    FechaConsulta = reader["fecha_consulta"] != DBNull.Value ? Convert.ToDateTime(reader["fecha_consulta"]) : DateTime.Now
                });
            }

            return lista;
        }
    }
}