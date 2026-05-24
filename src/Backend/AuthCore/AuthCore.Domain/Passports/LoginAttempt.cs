using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common;

namespace AuthCore.Domain.Passports;

/// <summary>
/// Representa o controle de tentativas de login.
/// </summary>
public sealed class LoginAttempt : ValueObject
{


    /// <summary>
    /// Quantidade máxima de falhas permitidas antes do bloqueio.
    /// </summary>
    private const int MAX_ATTEMPTS = 5;

    /// <summary>
    /// Duração do bloqueio temporário aplicado após exceder o limite de falhas.
    /// </summary>
    private static readonly TimeSpan LOCK_DURATION = TimeSpan.FromMinutes(15);


    /// <summary>
    /// Quantidade atual de tentativas de login com falha.
    /// </summary>
    public int FailedAttempts { get; private set; }

    /// <summary>
    /// Data e hora da última tentativa de login que falhou.
    /// </summary>
    public DateTime? LastFailedAt { get; private set; }

    /// <summary>
    /// Data e hora até quando a conta permanece bloqueada.
    /// </summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>
    /// Inicializa uma instância de <see cref="LoginAttempt"/> com o estado informado.
    /// </summary>
    /// <param name="failedAttempts">Quantidade de tentativas de login com falha registradas.</param>
    /// <param name="lastFailedAt">Data e hora da última falha registrada.</param>
    /// <param name="lockedUntil">Data e hora de expiração do bloqueio, quando existir.</param>
    private LoginAttempt(
        int failedAttempts,
        DateTime? lastFailedAt,
        DateTime? lockedUntil)
    {
        ValidateState(failedAttempts, lastFailedAt, lockedUntil);
        FailedAttempts = failedAttempts;
        LastFailedAt = lastFailedAt;
        LockedUntil = lockedUntil;
    }

    /// <summary>
    /// Construtor sem parâmetros utilizado por ferramentas de materialização.
    /// </summary>
    private LoginAttempt() { }


    /// <summary>
    /// Cria um novo controle de tentativas.
    /// </summary>
    /// <returns>Instância inicial de <see cref="LoginAttempt"/>.</returns>
    public static LoginAttempt Create()
    {
        return new LoginAttempt(0, null, null);
    }

    /// <summary>
    /// Restaura um controle de tentativas já persistido.
    /// </summary>
    /// <param name="failedAttempts">Quantidade de tentativas falhas.</param>
    /// <param name="lastFailedAt">Data/hora da última falha.</param>
    /// <param name="lockedUntil">Data/hora de expiração do bloqueio.</param>
    /// <returns>Instância restaurada de <see cref="LoginAttempt"/>.</returns>
    public static LoginAttempt Restore(
        int failedAttempts,
        DateTime? lastFailedAt,
        DateTime? lockedUntil)
    {
        return new LoginAttempt(failedAttempts, lastFailedAt, lockedUntil);
    }


    /// <summary>
    /// Registra falha e aplica bloqueio quando necessário.
    /// </summary>
    /// <returns>Instância atualizada com os novos valores.</returns>
    public LoginAttempt RegisterFailure()
    {
        var now = DateTime.UtcNow;

        if (IsLockedAt(now))
            return this;

        var failed = HasExpiredLock(now)
            ? 1
            : FailedAttempts + 1;

        DateTime? lockedUntil = failed >= MAX_ATTEMPTS
            ? now.Add(LOCK_DURATION)
            : null;

        return new LoginAttempt(failed, now, lockedUntil);
    }

    /// <summary>
    /// Reseta contadores e remove bloqueios.
    /// </summary>
    /// <returns>Instância limpa de tentativas.</returns>
    public LoginAttempt Reset()
    {
        return new LoginAttempt(0, null, null);
    }

    /// <summary>
    /// Verifica se a conta está bloqueada.
    /// </summary>
    /// <returns>True quando o bloqueio está ativo.</returns>
    public bool IsLocked()
    {
        return IsLockedAt(DateTime.UtcNow);
    }

    /// <summary>
    /// Obtém mensagem com o tempo restante de bloqueio.
    /// </summary>
    /// <returns>Mensagem ou null quando não há bloqueio.</returns>
    public string? GetLockMessage()
    {
        var now = DateTime.UtcNow;

        if (!IsLockedAt(now))
            return null;

        var remaining = LockedUntil!.Value - now;

        if (remaining.TotalSeconds < 60)
            return $"A conta está temporariamente bloqueada. Tente novamente em {Math.Ceiling(remaining.TotalSeconds)} segundos.";

        return $"A conta está temporariamente bloqueada. Tente novamente em {Math.Ceiling(remaining.TotalMinutes)} minutos.";
    }


    /// <summary>
    /// Operação para obter os componentes usados na igualdade.
    /// </summary>
    /// <returns>Componentes utilizados na comparação.</returns>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return FailedAttempts;
        yield return LastFailedAt ?? DateTime.MinValue;
        yield return LockedUntil ?? DateTime.MinValue;
    }

    /// <summary>
    /// Verifica se o bloqueio ainda está ativo no instante informado.
    /// </summary>
    /// <param name="now">Instante de referência usado para validar o bloqueio.</param>
    /// <returns><c>true</c> quando o bloqueio ainda não expirou; caso contrário, <c>false</c>.</returns>
    private bool IsLockedAt(DateTime now)
    {
        return LockedUntil.HasValue && LockedUntil.Value > now;
    }

    /// <summary>
    /// Verifica se existia um bloqueio anterior que já expirou.
    /// </summary>
    /// <param name="now">Instante de referência usado para comparar a expiração do bloqueio.</param>
    /// <returns><c>true</c> quando há bloqueio expirado; caso contrário, <c>false</c>.</returns>
    private bool HasExpiredLock(DateTime now)
    {
        return LockedUntil.HasValue && LockedUntil.Value <= now;
    }

    /// <summary>
    /// Valida a consistência do estado interno antes de criar ou restaurar a instância.
    /// </summary>
    /// <param name="failedAttempts">Quantidade de tentativas de login com falha registradas.</param>
    /// <param name="lastFailedAt">Data e hora da última falha registrada.</param>
    /// <param name="lockedUntil">Data e hora de expiração do bloqueio, quando existir.</param>
    /// <exception cref="DomainException">Lançada quando a combinação de valores informada representa um estado inválido.</exception>
    private static void ValidateState(
        int failedAttempts,
        DateTime? lastFailedAt,
        DateTime? lockedUntil)
    {
        if (failedAttempts < 0)
            throw new DomainException("A quantidade de tentativas falhas não pode ser negativa.");

        if (failedAttempts == 0 && (lastFailedAt.HasValue || lockedUntil.HasValue))
            throw new DomainException("Não é permitido informar datas de falha ou bloqueio sem tentativas registradas.");

        if (failedAttempts > 0 && !lastFailedAt.HasValue)
            throw new DomainException("A data da última falha é obrigatória quando existem tentativas registradas.");

        if (failedAttempts >= MAX_ATTEMPTS && !lockedUntil.HasValue)
            throw new DomainException("O bloqueio deve ser informado quando o limite de tentativas falhas for atingido.");

        if (lockedUntil.HasValue && failedAttempts < MAX_ATTEMPTS)
            throw new DomainException("O bloqueio só pode existir quando o limite de tentativas falhas for atingido.");

        if (lastFailedAt.HasValue && lockedUntil.HasValue && lockedUntil.Value <= lastFailedAt.Value)
            throw new DomainException("A data de expiração do bloqueio deve ser posterior à última falha.");
    }

}
