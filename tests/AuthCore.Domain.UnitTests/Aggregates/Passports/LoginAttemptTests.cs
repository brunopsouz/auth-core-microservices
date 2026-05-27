using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;

namespace AuthCore.Domain.UnitTests.Aggregates.Passports;

public class LoginAttemptTests
{
    /// <summary>
    /// Verifica se a criação padrão inicializa a entidade sem falhas nem bloqueio.
    /// </summary>
    [Fact]
    public void Create_WhenCalled_ShouldStartUnlocked()
    {
        var attempt = LoginAttempt.Create();

        Assert.Equal(0, attempt.FailedAttempts);
        Assert.Null(attempt.LastFailedAt);
        Assert.Null(attempt.LockedUntil);
        Assert.False(attempt.IsLocked());
    }

    /// <summary>
    /// Verifica se a quinta falha consecutiva aplica o bloqueio temporário.
    /// </summary>
    [Fact]
    public void RegisterFailure_WhenThresholdIsReached_ShouldLock()
    {
        var attempt = LoginAttempt.Create();

        for (var i = 0; i < 5; i++)
            attempt = attempt.RegisterFailure();

        Assert.Equal(5, attempt.FailedAttempts);
        Assert.NotNull(attempt.LastFailedAt);
        Assert.True(attempt.IsLocked());
        Assert.NotNull(attempt.LockedUntil);
    }

    /// <summary>
    /// Verifica se novas falhas durante o bloqueio não alteram o estado atual.
    /// </summary>
    [Fact]
    public void RegisterFailure_WhenAlreadyLocked_ShouldKeepCurrentState()
    {
        var now = DateTime.UtcNow;
        var attempt = LoginAttempt.Restore(
            failedAttempts: 5,
            lastFailedAt: now.AddMinutes(-1),
            lockedUntil: now.AddMinutes(10));

        var updatedAttempt = attempt.RegisterFailure();

        Assert.Equal(attempt.FailedAttempts, updatedAttempt.FailedAttempts);
        Assert.Equal(attempt.LastFailedAt, updatedAttempt.LastFailedAt);
        Assert.Equal(attempt.LockedUntil, updatedAttempt.LockedUntil);
    }

    /// <summary>
    /// Verifica se uma falha após a expiração do bloqueio reinicia a contagem.
    /// </summary>
    [Fact]
    public void RegisterFailure_WhenPreviousLockHasExpired_ShouldRestartCounter()
    {
        var now = DateTime.UtcNow;
        var attempt = LoginAttempt.Restore(
            failedAttempts: 5,
            lastFailedAt: now.AddMinutes(-20),
            lockedUntil: now.AddMinutes(-5));

        var updatedAttempt = attempt.RegisterFailure();

        Assert.Equal(1, updatedAttempt.FailedAttempts);
        Assert.NotNull(updatedAttempt.LastFailedAt);
        Assert.Null(updatedAttempt.LockedUntil);
        Assert.False(updatedAttempt.IsLocked());
    }

    /// <summary>
    /// Verifica se a restauração com estado inconsistente lança exceção de domínio.
    /// </summary>
    [Fact]
    public void Restore_WhenStateIsInvalid_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => LoginAttempt.Restore(
            failedAttempts: 3,
            lastFailedAt: null,
            lockedUntil: null));
    }

    /// <summary>
    /// Verifica se a restauração falha quando o limite de falhas é atingido sem data de bloqueio.
    /// </summary>
    [Fact]
    public void Restore_WhenThresholdIsReachedWithoutLockExpiration_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => LoginAttempt.Restore(
            failedAttempts: 5,
            lastFailedAt: DateTime.UtcNow.AddMinutes(-1),
            lockedUntil: null));
    }
}
