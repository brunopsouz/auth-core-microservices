namespace Shared.Observability;

/// <summary>
/// Representa os nomes de fontes específicos que um host disponibiliza para observabilidade.
/// </summary>
public sealed class ObservabilityServiceDescriptor
{
    /// <summary>
    /// Inicializa uma nova instância da classe <see cref="ObservabilityServiceDescriptor"/>.
    /// </summary>
    /// <param name="activitySourceNames">Nomes das fontes de atividades do host.</param>
    /// <param name="meterNames">Nomes dos medidores do host.</param>
    public ObservabilityServiceDescriptor(
        IEnumerable<string>? activitySourceNames = null,
        IEnumerable<string>? meterNames = null)
    {
        ActivitySourceNames = NormalizeNames(activitySourceNames);
        MeterNames = NormalizeNames(meterNames);
    }

    /// <summary>
    /// Representa os nomes das fontes de atividades a serem escutadas.
    /// </summary>
    public IReadOnlyCollection<string> ActivitySourceNames { get; }

    /// <summary>
    /// Representa os nomes dos medidores a serem escutados.
    /// </summary>
    public IReadOnlyCollection<string> MeterNames { get; }

    private static IReadOnlyCollection<string> NormalizeNames(IEnumerable<string>? names)
    {
        return (names ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
