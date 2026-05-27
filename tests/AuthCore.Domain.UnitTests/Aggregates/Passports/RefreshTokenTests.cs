using AuthCore.Domain.Common.Exceptions;
using AuthCore.Domain.Passports;

namespace AuthCore.Domain.UnitTests.Aggregates.Passports;

public sealed class RefreshTokenTests
{
    /// <summary>
    /// Verifica se a emissão inicial cria um refresh token ativo com família própria.
    /// </summary>
    [Fact]
    public void IssueInitial_WhenStateIsValid_ShouldCreateActiveRefreshToken()
    {
        var userId = Guid.NewGuid();
        var expiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);

        var refreshToken = RefreshToken.IssueInitial(userId, "  hash-inicial  ", expiresAtUtc);

        Assert.Equal(userId, refreshToken.UserId);
        Assert.NotEqual(Guid.Empty, refreshToken.Id);
        Assert.NotEqual(Guid.Empty, refreshToken.FamilyId);
        Assert.Null(refreshToken.ParentTokenId);
        Assert.Null(refreshToken.ReplacedByTokenId);
        Assert.Equal("hash-inicial", refreshToken.TokenHash);
        Assert.Equal(expiresAtUtc, refreshToken.ExpiresAtUtc);
        Assert.Null(refreshToken.ConsumedAtUtc);
        Assert.Null(refreshToken.RevokedAtUtc);
        Assert.Null(refreshToken.RevocationReason);
        Assert.True(refreshToken.IsActiveAt(expiresAtUtc.AddMinutes(-5)));
        Assert.True(refreshToken.IsReusable());
    }

    /// <summary>
    /// Verifica se a emissão inicial falha quando o usuário é inválido.
    /// </summary>
    [Fact]
    public void IssueInitial_WhenUserIdIsEmpty_ShouldThrowDomainException()
    {
        var expiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() =>
            RefreshToken.IssueInitial(Guid.Empty, "hash-inicial", expiresAtUtc));
    }

    /// <summary>
    /// Verifica se a emissão sucessora mantém a família e aponta para o token pai.
    /// </summary>
    [Fact]
    public void IssueReplacement_WhenStateIsValid_ShouldCreateTokenLinkedToFamily()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var parentTokenId = Guid.NewGuid();
        var expiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);

        var refreshToken = RefreshToken.IssueReplacement(
            userId,
            familyId,
            parentTokenId,
            "hash-sucessor",
            expiresAtUtc);

        Assert.Equal(userId, refreshToken.UserId);
        Assert.Equal(familyId, refreshToken.FamilyId);
        Assert.Equal(parentTokenId, refreshToken.ParentTokenId);
        Assert.Null(refreshToken.ReplacedByTokenId);
        Assert.Equal("hash-sucessor", refreshToken.TokenHash);
        Assert.True(refreshToken.IsActiveAt(expiresAtUtc.AddMinutes(-1)));
    }

    /// <summary>
    /// Verifica se o consumo marca o token como utilizado e liga o sucessor.
    /// </summary>
    [Fact]
    public void Consume_WhenTokenIsActive_ShouldMarkAsConsumedAndLinkReplacement()
    {
        var expiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var consumedAtUtc = expiresAtUtc.AddMinutes(-10);
        var replacementTokenId = Guid.NewGuid();
        var refreshToken = RefreshToken.IssueInitial(Guid.NewGuid(), "hash-inicial", expiresAtUtc);

        var consumedToken = refreshToken.Consume(replacementTokenId, consumedAtUtc);

        Assert.Equal(refreshToken.Id, consumedToken.Id);
        Assert.Equal(replacementTokenId, consumedToken.ReplacedByTokenId);
        Assert.Equal(consumedAtUtc, consumedToken.ConsumedAtUtc);
        Assert.False(consumedToken.IsActiveAt(consumedAtUtc.AddMinutes(1)));
        Assert.False(consumedToken.IsReusable());
    }

    /// <summary>
    /// Verifica se o consumo falha quando o token já expirou.
    /// </summary>
    [Fact]
    public void Consume_WhenTokenIsExpired_ShouldThrowDomainException()
    {
        var expiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var refreshToken = RefreshToken.IssueInitial(Guid.NewGuid(), "hash-inicial", expiresAtUtc);

        Assert.Throws<DomainException>(() =>
            refreshToken.Consume(Guid.NewGuid(), expiresAtUtc.AddMinutes(1)));
    }

    /// <summary>
    /// Verifica se o consumo falha quando o token já foi utilizado.
    /// </summary>
    [Fact]
    public void Consume_WhenTokenIsAlreadyConsumed_ShouldThrowDomainException()
    {
        var expiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var consumedAtUtc = expiresAtUtc.AddMinutes(-10);
        var refreshToken = RefreshToken.IssueInitial(Guid.NewGuid(), "hash-inicial", expiresAtUtc)
            .Consume(Guid.NewGuid(), consumedAtUtc);

        Assert.Throws<DomainException>(() =>
            refreshToken.Consume(Guid.NewGuid(), consumedAtUtc.AddMinutes(1)));
    }

    /// <summary>
    /// Verifica se a revogação registra o motivo operacional e inativa o token.
    /// </summary>
    [Fact]
    public void Revoke_WhenTokenIsActive_ShouldMarkAsRevoked()
    {
        var expiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var revokedAtUtc = expiresAtUtc.AddMinutes(-5);
        var refreshToken = RefreshToken.IssueInitial(Guid.NewGuid(), "hash-inicial", expiresAtUtc);

        var revokedToken = refreshToken.Revoke(" logout ", revokedAtUtc);

        Assert.Equal(revokedAtUtc, revokedToken.RevokedAtUtc);
        Assert.Equal("logout", revokedToken.RevocationReason);
        Assert.False(revokedToken.IsActiveAt(revokedAtUtc.AddMinutes(1)));
        Assert.True(revokedToken.IsReusable());
    }

    /// <summary>
    /// Verifica se a revogação repetida mantém o estado atual do token.
    /// </summary>
    [Fact]
    public void Revoke_WhenTokenIsAlreadyRevoked_ShouldKeepCurrentState()
    {
        var expiresAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        var revokedToken = RefreshToken.IssueInitial(Guid.NewGuid(), "hash-inicial", expiresAtUtc)
            .Revoke("logout", expiresAtUtc.AddMinutes(-5));

        var updatedToken = revokedToken.Revoke("reuse-detected", expiresAtUtc.AddMinutes(-4));

        Assert.Same(revokedToken, updatedToken);
    }

    /// <summary>
    /// Verifica se a restauração falha quando um token consumido não possui sucessor.
    /// </summary>
    [Fact]
    public void Restore_WhenConsumedTokenHasNoReplacement_ShouldThrowDomainException()
    {
        var now = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() => RefreshToken.Restore(
            Guid.NewGuid(),
            now.AddHours(-2),
            now.AddHours(-1),
            true,
            Guid.NewGuid(),
            Guid.NewGuid(),
            parentTokenId: null,
            replacedByTokenId: null,
            tokenHash: "hash-restaurado",
            expiresAtUtc: now.AddHours(4),
            consumedAtUtc: now,
            revokedAtUtc: null,
            revocationReason: null));
    }

    /// <summary>
    /// Verifica se a restauração falha quando existe revogação sem motivo.
    /// </summary>
    [Fact]
    public void Restore_WhenRevokedWithoutReason_ShouldThrowDomainException()
    {
        var now = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<DomainException>(() => RefreshToken.Restore(
            Guid.NewGuid(),
            now.AddHours(-2),
            now.AddHours(-1),
            true,
            Guid.NewGuid(),
            Guid.NewGuid(),
            parentTokenId: null,
            replacedByTokenId: null,
            tokenHash: "hash-restaurado",
            expiresAtUtc: now.AddHours(4),
            consumedAtUtc: null,
            revokedAtUtc: now,
            revocationReason: null));
    }
}
