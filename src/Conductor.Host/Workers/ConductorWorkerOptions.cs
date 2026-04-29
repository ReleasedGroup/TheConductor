namespace Conductor.Host.Workers;

public sealed class ConductorWorkerOptions
{
    public const string SectionName = "Conductor";

    public int DefaultHealthPollSeconds { get; init; } = 10;

    public TimeSpan HealthPollInterval =>
        TimeSpan.FromSeconds(Math.Max(1, DefaultHealthPollSeconds));
}
