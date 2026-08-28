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

            using var transaccion = conexion.BeginTransaction();

            try
            {
                // A. Buscar ID Real del Paciente
                string sqlPaciente = @"SELECT paciente_id FROM Pacientes WHERE codigo_expediente = @Codigo OR paciente_id = @PacienteId";
                int idRealPaciente = 0;

                using (var cmdP = new SqlCommand(sqlPaciente, conexion, transaccion))
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

                // B. Insertar en tabla Consultas
                string sqlInsertConsulta = @"
                    INSERT INTO Consultas (paciente_id, recepcionista_id, fecha_consulta, tipo_atencion_id, estado_consulta_id, monto_consulta, es_emergencia)
                    VALUES (@PacienteId, 1, GETDATE(), 1, 1, 0.00, 0);
                    SELECT SCOPE_IDENTITY();";

                int nuevaConsultaId = 0;
                using (var cmdIns = new SqlCommand(sqlInsertConsulta, conexion, transaccion))
                {
                    cmdIns.Parameters.AddWithValue("@PacienteId", idRealPaciente);
                    object? newId = cmdIns.ExecuteScalar();
                    if (newId != null && newId != DBNull.Value)
                    {
                        nuevaConsultaId = Convert.ToInt32(newId);
                    }
                }

                if (nuevaConsultaId == 0) return false;

                // C. Parsear Signos Vitales para la tabla Triaje
                int sistolica = 120, diastolica = 80, fcNum = 80;
                decimal tempNum = 36.5m, pesoNum = 70.0m;

                if (!string.IsNullOrWhiteSpace(pa) && pa.Contains('/'))
                {
                    var partes = pa.Split('/');
                    int.TryParse(partes[0].Trim(), out sistolica);
                    if (partes.Length > 1) int.TryParse(partes[1].Trim(), out diastolica);
                }

                int.TryParse(fc, out fcNum);
                decimal.TryParse(temp, out tempNum);
                decimal.TryParse(peso, out pesoNum);

                // Insertar en tabla Triaje
                string sqlInsertTriaje = @"
                    INSERT INTO Triaje (consulta_id, enfermero_id, presion_sistolica, presion_diastolica, frecuencia_cardiaca, temperatura, peso_kg, sincronizado_desde_movil, fecha_registro)
                    VALUES (@ConsultaId, 1, @Sistolica, @Diastolica, @FC, @Temp, @Peso, 0, GETDATE());";

                using (var cmdT = new SqlCommand(sqlInsertTriaje, conexion, transaccion))
                {
                    cmdT.Parameters.AddWithValue("@ConsultaId", nuevaConsultaId);
                    cmdT.Parameters.AddWithValue("@Sistolica", sistolica == 0 ? 120 : sistolica);
                    cmdT.Parameters.AddWithValue("@Diastolica", diastolica == 0 ? 80 : diastolica);
                    cmdT.Parameters.AddWithValue("@FC", fcNum == 0 ? 80 : fcNum);
                    cmdT.Parameters.AddWithValue("@Temp", tempNum == 0 ? 36.5m : tempNum);
                    cmdT.Parameters.AddWithValue("@Peso", pesoNum == 0 ? 70.0m : pesoNum);
                    cmdT.ExecuteNonQuery();
                }

                // D. Insertar Diagnóstico
                string sqlInsertDiagnostico = @"
                    INSERT INTO Diagnosticos (consulta_id, conclusion_diagnostico, fecha_registro)
                    VALUES (@ConsultaId, @Diagnostico, GETDATE());";

                using (var cmdD = new SqlCommand(sqlInsertDiagnostico, conexion, transaccion))
                {
                    cmdD.Parameters.AddWithValue("@ConsultaId", nuevaConsultaId);
                    cmdD.Parameters.AddWithValue("@Diagnostico", diagnosticoTexto ?? string.Empty);
                    cmdD.ExecuteNonQuery();
                }

                // E. Insertar Receta y Detalle (si existe receta)
                if (!string.IsNullOrWhiteSpace(receta))
                {
                    string sqlInsertReceta = @"
                        INSERT INTO Recetas (consulta_id, fecha_registro)
                        VALUES (@ConsultaId, GETDATE());
                        SELECT SCOPE_IDENTITY();";

                    int recetaId = 0;
                    using (var cmdR = new SqlCommand(sqlInsertReceta, conexion, transaccion))
                    {
                        cmdR.Parameters.AddWithValue("@ConsultaId", nuevaConsultaId);
                        object? rId = cmdR.ExecuteScalar();
                        if (rId != null && rId != DBNull.Value)
                        {
                            recetaId = Convert.ToInt32(rId);
                        }
                    }

                    if (recetaId > 0)
                    {
                        string sqlInsertDetalle = @"
                            INSERT INTO DetalleRecetas (receta_id, medicamento, indicaciones_dosis)
                            VALUES (@RecetaId, 'Formulación Médica', @Indicaciones);";

                        using var cmdDet = new SqlCommand(sqlInsertDetalle, conexion, transaccion);
                        cmdDet.Parameters.AddWithValue("@RecetaId", recetaId);
                        cmdDet.Parameters.AddWithValue("@Indicaciones", receta);
                        cmdDet.ExecuteNonQuery();
                    }
                }

                transaccion.Commit();
                return true;
            }
            catch
            {
                transaccion.Rollback();
                return false;
            }
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
                    ISNULL('PA: ' + CAST(t.presion_sistolica AS varchar) + '/' + CAST(t.presion_diastolica AS varchar) + ' mmHg | FC: ' + CAST(t.frecuencia_cardiaca AS varchar) + ' lpm | Temp: ' + CAST(t.temperatura AS varchar) + ' °C | Peso: ' + CAST(t.peso_kg AS varchar) + ' Kg' + CHAR(13) + CHAR(10) + ISNULL('Receta: ' + dr.indicaciones_dosis, 'Sin medicamentos formulados.'), '') AS observaciones
                FROM [dbo].[Diagnosticos] d
                INNER JOIN [dbo].[Consultas] c ON d.consulta_id = c.consulta_id
                INNER JOIN [dbo].[Pacientes] p ON c.paciente_id = p.paciente_id
                LEFT JOIN [dbo].[Triaje] t ON c.consulta_id = t.consulta_id
                LEFT JOIN [dbo].[Recetas] r ON c.consulta_id = r.consulta_id
                LEFT JOIN [dbo].[DetalleRecetas] dr ON r.receta_id = dr.receta_id
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