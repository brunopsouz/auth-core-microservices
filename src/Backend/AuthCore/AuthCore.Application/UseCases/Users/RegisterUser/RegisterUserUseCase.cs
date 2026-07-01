using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Common.Enums;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Users;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.UseCases.Users.RegisterUser;

/// <summary>
/// Representa caso de uso para iniciar o registro de um usuário.
/// </summary>
internal sealed class RegisterUserUseCase : IRegisterUserUseCase
{
    /// <summary>
    /// Campo que armazena email verification repository.
    /// </summary>
    private readonly IEmailVerificationRepository _emailVerificationRepository;
    /// <summary>
    /// Campo que armazena email verification notification outbox factory.
    /// </summary>
    private readonly IEmailVerificationNotificationOutboxFactory _emailVerificationNotificationOutboxFactory;
    /// <summary>
    /// Campo que armazena email verification service.
    /// </summary>
    private readonly IEmailVerificationService _emailVerificationService;
    /// <summary>
    /// Campo que armazena outbox repository.
    /// </summary>
    private readonly IOutboxRepository _outboxRepository;
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
    /// <param name="userRepository">repositório de escrita de usuário.</param>
    /// <param name="userReadRepository">repositório de leitura de usuário.</param>
    /// <param name="emailVerificationRepository">repositório de verificação de e-mail.</param>
    /// <param name="emailVerificationService">Serviço de verificação de e-mail.</param>
    /// <param name="emailVerificationNotificationOutboxFactory">Factory de mensagem de outbox de verificação de e-mail.</param>
    /// <param name="outboxRepository">repositório da outbox.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public RegisterUserUseCase(
        IUserRepository userRepository,
        IUserReadRepository userReadRepository,
        IEmailVerificationRepository emailVerificationRepository,
        IEmailVerificationService emailVerificationService,
        IEmailVerificationNotificationOutboxFactory emailVerificationNotificationOutboxFactory,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _userReadRepository = userReadRepository;
        _emailVerificationRepository = emailVerificationRepository;
        _emailVerificationService = emailVerificationService;
        _emailVerificationNotificationOutboxFactory = emailVerificationNotificationOutboxFactory;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Operação para iniciar o registro de um usuário.
    /// </summary>
    /// <param name="command">Comando com os dados iniciais do registro.</param>
    /// <returns>Resultado do usuário registrado.</returns>
    public async Task<RegisterUserResult> Execute(RegisterUserCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

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
        var nowUtc = DateTime.UtcNow;
        var emailVerification = EmailVerification.Issue(
            user.Id,
            user.Email.Value,
            emailVerificationMaterial.Hash,
            _emailVerificationService.GetExpiresAtUtc(),
            _emailVerificationService.GetMaxAttempts(),
            _emailVerificationService.GetCooldownUntilUtc(),
            nowUtc);
        var outboxMessage = _emailVerificationNotificationOutboxFactory.Create(
            emailVerification,
            emailVerificationMaterial.Code,
            nowUtc);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _userRepository.AddAsync(user);
            await _emailVerificationRepository.AddAsync(emailVerification);
            await _outboxRepository.AddAsync(outboxMessage);
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
