using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports.Aggregates;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Passports.Services;
using AuthCore.Domain.Users.Enums;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.Authentication.UseCases.ResendVerification;

/// <summary>
/// Representa caso de uso para reenviar a verificação de e-mail.
/// </summary>
public sealed class ResendVerificationUseCase : IResendVerificationUseCase
{
    private readonly IEmailVerificationRepository _emailVerificationRepository;
    private readonly IEmailVerificationNotificationOutboxFactory _emailVerificationNotificationOutboxFactory;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="emailVerificationRepository">Repositório de verificação de e-mail.</param>
    /// <param name="emailVerificationService">Serviço de verificação de e-mail.</param>
    /// <param name="emailVerificationNotificationOutboxFactory">Factory de mensagem de outbox.</param>
    /// <param name="outboxRepository">Repositório de outbox.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public ResendVerificationUseCase(
        IUserReadRepository userReadRepository,
        IEmailVerificationRepository emailVerificationRepository,
        IEmailVerificationService emailVerificationService,
        IEmailVerificationNotificationOutboxFactory emailVerificationNotificationOutboxFactory,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork)
    {
        _userReadRepository = userReadRepository;
        _emailVerificationRepository = emailVerificationRepository;
        _emailVerificationService = emailVerificationService;
        _emailVerificationNotificationOutboxFactory = emailVerificationNotificationOutboxFactory;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
    }

    #endregion

    /// <summary>
    /// Operação para reenviar a verificação de e-mail.
    /// </summary>
    /// <param name="command">Comando com o e-mail alvo.</param>
    public async Task Execute(ResendVerificationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedEmail = NormalizeEmail(command.Email);
        var user = await _userReadRepository.GetByEmailAsync(normalizedEmail);

        if (user is null || user.Status != UserStatus.PendingEmailVerification)
            return;

        var nowUtc = DateTime.UtcNow;
        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var existingVerification = await _emailVerificationRepository.GetByUserIdAsync(user.Id);

            if (existingVerification is not null
                && existingVerification.IsActiveAt(nowUtc)
                && existingVerification.IsInCooldownAt(nowUtc))
            {
                await _unitOfWork.CommitAsync();
                return;
            }

            var material = _emailVerificationService.Create();
            var verification = existingVerification is null
                ? EmailVerification.Issue(
                    user.Id,
                    user.Email.Value,
                    material.Hash,
                    _emailVerificationService.GetExpiresAtUtc(),
                    _emailVerificationService.GetMaxAttempts(),
                    _emailVerificationService.GetCooldownUntilUtc(),
                    nowUtc)
                : existingVerification.Reissue(
                    material.Hash,
                    _emailVerificationService.GetExpiresAtUtc(),
                    _emailVerificationService.GetMaxAttempts(),
                    _emailVerificationService.GetCooldownUntilUtc(),
                    nowUtc);
            var outboxMessage = _emailVerificationNotificationOutboxFactory.Create(
                verification,
                material.Code,
                nowUtc);

            if (existingVerification is null)
                await _emailVerificationRepository.AddAsync(verification);
            else
                await _emailVerificationRepository.UpdateAsync(verification);

            await _outboxRepository.AddAsync(outboxMessage);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    #region Helpers

    /// <summary>
    /// Operação para normalizar o e-mail informado.
    /// </summary>
    /// <param name="email">E-mail informado.</param>
    /// <returns>E-mail normalizado.</returns>
    private static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }

    #endregion
}
