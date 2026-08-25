using System;
using ESFE.SYSCURAVITA.DAL;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.LN
{
    public class FacturaLN
    {
        public static RespuestaPagoDTO ProcesarCobro(SolicitudPagoDTO dto)
        {
            if (dto == null || dto.MontoTotal <= 0)
            {
                return new RespuestaPagoDTO { Exito = false, Mensaje = "El monto total de la factura no es válido." };
            }

            // Cálculo inverso del IVA (13%) e importe subtotal
            decimal subtotal = Math.Round(dto.MontoTotal / 1.13m, 2);
            decimal iva = Math.Round(dto.MontoTotal - subtotal, 2);

            FacturaEN nuevaFactura = new FacturaEN
            {
                NumeroFactura = dto.NumeroFactura,
                PacienteNombre = dto.Paciente,
                MetodoPago = dto.MetodoPago,
                Subtotal = subtotal,
                Iva = iva,
                Total = dto.MontoTotal,
                FechaEmision = DateTime.Now
            };

            bool resultado = FacturaDAL.RegistrarPago(nuevaFactura);

            return new RespuestaPagoDTO
            {
                Exito = resultado,
                Mensaje = resultado ? "Factura procesada con éxito." : "Error al guardar el pago en la base de datos."
            };
        }
    }
}