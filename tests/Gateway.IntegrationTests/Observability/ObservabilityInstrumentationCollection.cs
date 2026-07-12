namespace Gateway.IntegrationTests.Observability;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ObservabilityInstrumentationCollection
{
    public const string Name = "ObservabilityInstrumentation";
}
