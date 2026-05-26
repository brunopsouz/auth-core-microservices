using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.DomainEvents;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Cryptography;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.Users.UseCases.RegisterUser;

/// <summary>
/// Representa caso de uso para registrar um usuário.
/// </summary>
internal sealed class RegisterUserUseCase : IRegisterUserUseCase
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
    /// <param name="userRepository">Repositório de escrita de usuário.</param>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="passwordRepository">Repositório de senha.</param>
    /// <param name="emailVerificationRepository">Repositório de verificação de e-mail.</param>
    /// <param name="emailVerificationService">Serviço de verificação de e-mail.</param>
    /// <param name="emailVerificationRequestedPublisher">Publisher do evento de verificação de e-mail.</param>
    /// <param name="passwordEncripter">Serviço de criptografia de senha.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public RegisterUserUseCase(
        IUserRepository userRepository,
        IUserReadRepository userReadRepository,
        IPasswordRepository passwordRepository,
        IEmailVerificationRepository emailVerificationRepository,
        IEmailVerificationService emailVerificationService,
        IEmailVerificationRequestedPublisher emailVerificationRequestedPublisher,
        IPasswordEncripter passwordEncripter,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _userReadRepository = userReadRepository;
        _passwordRepository = passwordRepository;
        _emailVerificationRepository = emailVerificationRepository;
        _emailVerificationService = emailVerificationService;
        _emailVerificationRequestedPublisher = emailVerificationRequestedPublisher;
        _passwordEncripter = passwordEncripter;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Operação para registrar um usuário.
    /// </summary>
    /// <param name="command">Comando com os dados do registro.</param>
    /// <returns>Resultado do usuário registrado.</returns>
    public async Task<RegisterUserResult> Execute(RegisterUserCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        Password.ValidateWithConfirmation(command.Password, command.ConfirmPassword);

        var existingUser = await _userReadRepository.GetByEmailAsync(command.Email);

        if (existingUser is not null)
            throw new ConflictException("Já existe um usuário cadastrado com o e-mail informado.");

        var user = User.Register(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Contact,
            Role.User);
        var emailVerificationMaterial = _emailVerificationService.Create();
        var passwordHash = _passwordEncripter.Encrypt(command.Password);
        var password = Password.Create(user.Id, passwordHash, PasswordStatus.FirstAccess);
        var nowUtc = DateTime.UtcNow;
        var emailVerification = EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            emailVerificationMaterial.Hash,
            _emailVerificationService.GetExpiresAtUtc(),
            _emailVerificationService.GetMaxAttempts(),
            _emailVerificationService.GetCooldownUntilUtc(),
            nowUtc);
        var emailVerificationRequested = new EmailVerificationRequested
        {
            UserId = user.Id,
            Email = user.Email.Value,
            Code = emailVerificationMaterial.Code,
            RequestedAtUtc = nowUtc
        };
        emailVerificationRequested.Validate();

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _userRepository.AddAsync(user);
            await _passwordRepository.AddAsync(password);
            await _emailVerificationRepository.AddAsync(emailVerification);
            await _emailVerificationRequestedPublisher.PublishAsync(emailVerificationRequested);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        return new RegisterUserResult
        {
            UserIdentifier = user.UserIdentifier,
            FullName = user.FullName,
            Email = user.Email.Value
        };
    }
}
