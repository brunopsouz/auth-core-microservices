using AuthCore.Application.Authentication.Models;
using AuthCore.Domain.Common.Repositories;
using AuthCore.Domain.Passports.Aggregates;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Tokens.Services;
using AuthCore.Domain.Users.Aggregates;
using AuthCore.Domain.Users.Repositories;

namespace AuthCore.Application.Authentication.UseCases.RefreshSession;

/// <summary>
/// Representa caso de uso para renovar uma autenticação do modo token.
/// </summary>
internal sealed class RefreshSessionUseCase : IRefreshSessionUseCase
{
    private const string INVALID_SESSION_MESSAGE = "A sessão informada é inválida ou expirou.";
    private const string REUSE_DETECTED_REASON = "reuse-detected";

    private readonly IAccessTokenGenerator _accessTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserReadRepository _userReadRepository;

    #region Constructors

    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="refreshTokenRepository">Repositório de refresh token.</param>
    /// <param name="refreshTokenService">Serviço de refresh token.</param>
    /// <param name="accessTokenGenerator">Gerador de access token.</param>
    /// <param name="userReadRepository">Repositório de leitura de usuário.</param>
    /// <param name="unitOfWork">Unidade de trabalho transacional.</param>
    public RefreshSessionUseCase(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService,
        IAccessTokenGenerator accessTokenGenerator,
        IUserReadRepository userReadRepository,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _accessTokenGenerator = accessTokenGenerator;
        _userReadRepository = userReadRepository;
        _unitOfWork = unitOfWork;
    }

    #endregion

    /// <summary>
    /// Operação para renovar uma autenticação do modo token.
    /// </summary>
    /// <param name="command">Comando com o refresh token informado.</param>
    /// <returns>Resultado da sessão renovada.</returns>
    public async Task<AuthenticatedSessionResult> Execute(RefreshSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            throw CreateInvalidSessionException();

        var tokenHash = _refreshTokenService.ComputeHash(command.RefreshToken);
        var currentRefreshToken = await _refreshTokenRepository.GetByHashAsync(tokenHash);

        if (currentRefreshToken is null)
            throw CreateInvalidSessionException();

        if (currentRefreshToken.ConsumedAtUtc.HasValue)
        {
            await RevokeFamilyAsync(currentRefreshToken.FamilyId);
            throw CreateInvalidSessionException();
        }

        var nowUtc = DateTime.UtcNow;

        if (!currentRefreshToken.IsActiveAt(nowUtc))
            throw CreateInvalidSessionException();

        var user = await _userReadRepository.GetByIdAsync(currentRefreshToken.UserId);

        if (user is null || !user.CanSignIn)
            throw CreateInvalidSessionException();

        return await RotateSessionAsync(currentRefreshToken, user, nowUtc);
    }

    #region Helpers

    /// <summary>
    /// Operação para rotacionar a autenticação do modo token.
    /// </summary>
    /// <param name="currentRefreshToken">Refresh token atual.</param>
    /// <param name="user">Usuário da sessão.</param>
    /// <param name="nowUtc">Instante atual em UTC.</param>
    /// <returns>Resultado da sessão renovada.</returns>
    private async Task<AuthenticatedSessionResult> RotateSessionAsync(
        RefreshToken currentRefreshToken,
        User user,
        DateTime nowUtc)
    {
        var accessToken = _accessTokenGenerator.Generate(user);
        var refreshTokenMaterial = _refreshTokenService.Create();
        var refreshTokenExpiresAtUtc = _refreshTokenService.GetExpiresAtUtc();
        var replacementRefreshToken = RefreshToken.IssueReplacement(
            user.Id,
            currentRefreshToken.FamilyId,
            currentRefreshToken.Id,
            refreshTokenMaterial.Hash,
            refreshTokenExpiresAtUtc);
        var consumedRefreshToken = currentRefreshToken.Consume(replacementRefreshToken.Id, nowUtc);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            await _refreshTokenRepository.UpdateAsync(consumedRefreshToken);
            await _refreshTokenRepository.AddAsync(replacementRefreshToken);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        return new AuthenticatedSessionResult
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = refreshTokenMaterial.Token,
            RefreshTokenExpiresAtUtc = replacementRefreshToken.ExpiresAtUtc
        };
    }

    /// <summary>
    /// Operação para revogar a família da sessão quando há indício de reuse.
    /// </summary>
    /// <param name="familyId">Identificador da família de rotação.</param>
    private async Task RevokeFamilyAsync(Guid familyId)
    {
        var revokedAtUtc = DateTime.UtcNow;
        await _refreshTokenRepository.RevokeFamilyAsync(familyId, revokedAtUtc, REUSE_DETECTED_REASON);
    }

    /// <summary>
    /// Operação para criar a falha genérica de renovação do modo token.
    /// </summary>
    /// <returns>Exceção de acesso não autorizado.</returns>
    private static UnauthorizedAccessException CreateInvalidSessionException()
    {
        return new UnauthorizedAccessException(INVALID_SESSION_MESSAGE);
    }

    #endregion
}
