using System.ComponentModel.DataAnnotations;

namespace AuthCore.Infrastructure.Configurations;

/// <summary>
/// Representa as configurações de encaminhamento por proxy reverso.
/// </summary>
internal sealed class ProxyForwardingOptions
{
    /// <summary>
    /// Nome da seção de configuração.
    /// </summary>
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// Endereços IP de proxies confiáveis.
    /// </summary>
    public string[] KnownProxies { get; init; } = [];

    /// <summary>
    /// Redes em CIDR confiáveis para headers encaminhados.
    /// </summary>
    public string[] KnownNetworks { get; init; } = [];

    /// <summary>
    /// Quantidade máxima de encaminhamentos aceitos.
    /// </summary>
    [Range(1, 32)]
    public int ForwardLimit { get; init; } = 2;
}
