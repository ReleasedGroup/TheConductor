namespace Conductor.Core.Domain;

public enum CredentialInheritanceMode
{
    InheritDefault,
    SpecificSecret,
    None,
}

public enum ExecutionMode
{
    LocalProcess,
    Docker,
    AzureContainer,
}

public enum InstanceHealthStatus
{
    Unknown,
    Healthy,
    Warning,
    Critical,
    Offline,
}

public enum InstanceLifecycleStatus
{
    NotProvisioned,
    Provisioned,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Destroyed,
}

public enum ProjectStatus
{
    Active,
    Archived,
}

public enum RepositoryProvider
{
    GitHub,
}
