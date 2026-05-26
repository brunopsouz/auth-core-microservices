using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.Authentication.UseCases.VerifyEmail;

/// <summary>
/// Representa caso de uso para verificar o e-mail do usuário.
/// </summary>
internal sealed class VerifyEmailUseCase : IVerifyEmailUseCase
{
    private const string INVALID_VERIFICATION_MESSAGE = "Não foi possível validar o código de verificação informado.";

    /// <summary>
    /// Campo que armazena email verification repository.
    /// </summary>
    private readonly IEmailVerificationRepository _emailVerificationRepository;
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
    /// Campo que armazena user repository.
    /// </summary>
    private readonly IUserRepository _userRepository;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="emailVerificationRepository">Repositório de verificação de e-mail.</param>
    /// <param name="emailVerificationService">Serviço de verificação de e-mail.</param>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="userRepository">Repositório de escrita de usuário.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public VerifyEmailUseCase(
        IEmailVerificationRepository emailVerificationRepository,
        IEmailVerificationService emailVerificationService,
        IUserReadRepository userReadRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _emailVerificationRepository = emailVerificationRepository;
        _emailVerificationService = emailVerificationService;
        _userReadRepository = userReadRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }


    /// <summary>
    /// Operação para verificar o e-mail do usuário.
    /// </summary>
    /// <param name="command">Comando com o e-mail e código OTP.</param>
    public async Task Execute(VerifyEmailCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedEmail = NormalizeEmail(command.Email);
        await _unitOfWork.BeginTransactionAsync();
        var transactionCompleted = false;

        try
        {
            var emailVerification = await _emailVerificationRepository.GetPendingByEmailAsync(normalizedEmail)
                ?? throw CreateInvalidVerificationException();
            var user = await _userReadRepository.GetByEmailAsync(normalizedEmail)
                ?? throw CreateInvalidVerificationException();
            var validatedVerification = ValidateCode(emailVerification, command.Code);

            await _emailVerificationRepository.UpdateAsync(validatedVerification);

            if (validatedVerification.ConsumedAtUtc.HasValue)
            {
                user.VerifyEmail(validatedVerification.ConsumedAtUtc.Value);
                await _userRepository.UpdateAsync(user);
            }

            await _unitOfWork.CommitAsync();
            transactionCompleted = true;

            if (!validatedVerification.ConsumedAtUtc.HasValue)
                throw CreateInvalidVerificationException();
        }
        catch
        {
            if (!transactionCompleted)
                await _unitOfWork.RollbackAsync();

            throw;
        }
    }


    /// <summary>
    /// Operação para validar o código OTP e normalizar falhas previsíveis.
    /// </summary>
    /// <param name="emailVerification">Verificação pendente do usuário.</param>
    /// <param name="code">Código informado.</param>
    /// <returns>Verificação atualizada após a validação.</returns>
    private EmailVerification ValidateCode(EmailVerification emailVerification, string code)
    {
        try
        {
            return emailVerification.ValidateCode(
                _emailVerificationService.ComputeHash(code),
                DateTime.UtcNow);
        }
        catch (DomainException)
        {
            throw CreateInvalidVerificationException();
        }
    }

    /// <summary>
    /// Operação para criar a falha genérica de validação do código.
    /// </summary>
    /// <returns>Exceção de domínio padronizada.</returns>
    private static DomainException CreateInvalidVerificationException()
    {
        return new DomainException(INVALID_VERIFICATION_MESSAGE);
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
