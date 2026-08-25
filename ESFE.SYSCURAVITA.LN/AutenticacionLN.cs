using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA_DAL;

namespace ESFE.SYSCURAVITA.LN
{
    public class AutenticacionLN
    {
        public AccesosEN? Autenticar(AccesosEN? pUsuario)
        {
            if (pUsuario == null ||
                string.IsNullOrWhiteSpace(pUsuario.correo) ||
                string.IsNullOrWhiteSpace(pUsuario.password_hash))
            {
                return null;
            }

            return ValidarCredencialesDAL.ValidarCredenciales(pUsuario);
        }
    }
}