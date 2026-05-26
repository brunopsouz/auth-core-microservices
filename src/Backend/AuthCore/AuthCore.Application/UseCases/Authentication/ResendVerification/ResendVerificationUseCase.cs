using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.UseCases.Authentication.ResendVerification;

/// <summary>
/// Representa caso de uso para reenviar a verificação de e-mail.
/// </summary>
internal sealed class ResendVerificationUseCase : IResendVerificationUseCase
{
    /// <summary>
    /// Campo que armazena email verification repository.
    /// </summary>
    private readonly IEmailVerificationRepository _emailVerificationRepository;
    /// <summary>
    /// Campo que armazena email verification requested publisher.
    /// </summary>
    private readonly IEmailVerificationRequestedPublisher _emailVerificationRequestedPublisher;
    /// <summary>
    /// Campo que armazena email verification service.
    /// </summary>
    private readonly IEmailVerificationService _emailVerificationService;
    /// <summary>
    /// Campo que armazena unit of work.
    /// </summary>
    private readonly IUnitOfWork _unitOfWork;
    /// <summary>
    /// Campo que armazena user read repository.
    /// </summary>
    private readonly IUserReadRepository _userReadRepository;

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="emailVerificationRepository">Repositório de verificação de e-mail.</param>
    /// <param name="emailVerificationService">Serviço de verificação de e-mail.</param>
    /// <param name="emailVerificationRequestedPublisher">Publisher do evento de verificação de e-mail.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public ResendVerificationUseCase(
        IUserReadRepository userReadRepository,
        IEmailVerificationRepository emailVerificationRepository,
        IEmailVerificationService emailVerificationService,
        IEmailVerificationRequestedPublisher emailVerificationRequestedPublisher,
        IUnitOfWork unitOfWork)
    {
        _userReadRepository = userReadRepository;
        _emailVerificationRepository = emailVerificationRepository;
        _emailVerificationService = emailVerificationService;
        _emailVerificationRequestedPublisher = emailVerificationRequestedPublisher;
        _unitOfWork = unitOfWork;
    }

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
            var emailVerificationRequested = new EmailVerificationRequested
            {
                UserId = user.Id,
                Email = user.Email.Value,
                Code = material.Code,
                RequestedAtUtc = nowUtc
            };
            emailVerificationRequested.Validate();

            if (existingVerification is null)
                await _emailVerificationRepository.AddAsync(verification);
            else
                await _emailVerificationRepository.UpdateAsync(verification);

            await _emailVerificationRequestedPublisher.PublishAsync(emailVerificationRequested);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

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

}
