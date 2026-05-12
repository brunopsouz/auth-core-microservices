namespace NotificationCore.Domain.Common.Repositories;

/// <summary>
/// Define operações para controlar transações da unidade de trabalho.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Operação para iniciar uma transação.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Operação para confirmar a transação atual.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Operação para desfazer a transação atual.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação.</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
