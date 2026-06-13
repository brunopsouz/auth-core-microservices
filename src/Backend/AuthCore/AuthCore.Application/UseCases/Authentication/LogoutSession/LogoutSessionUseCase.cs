using AuthCore.Application.Common.Exceptions;
using AuthCore.Domain.Passports.Repositories;
using AuthCore.Domain.Security.Tokens.Services;

namespace AuthCore.Application.UseCases.Authentication.LogoutSession;

/// <summary>
/// Representa caso de uso para encerrar uma autenticação do modo token.
/// </summary>
internal sealed class LogoutSessionUseCase : ILogoutSessionUseCase
{
    private const string LOGOUT_REASON = "logout";

    /// <summary>
    /// Campo que armazena refresh token repository.
    /// </summary>
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    /// <summary>
    /// Campo que armazena refresh token service.
    /// </summary>
    private readonly IRefreshTokenService _refreshTokenService;


    /// <summary>
    /// Operação para criar instância da classe.
    /// </summary>
    /// <param name="refreshTokenRepository">Repositório de refresh token.</param>
    /// <param name="refreshTokenService">Serviço de refresh token.</param>
    public LogoutSessionUseCase(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
    }


    /// <summary>
    /// Operação para encerrar uma autenticação do modo token.
    /// </summary>
    /// <param name="command">Comando com o refresh token informado.</param>
    public async Task Execute(LogoutSessionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            throw new ValidationException("O refresh token é obrigatório.");

        var tokenHash = _refreshTokenService.ComputeHash(command.RefreshToken);
        var refreshToken = await _refreshTokenRepository.GetByHashAsync(tokenHash);

        if (refreshToken is null || refreshToken.RevokedAtUtc.HasValue)
            return;

        if (refreshToken.ConsumedAtUtc.HasValue)
        {
            await RevokeFamilyAsync(refreshToken.FamilyId);
            return;
        }

        var nowUtc = DateTime.UtcNow;

        if (!refreshToken.IsActiveAt(nowUtc))
            return;

        var revokedRefreshToken = refreshToken.Revoke(LOGOUT_REASON, nowUtc);
        await _refreshTokenRepository.UpdateAsync(revokedRefreshToken);
    }


    /// <summary>
    /// Operação para revogar a família da autenticação encerrada.
    /// </summary>
    /// <param name="familyId">Identificador da família de rotação.</param>
    private async Task RevokeFamilyAsync(Guid familyId)
    {
        var revokedAtUtc = DateTime.UtcNow;
        await _refreshTokenRepository.RevokeFamilyAsync(familyId, revokedAtUtc, LOGOUT_REASON);
    }

}
