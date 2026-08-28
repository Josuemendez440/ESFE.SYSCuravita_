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

                    // Mapeo de Método de Pago a ID (Ajusta los IDs según tu BD)
                    int metodoPagoId = (factura.MetodoPago == "Tarjeta") ? 2 : 1; // 1: Efectivo, 2: Tarjeta
                    int estadoPagoId = 1; // 1: Pagado / Completado
                    int cajeroId = 1;     // ID por defecto del cajero o usuario activo

                    // Inserción directa a la tabla 'pago'
                    string queryPago = @"INSERT INTO pago 
                                        (consulta_id, metodo_pago_id, estado_pago_id, monto_pagado, fecha_pago, cajero_id) 
                                         VALUES 
                                        (@ConsultaId, @MetodoPagoId, @EstadoPagoId, @MontoPagado, @FechaPago, @CajeroId)";

                    using (SqlCommand cmd = new SqlCommand(queryPago, conn))
                    {
                        // Se usa PacienteId como referencia de consulta si se envía en el DTO
                        cmd.Parameters.AddWithValue("@ConsultaId", factura.PacienteId);
                        cmd.Parameters.AddWithValue("@MetodoPagoId", metodoPagoId);
                        cmd.Parameters.AddWithValue("@EstadoPagoId", estadoPagoId);
                        cmd.Parameters.AddWithValue("@MontoPagado", factura.Total);
                        cmd.Parameters.AddWithValue("@FechaPago", factura.FechaEmision);
                        cmd.Parameters.AddWithValue("@CajeroId", cajeroId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en FacturaDAL.RegistrarPago: " + ex.Message);
                return false;
            }
        }
    }
}