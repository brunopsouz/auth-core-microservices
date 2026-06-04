using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;

namespace AuthCore.Domain.UnitTests.Aggregates.Passports;

public class PasswordTests
{
    /// <summary>
    /// Verifica se a criação da senha inicializa o controle de tentativas sem bloqueio.
    /// </summary>
    [Fact]
    public void Create_WhenStateIsValid_ShouldInitializeWithCleanLoginAttempt()
    {
        var userId = Guid.NewGuid();

        var password = Password.Create(userId, "hashed-password", PasswordStatus.Active);

        Assert.Equal(userId, password.UserId);
        Assert.Equal("hashed-password", password.Value);
        Assert.Equal(PasswordStatus.Active, password.Status);
        Assert.NotNull(password.LoginAttempt);
        Assert.False(password.IsLocked());
        Assert.Equal(0, password.LoginAttempt.FailedAttempts);
    }

    /// <summary>
    /// Verifica se a criação da senha falha quando o identificador do usuário é inválido.
    /// </summary>
    [Fact]
    public void Create_WhenUserIdIsEmpty_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            Password.Create(Guid.Empty, "hashed-password", PasswordStatus.Active));
    }

    /// <summary>
    /// Verifica se a restauração falha quando o status não corresponde ao bloqueio atual.
    /// </summary>
    [Fact]
    public void Restore_WhenLoginAttemptIsLockedAndStatusIsNotBlocked_ShouldThrowDomainException()
    {
        var attempts = LoginAttempt.Restore(
            failedAttempts: 5,
            lastFailedAt: DateTime.UtcNow.AddMinutes(-1),
            lockedUntil: DateTime.UtcNow.AddMinutes(10));

        Assert.Throws<DomainException>(() =>
            Password.Restore(Guid.NewGuid(), "hashed-password", attempts, PasswordStatus.Active));
    }

    /// <summary>
    /// Verifica se o registro da quinta falha bloqueia a senha.
    /// </summary>
    [Fact]
    public void RegisterLoginFailure_WhenThresholdIsReached_ShouldBlockPassword()
    {
        var password = Password.Create(Guid.NewGuid(), "hashed-password", PasswordStatus.Active);

        for (var i = 0; i < 5; i++)
            password = password.RegisterLoginFailure();

        Assert.Equal(PasswordStatus.Blocked, password.Status);
        Assert.True(password.IsLocked());
        Assert.NotNull(password.GetLockMessage());
    }

    /// <summary>
    /// Verifica se o reset das tentativas remove o bloqueio e restaura o status informado.
    /// </summary>
    [Fact]
    public void ResetLoginAttempts_WhenPasswordIsBlocked_ShouldResetAttemptsAndRestoreGivenStatus()
    {
        var attempts = LoginAttempt.Restore(
            failedAttempts: 5,
            lastFailedAt: DateTime.UtcNow.AddMinutes(-1),
            lockedUntil: DateTime.UtcNow.AddMinutes(10));

        var password = Password.Restore(
            Guid.NewGuid(),
            "hashed-password",
            attempts,
            PasswordStatus.Blocked);

        var updatedPassword = password.ResetLoginAttempts(PasswordStatus.FirstAccess);

        Assert.Equal(PasswordStatus.FirstAccess, updatedPassword.Status);
        Assert.False(updatedPassword.IsLocked());
        Assert.Equal(0, updatedPassword.LoginAttempt.FailedAttempts);
        Assert.Null(updatedPassword.LoginAttempt.LockedUntil);
    }

    /// <summary>
    /// Verifica se o reset das tentativas rejeita a manutenção do status bloqueado.
    /// </summary>
    [Fact]
    public void ResetLoginAttempts_WhenRestoredStatusIsBlocked_ShouldThrowDomainException()
    {
        var attempts = LoginAttempt.Restore(
            failedAttempts: 5,
            lastFailedAt: DateTime.UtcNow.AddMinutes(-1),
            lockedUntil: DateTime.UtcNow.AddMinutes(10));

        var password = Password.Restore(
            Guid.NewGuid(),
            "hashed-password",
            attempts,
            PasswordStatus.Blocked);

        Assert.Throws<DomainException>(() => password.ResetLoginAttempts(PasswordStatus.Blocked));
    }

    /// <summary>
    /// Verifica se a troca da senha preserva as invariantes e limpa o histórico de falhas.
    /// </summary>
    [Fact]
    public void Change_WhenCalledWithValidState_ShouldResetAttemptsAndApplyNewStatus()
    {
        var password = Password.Create(Guid.NewGuid(), "hashed-password", PasswordStatus.Active);

        for (var index = 0; index < 3; index++)
            password = password.RegisterLoginFailure();

        var updatedPassword = password.Change("new-hashed-password", PasswordStatus.Active);

        Assert.Equal("new-hashed-password", updatedPassword.Value);
        Assert.Equal(PasswordStatus.Active, updatedPassword.Status);
        Assert.Equal(0, updatedPassword.LoginAttempt.FailedAttempts);
        Assert.False(updatedPassword.IsLocked());
    }
}
