using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace ESFE.SYSCURAVITA_DAL
{
    public static class ConsultaDAL
    {
        public static bool GuardarDiagnostico(
            int pacienteId,
            string? codigoExpediente,
            string? diagnosticoTexto,
            string? pa = "N/A",
            string? fc = "N/A",
            string? temp = "N/A",
            string? peso = "N/A",
            string? receta = "")
        {
            using var conexion = ConexionDAL.ObtenerConexion();
            if (conexion == null) return false;
            conexion.Open();

            string sqlPaciente = @"SELECT paciente_id FROM Pacientes WHERE codigo_expediente = @Codigo OR paciente_id = @PacienteId";
            int idRealPaciente = 0;

            using (var cmdP = new SqlCommand(sqlPaciente, conexion))
            {
                cmdP.Parameters.AddWithValue("@Codigo", codigoExpediente ?? string.Empty);
                cmdP.Parameters.AddWithValue("@PacienteId", pacienteId);
                object? res = cmdP.ExecuteScalar();
                if (res != null && res != DBNull.Value)
                {
                    idRealPaciente = Convert.ToInt32(res);
                }
            }

            if (idRealPaciente == 0) return false;

            // 1. Insertar una nueva consulta
            string sqlInsertConsulta = @"
                INSERT INTO Consultas (paciente_id, recepcionista_id, fecha_consulta, tipo_atencion_id, estado_consulta_id, monto_consulta, es_emergencia)
                VALUES (@PacienteId, 1, GETDATE(), 1, 1, 0.00, 0);
                SELECT SCOPE_IDENTITY();";

            int nuevaConsultaId = 0;
            using (var cmdIns = new SqlCommand(sqlInsertConsulta, conexion))
            {
                cmdIns.Parameters.AddWithValue("@PacienteId", idRealPaciente);
                object? newId = cmdIns.ExecuteScalar();
                if (newId != null && newId != DBNull.Value)
                {
                    nuevaConsultaId = Convert.ToInt32(newId);
                }
            }

            if (nuevaConsultaId == 0) return false;

            // 2. Formatear signos vitales y receta para la nueva columna
            string vitalsTexto = $"PA: {pa} mmHg | FC: {fc} lpm | Temp: {temp} °C | Peso: {peso} Kg";
            string recetaTexto = string.IsNullOrWhiteSpace(receta) ? "Sin medicamentos formulados." : (receta.StartsWith("Receta:") ? receta : $"Receta: {receta}");
            string detalleCompleto = $"{vitalsTexto}\n{recetaTexto}";

            // 3. Registrar el diagnóstico usando la columna observaciones
            string sqlInsertDiagnostico = @"
                INSERT INTO Diagnosticos (consulta_id, conclusion_diagnostico, fecha_registro, observaciones)
                VALUES (@ConsultaId, @Diagnostico, GETDATE(), @Observaciones);";

            using var cmdD = new SqlCommand(sqlInsertDiagnostico, conexion);
            cmdD.Parameters.AddWithValue("@ConsultaId", nuevaConsultaId);
            cmdD.Parameters.AddWithValue("@Diagnostico", diagnosticoTexto ?? string.Empty);
            cmdD.Parameters.AddWithValue("@Observaciones", detalleCompleto);

            return cmdD.ExecuteNonQuery() > 0;
        }

        public static List<HistorialDTO> ObtenerHistorial(int pacienteId, string? codigoExpediente)
        {
            var lista = new List<HistorialDTO>();

            using var conexion = ConexionDAL.ObtenerConexion();
            if (conexion == null) return lista;
            conexion.Open();

            string query = @"
                SELECT 
                    FORMAT(d.fecha_registro, 'd/M/yyyy') AS fecha,
                    FORMAT(d.fecha_registro, 'hh:mm') AS hora,
                    d.conclusion_diagnostico AS diagnostico,
                    ISNULL(d.observaciones, '') AS observaciones
                FROM [dbo].[Diagnosticos] d
                INNER JOIN [dbo].[Consultas] c ON d.consulta_id = c.consulta_id
                INNER JOIN [dbo].[Pacientes] p ON c.paciente_id = p.paciente_id
                WHERE p.codigo_expediente = @Codigo 
                   OR (p.paciente_id = @PacienteId AND @PacienteId > 0)
                ORDER BY d.fecha_registro DESC";

            using var cmd = new SqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@Codigo", codigoExpediente ?? string.Empty);
            cmd.Parameters.AddWithValue("@PacienteId", pacienteId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new HistorialDTO
                {
                    Fecha = reader["fecha"]?.ToString() ?? string.Empty,
                    Hora = reader["hora"]?.ToString() ?? string.Empty,
                    Diagnostico = reader["diagnostico"]?.ToString() ?? string.Empty,
                    Observaciones = reader["observaciones"]?.ToString() ?? string.Empty
                });
            }

            return lista;
        }
    }
}