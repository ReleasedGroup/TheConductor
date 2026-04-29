namespace Conductor.Host.Workers;

public sealed class InstanceCollectorWorkerOptions
{
    public const string SectionName = "InstanceCollector";

    public bool Enabled { get; set; } = true;

    public TimeSpan LoopDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan HealthInterval { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan StateInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RuntimeInterval { get; set; } = TimeSpan.FromMinutes(2);
}
