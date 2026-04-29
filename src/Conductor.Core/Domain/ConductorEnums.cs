namespace Conductor.Core.Domain;

public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
}

public enum AlertStatus
{
    Active,
    Acknowledged,
    Resolved,
}

public enum AuditEventOutcome
{
    Succeeded,
    Failed,
    Denied,
}

public enum BackgroundOperationStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled,
}

public enum CredentialInheritanceMode
{
    InheritDefault,
    SpecificSecret,
    None,
}

public enum EventSeverity
{
    Information,
    Warning,
    Error,
    Critical,
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

public enum RepositoryOrchestrationStatus
{
    Eligible,
    Ineligible,
}

public enum RepositoryProvider
{
    GitHub,
}

public enum RepositoryVisibility
{
    Public,
    Private,
    Internal,
}

public enum ReportType
{
    DailyDeliveryBrief,
    WeeklySoftwareFactory,
    Project,
    EngineeringReliability,
}

public enum RunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled,
    TimedOut,
}

public enum SymphonyIssueStatus
{
    Unknown,
    Queued,
    Running,
    Succeeded,
    Failed,
    Blocked,
    NoSession,
}

public enum TrackedIssueState
{
    Open,
    Closed,
}
