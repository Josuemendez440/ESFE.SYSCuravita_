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

                    // Incluimos paciente_id por si la tabla lo requiere en la base de datos
                    string query = @"INSERT INTO Facturas 
                                    (NumeroFactura, PacienteNombre, MetodoPago, Subtotal, Iva, Total, FechaEmision) 
                                     VALUES 
                                    (@NumeroFactura, @PacienteNombre, @MetodoPago, @Subtotal, @Iva, @Total, @FechaEmision)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NumeroFactura", factura.NumeroFactura ?? string.Empty);
                        cmd.Parameters.AddWithValue("@PacienteNombre", string.IsNullOrWhiteSpace(factura.PacienteNombre) ? "Consumidor Final" : factura.PacienteNombre);
                        cmd.Parameters.AddWithValue("@MetodoPago", factura.MetodoPago ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Subtotal", factura.Subtotal);
                        cmd.Parameters.AddWithValue("@Iva", factura.Iva);
                        cmd.Parameters.AddWithValue("@Total", factura.Total);
                        cmd.Parameters.AddWithValue("@FechaEmision", factura.FechaEmision);

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