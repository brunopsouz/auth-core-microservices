namespace NotificationCore.Domain.Notifications.Providers;

/// <summary>
/// Representa resultado retornado pelo provedor de e-mail.
/// </summary>
public sealed class EmailProviderResult
{
    /// <summary>
    /// Nome do provedor.
    /// </summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Indica se o envio foi concluído com sucesso.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Indica se a falha permite nova tentativa.
    /// </summary>
    public bool IsTemporaryFailure { get; init; }

    /// <summary>
    /// Código de erro retornado pelo provedor.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Mensagem de erro retornada pelo provedor.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Identificador da mensagem retornado pelo provedor.
    /// </summary>
    public string ProviderMessageId { get; init; } = string.Empty;

    /// <summary>
    /// Operação para criar resultado de sucesso.
    /// </summary>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="providerMessageId">Identificador retornado pelo provedor.</param>
    /// <returns>Resultado de sucesso.</returns>
    public static EmailProviderResult Success(string provider, string providerMessageId)
    {
        return new EmailProviderResult
        {
            Provider = provider,
            IsSuccess = true,
            IsTemporaryFailure = false,
            ProviderMessageId = providerMessageId
        };
    }

    /// <summary>
    /// Operação para criar resultado de falha temporária.
    /// </summary>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem de erro.</param>
    /// <returns>Resultado de falha temporária.</returns>
    public static EmailProviderResult TemporaryFailure(
        string provider,
        string errorCode,
        string errorMessage)
    {
        return new EmailProviderResult
        {
            Provider = provider,
            IsSuccess = false,
            IsTemporaryFailure = true,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Operação para criar resultado de falha permanente.
    /// </summary>
    /// <param name="provider">Nome do provedor.</param>
    /// <param name="errorCode">Código de erro.</param>
    /// <param name="errorMessage">Mensagem de erro.</param>
    /// <returns>Resultado de falha permanente.</returns>
    public static EmailProviderResult PermanentFailure(
        string provider,
        string errorCode,
        string errorMessage)
    {
        return new EmailProviderResult
        {
            Provider = provider,
            IsSuccess = false,
            IsTemporaryFailure = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
