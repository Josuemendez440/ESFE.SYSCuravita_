using System;
using System.Collections.Generic;

namespace ESFE.SYSCURAVITA.EN
{
    public class FacturaEN
    {
        public int Id { get; set; }
        public string NumeroFactura { get; set; }
        public int PacienteId { get; set; }
        public string PacienteNombre { get; set; }
        public string MetodoPago { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public DateTime FechaEmision { get; set; }
        public List<DetalleFacturaEN> Detalles { get; set; } = new List<DetalleFacturaEN>();
    }

    public class DetalleFacturaEN
    {
        public int Id { get; set; }
        public int FacturaId { get; set; }
        public string Descripcion { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal TotalItem => Cantidad * PrecioUnitario;
    }

    // DTO para deserializar el JSON enviado desde facturacion.js
    public class SolicitudPagoDTO
    {
        public string Accion { get; set; }
        public string NumeroFactura { get; set; }
        public string Paciente { get; set; }
        public string MetodoPago { get; set; }
        public decimal MontoTotal { get; set; }
        public string FechaEmision { get; set; }
    }

    // DTO para responder al WebView2
    public class RespuestaPagoDTO
    {
        public string Accion { get; set; } = "PAGO_CONFIRMADO";
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
    }
}