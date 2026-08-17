using ESFE.SYSCURAVITA.DAL;
using ESFE.SYSCURAVITA.EN;

namespace ESFE.SYSCURAVITA.LN
{
    public class AutenticacionLN
    {
        private readonly ValidarCredencialesDAL _validarDAL = new ValidarCredencialesDAL();

        // Recibe el objeto pUsuario con el correo y password cargados
        public AccesosEN Autenticar(AccesosEN pUsuario)
        {
            // Validamos que el objeto no venga nulo ni con campos vacíos
            if (pUsuario == null ||
                string.IsNullOrWhiteSpace(pUsuario.correo) ||
                string.IsNullOrWhiteSpace(pUsuario.password_hash))
            {
                return null;
            }

            // Enviamos el objeto completo a la DAL
            return _validarDAL.ValidarCredenciales(pUsuario);
        }
    }
}