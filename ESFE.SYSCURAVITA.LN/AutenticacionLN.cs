using System;
using System.Security.Cryptography;
using System.Text;
using ESFE.SYSCURAVITA.EN;
using ESFE.SYSCURAVITA_DAL;

namespace ESFE.SYSCURAVITA.LN
{
    public class AutenticacionLN
    {
        /// <summary>
        /// Genera el hash criptográfico SHA-256 en formato hexadecimal.
        /// </summary>
        public static string GenerarHashSHA256(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;

            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
            var builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        public AccesosEN? Autenticar(AccesosEN? pUsuario)
        {
            if (pUsuario == null ||
                string.IsNullOrWhiteSpace(pUsuario.correo) ||
                string.IsNullOrWhiteSpace(pUsuario.password_hash))
            {
                return null;
            }

            string passwordIngresada = pUsuario.password_hash;
            string hashCalculado = GenerarHashSHA256(passwordIngresada);

            // 1. Intentar validar con la contraseña encriptada en hash SHA-256
            pUsuario.password_hash = hashCalculado;
            var usuario = ValidarCredencialesDAL.ValidarCredenciales(pUsuario);

            // 2. Si no coincide por hash (en caso de registros existentes en texto plano), intentar validación directa como fallback
            if (usuario == null)
            {
                pUsuario.password_hash = passwordIngresada;
                usuario = ValidarCredencialesDAL.ValidarCredenciales(pUsuario);
            }

            return usuario;
        }
    }
}