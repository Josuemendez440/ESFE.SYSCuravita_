using ESFE.SYSCURAVITA.EN;
using System;
using Microsoft.Data.SqlClient;

namespace ESFE.SYSCURAVITA_DAL
{
    public static class FacturaDAL
    {
        public static bool RegistrarPago(FacturaEN factura)
        {
            try
            {
                using (SqlConnection conn = ConexionDAL.ObtenerConexion())
                {
                    conn.Open();

                    // Mapeo según los IDs reales de tu catálogo
                    int metodoPagoId = (factura.MetodoPago == "Tarjeta") ? 2 : 1;
                    int estadoPagoId = 1;

                    // IMPORTANTE: Asegúrate de enviar un usuario_id REAL existente en la tabla Usuarios
                    int cajeroId = 1;

                    // CORREGIDO: Se cambia 'pago' por 'Pagos' (nombre exacto de tu tabla)
                    string queryPago = @"INSERT INTO [dbo].[Pagos] 
                                        (consulta_id, metodo_pago_id, estado_pago_id, monto_pagado, fecha_pago, cajero_id) 
                                         VALUES 
                                        (@ConsultaId, @MetodoPagoId, @EstadoPagoId, @MontoPagado, @FechaPago, @CajeroId)";

                    using (SqlCommand cmd = new SqlCommand(queryPago, conn))
                    {
                        cmd.Parameters.AddWithValue("@ConsultaId", factura.PacienteId);
                        cmd.Parameters.AddWithValue("@MetodoPagoId", metodoPagoId);
                        cmd.Parameters.AddWithValue("@EstadoPagoId", estadoPagoId);
                        cmd.Parameters.AddWithValue("@MontoPagado", factura.Total);
                        cmd.Parameters.AddWithValue("@FechaPago", factura.FechaEmision == default ? DateTime.Now : factura.FechaEmision);
                        cmd.Parameters.AddWithValue("@CajeroId", cajeroId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                // Revisa la ventana "Salida" (Output) de Visual Studio para ver si salta alguna Foreign Key
                System.Diagnostics.Debug.WriteLine($"=== ERROR SQL ({sqlEx.Number}) ===");
                System.Diagnostics.Debug.WriteLine(sqlEx.Message);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error general en FacturaDAL: " + ex.Message);
                return false;
            }
        }
    }
}