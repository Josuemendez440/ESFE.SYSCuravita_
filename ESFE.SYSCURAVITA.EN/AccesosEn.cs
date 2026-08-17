namespace ESFE.SYSCURAVITA.EN
{
    public class AccesosEN
    {
        public int usuario_id { get; set; }
        public string correo { get; set; }
        public string password_hash { get; set; }
        public string nombres { get; set; }
        public string apellidos { get; set; }
        public int rol_id { get; set; }

        public string Rol => rol_id switch
        {
            1 => "Admin",
            2 => "Recepcion",
            3 => "Doctor",
            4 => "Caja",
            _ => "User"
        };

        public string VistaHtml => rol_id switch
        {
            3 => "consulta.html",
            4 => "facturacion.html",
            _ => "expedientes.html"
        };
    }
}