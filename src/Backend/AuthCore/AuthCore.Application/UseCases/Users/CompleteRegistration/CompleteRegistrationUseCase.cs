using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.UseCases.Users.CompleteRegistration;

/// <summary>
/// Representa caso de uso para concluir o registro de usuário.
/// </summary>
internal sealed class CompleteRegistrationUseCase : ICompleteRegistrationUseCase
{
    /// <summary>
    /// Campo que armazena email verification repository.
    /// </summary>
    private readonly IEmailVerificationRepository _emailVerificationRepository;
    /// <summary>
    /// Campo que armazena email verification service.
    /// </summary>
    private readonly IEmailVerificationService _emailVerificationService;
    /// <summary>
    /// Campo que armazena password encripter.
    /// </summary>
    private readonly IPasswordEncripter _passwordEncripter;
    /// <summary>
    /// Campo que armazena password repository.
    /// </summary>
    private readonly IPasswordRepository _passwordRepository;
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
    /// <param name="emailVerificationRepository">repositório de verificação de e-mail.</param>
    /// <param name="emailVerificationService">Serviço de verificação de e-mail.</param>
    /// <param name="passwordRepository">repositório de senha.</param>
    /// <param name="passwordEncripter">Serviço de criptografia de senha.</param>
    /// <param name="userReadRepository">repositório de leitura de usuário.</param>
    /// <param name="userRepository">repositório de escrita de usuário.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public CompleteRegistrationUseCase(
        IEmailVerificationRepository emailVerificationRepository,
        IEmailVerificationService emailVerificationService,
        IPasswordRepository passwordRepository,
        IPasswordEncripter passwordEncripter,
        IUserReadRepository userReadRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _emailVerificationRepository = emailVerificationRepository;
        _emailVerificationService = emailVerificationService;
        _passwordRepository = passwordRepository;
        _passwordEncripter = passwordEncripter;
        _userReadRepository = userReadRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Operação para concluir o registro de usuário.
    /// </summary>
    /// <param name="command">Comando com e-mail, código OTP e senha.</param>
    public async Task Execute(CompleteRegistrationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        Password.ValidateWithConfirmation(command.Password, command.ConfirmPassword);

        var normalizedEmail = NormalizeEmail(command.Email);
        await _unitOfWork.BeginTransactionAsync();
        var transactionCompleted = false;

        try
        {
            var emailVerification = await _emailVerificationRepository.GetPendingByEmailAsync(normalizedEmail)
                ?? throw CreateInvalidVerificationException();
            var user = await _userReadRepository.GetByEmailAsync(normalizedEmail)
                ?? throw CreateInvalidVerificationException();

            if (emailVerification.UserId != user.Id)
                throw CreateInvalidVerificationException();

            if (user.Status != UserStatus.PendingEmailVerification)
                throw CreateInvalidVerificationException();

            var existingPassword = await _passwordRepository.GetByUserIdAsync(user.Id);

            if (existingPassword is not null)
                throw new ConflictException("O cadastro informado já possui senha definida.");

            var validatedVerification = ValidateCode(emailVerification, command.Code);
            await _emailVerificationRepository.UpdateAsync(validatedVerification);

            if (!validatedVerification.ConsumedAtUtc.HasValue)
            {
                await _unitOfWork.CommitAsync();
                transactionCompleted = true;
                throw CreateInvalidVerificationException();
            }

            user.VerifyEmail(validatedVerification.ConsumedAtUtc.Value);

            var passwordHash = _passwordEncripter.Encrypt(command.Password);
            var password = Password.Create(user.Id, passwordHash, PasswordStatus.Active);

            await _userRepository.UpdateAsync(user);
            await _passwordRepository.AddAsync(password);
            await _unitOfWork.CommitAsync();
            transactionCompleted = true;
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
    /// <param name="emailVerification">verificação pendente do usuário.</param>
    /// <param name="code">código informado.</param>
    /// <returns>verificação atualizada após a validação.</returns>
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
    /// <returns>Exceção de aplicação padronizada.</returns>
    private static InvalidEmailVerificationException CreateInvalidVerificationException()
    {
        return new InvalidEmailVerificationException();
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
