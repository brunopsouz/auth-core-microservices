namespace AuthCore.Domain.Users;

/// <summary>Perfis de usuário.</summary>
public enum Role
{
    /// <summary>
    /// Concede permissões de admin.
    /// </summary>
    Administrador = 0,
    
    /// <summary>
    /// Concede permissões necessárias para desenvolvimento dentro da plataforma.
    /// </summary>
    Developer = 1,

    /// <summary>
    /// Concede permissões padrão ao usuário.
    /// </summary>
    User = 2

}