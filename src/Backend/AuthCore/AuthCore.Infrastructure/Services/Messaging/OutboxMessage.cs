namespace AuthCore.Infrastructure.Services.Messaging;

/// <summary>
/// Representa uma mensagem persistida na outbox.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Identificador da mensagem.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Tipo lógico da mensagem.
    /// </summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>
    /// Conteúdo serializado da mensagem.
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Data de ocorrência do evento em UTC.
    /// </summary>
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>
    /// Data de processamento da mensagem em UTC.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>
    /// Quantidade de tentativas de processamento.
    /// </summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// Última mensagem de erro registrada.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    private OutboxMessage()
    {
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="id">Identificador da mensagem.</param>
    /// <param name="type">Tipo lógico da mensagem.</param>
    /// <param name="content">Conteúdo serializado da mensagem.</param>
    /// <param name="occurredAtUtc">Data de ocorrência em UTC.</param>
    /// <param name="processedAtUtc">Data de processamento em UTC.</param>
    /// <param name="attemptCount">Quantidade de tentativas registradas.</param>
    /// <param name="lastError">Última mensagem de erro.</param>
    private OutboxMessage(
        Guid id,
        string type,
        string content,
        DateTime occurredAtUtc,
        DateTime? processedAtUtc,
        int attemptCount,
        string? lastError)
    {
        Id = id;
        Type = Normalize(type);
        Content = Normalize(content);
        OccurredAtUtc = occurredAtUtc;
        ProcessedAtUtc = processedAtUtc;
        AttemptCount = attemptCount;
        LastError = NormalizeOptional(lastError);

        Validate();
    }

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="type">Tipo lógico da mensagem.</param>
    /// <param name="content">Conteúdo serializado da mensagem.</param>
    /// <param name="occurredAtUtc">Data de ocorrência em UTC.</param>
    /// <returns>Mensagem criada.</returns>
    public static OutboxMessage Create(string type, string content, DateTime occurredAtUtc)
    {
        return new OutboxMessage(
            Guid.NewGuid(),
            type,
            content,
            occurredAtUtc,
            processedAtUtc: null,
            attemptCount: 0,
            lastError: null);
    }

    /// <summary>
    /// Operação para reconstruir uma mensagem persistida.
    /// </summary>
    /// <param name="id">Identificador da mensagem.</param>
    /// <param name="type">Tipo lógico da mensagem.</param>
    /// <param name="content">Conteúdo serializado da mensagem.</param>
    /// <param name="occurredAtUtc">Data de ocorrência em UTC.</param>
    /// <param name="processedAtUtc">Data de processamento em UTC.</param>
    /// <param name="attemptCount">Quantidade de tentativas registradas.</param>
    /// <param name="lastError">Última mensagem de erro.</param>
    /// <returns>Mensagem reconstruída.</returns>
    public static OutboxMessage Restore(
        Guid id,
        string type,
        string content,
        DateTime occurredAtUtc,
        DateTime? processedAtUtc,
        int attemptCount,
        string? lastError)
    {
        return new OutboxMessage(
            id,
            type,
            content,
            occurredAtUtc,
            processedAtUtc,
            attemptCount,
            lastError);
    }

    /// <summary>
    /// Operação para marcar a mensagem como processada.
    /// </summary>
    /// <param name="processedAtUtc">Data do processamento em UTC.</param>
    /// <returns>Mensagem processada.</returns>
    public OutboxMessage MarkAsProcessed(DateTime processedAtUtc)
    {
        if (processedAtUtc == default)
            throw new InvalidOperationException("A data de processamento da outbox é obrigatória.");

        return new OutboxMessage(
            Id,
            Type,
            Content,
            OccurredAtUtc,
            processedAtUtc,
            AttemptCount,
            LastError);
    }

    /// <summary>
    /// Operação para registrar uma falha no processamento.
    /// </summary>
    /// <param name="errorMessage">Mensagem de erro da tentativa.</param>
    /// <returns>Mensagem com falha registrada.</returns>
    public OutboxMessage RegisterFailure(string errorMessage)
    {
        return new OutboxMessage(
            Id,
            Type,
            Content,
            OccurredAtUtc,
            ProcessedAtUtc,
            AttemptCount + 1,
            errorMessage);
    }

    /// <summary>
    /// Operação para validar a consistência da mensagem.
    /// </summary>
    private void Validate()
    {
        if (Id == Guid.Empty)
            throw new InvalidOperationException("O identificador da mensagem de outbox é obrigatório.");
        if (string.IsNullOrWhiteSpace(Type))
            throw new InvalidOperationException("O tipo da mensagem de outbox é obrigatório.");
        if (string.IsNullOrWhiteSpace(Content))
            throw new InvalidOperationException("O conteúdo da mensagem de outbox é obrigatório.");
        if (OccurredAtUtc == default)
            throw new InvalidOperationException("A data de ocorrência da mensagem de outbox é obrigatória.");
        if (AttemptCount < 0)
            throw new InvalidOperationException("A quantidade de tentativas da outbox não pode ser negativa.");
    }

    /// <summary>
    /// Operação para normalizar texto obrigatório.
    /// </summary>
    /// <param name="value">Texto informado.</param>
    /// <returns>Texto normalizado.</returns>
    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    /// <summary>
    /// Operação para normalizar texto opcional.
    /// </summary>
    /// <param name="value">Texto informado.</param>
    /// <returns>Texto normalizado ou nulo.</returns>
    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
