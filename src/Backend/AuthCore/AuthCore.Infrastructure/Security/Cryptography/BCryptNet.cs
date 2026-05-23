using AuthCore.Domain.Security.Cryptography;

namespace AuthCore.Infrastructure.Security.Cryptography;

/// <summary>
/// Representa implementação de criptografia de senha com BCrypt.
/// </summary>
internal sealed class BCryptNet : IPasswordEncripter
{
    /// <summary>
    /// Operação para criptografar uma senha.
    /// </summary>
    /// <param name="password">Senha em texto puro.</param>
    /// <returns>Senha criptografada.</returns>
    public string Encrypt(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <summary>
    /// Operação para validar uma senha com o valor criptografado.
    /// </summary>
    /// <param name="password">Senha em texto puro.</param>
    /// <param name="passwordHash">Senha criptografada para validação.</param>
    /// <returns><c>true</c> quando a senha é válida; caso contrário, <c>false</c>.</returns>
    public bool IsValid(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
