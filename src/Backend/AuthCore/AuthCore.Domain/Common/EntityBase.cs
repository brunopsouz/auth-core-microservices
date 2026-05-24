namespace AuthCore.Domain.Common; 

/// <summary>
/// Classe para entidade base contendo propriedades em comum entre todas entidades.
/// </summary>
public abstract class EntityBase
{
    /// <summary>
    /// Identificador único.
    /// </summary>
    public Guid Id { get; protected set; }

    /// <summary>
    /// Status para regra de softdelete no banco como ativo visível true ou false.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Data de criação dos dados.
    /// </summary>
    public DateTime CreatedAt { get; protected set; }
    
    /// <summary>
    /// Data de alteração dos dados.
    /// </summary>
    public DateTime UpdateAt { get; protected set; }

    /// <summary>
    /// Inicializa a entidade base.
    /// </summary>
    protected EntityBase()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.Now;
        UpdateAt = CreatedAt;
    }

    /// <summary>
    /// Método utilizado para alterar a data de modificação.
    /// </summary>
    protected void SetUpdateData()
    {
        UpdateAt = DateTime.Now;
    }

    /// <summary>
    /// Construtor com Id útil para ORMs.
    /// </summary>
    /// <param name="id">Identificador único.</param>
    protected EntityBase(Guid id)
    {
        Id = id;
    }

    /// <summary>
    /// Construtor utilizado para reconstituir a entidade com o estado persistido.
    /// </summary>
    /// <param name="id">Identificador único.</param>
    /// <param name="createdAt">Data de criação persistida.</param>
    /// <param name="updateAt">Data da última atualização persistida.</param>
    /// <param name="isActive">Status de atividade persistido.</param>
    protected EntityBase(
        Guid id,
        DateTime createdAt,
        DateTime updateAt,
        bool isActive)
    {
        Id = id;
        CreatedAt = createdAt;
        UpdateAt = updateAt;
        IsActive = isActive;
    }

    /// <summary>
    /// Desativa a entidade (soft delete), marcando-a como inativa.
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        SetUpdateData();
    }

    /// <summary>
    /// Ativa a entidade, marcando-a como ativa/visível novamente.
    /// </summary>
    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        SetUpdateData();
    }

    /// <summary>
    /// Determina se o objeto especificado é igual à instância atual.
    /// </summary>
    /// <param name="obj">Objeto a ser comparado com a instância atual.</param>
    /// <returns>true se o objeto for uma instância de <see cref="EntityBase"/> com o mesmo Id, caso contrário, false.</returns>
    public override bool Equals(object? obj)
    {
        if(obj is not EntityBase other) 
            return false;
        if(ReferenceEquals(this, other)) 
            return true;
        return Id.Equals(other.Id);
    }

    /// <summary>
    /// Retorna o código hash da instância atual.
    /// </summary>
    /// <returns>Código hash baseado no Id da entidade.</returns>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// Compara duas instâncias de <see cref="EntityBase"/> para verificar se são iguais.
    /// </summary>
    /// <param name="left">Primeira instância a ser comparada</param>
    /// <param name="right">Segunda instância a ser comparada.</param>
    /// <returns>true se ambas forem nulas ou se tiverem o mesmo Id, caso contrário, false.</returns>
    public static bool operator ==(EntityBase left, EntityBase right)
    {
        if(ReferenceEquals(left, null))
            return ReferenceEquals(right, null);

        return left.Equals(right);
    }

    /// <summary>
    /// Compara duas instâncias de <see cref="EntityBase"/> para verificar se são diferentes.
    /// </summary>
    /// <param name="left">Primeira instância a ser comparada</param>
    /// <param name="right">Segunda instância a ser comparada.</param>
    /// <returns>true se os objetos não forem iguais, caso contrário, false.</returns>
    public static bool operator != (EntityBase left, EntityBase right)
    {
        return !left.Equals(right);
    }
}
