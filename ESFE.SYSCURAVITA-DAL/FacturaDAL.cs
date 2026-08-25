using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA_DAL;
using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ESFE.SYSCURAVITA.DAL
{
    public class FacturaDAL
    {
        public static bool RegistrarPago(FacturaEN factura)
        {
            try
            {
                using (SqlConnection conn = ConexionDAL.ObtenerConexion())
                {
                    conn.Open();
                    string query = @"INSERT INTO Facturas (NumeroFactura, PacienteNombre, MetodoPago, Subtotal, Iva, Total, FechaEmision) 
                                     VALUES (@NumeroFactura, @PacienteNombre, @MetodoPago, @Subtotal, @Iva, @Total, @FechaEmision)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@NumeroFactura", factura.NumeroFactura);
                        cmd.Parameters.AddWithValue("@PacienteNombre", factura.PacienteNombre ?? "Consumidor Final");
                        cmd.Parameters.AddWithValue("@MetodoPago", factura.MetodoPago);
                        cmd.Parameters.AddWithValue("@Subtotal", factura.Subtotal);
                        cmd.Parameters.AddWithValue("@Iva", factura.Iva);
                        cmd.Parameters.AddWithValue("@Total", factura.Total);
                        cmd.Parameters.AddWithValue("@FechaEmision", factura.FechaEmision);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}