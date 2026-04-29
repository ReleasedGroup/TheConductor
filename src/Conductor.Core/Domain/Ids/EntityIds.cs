namespace Conductor.Core.Domain.Ids;

public readonly record struct BackgroundOperationId(Guid Value)
{
    public static BackgroundOperationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ReportId(Guid Value)
{
    public static ReportId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct RepositoryId(Guid Value)
{
    public static RepositoryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct RunId(Guid Value)
{
    public static RunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct SecretId(Guid Value)
{
    public static SecretId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct SymphonyInstanceId(Guid Value)
{
    public static SymphonyInstanceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct WorkflowProfileId(Guid Value)
{
    public static WorkflowProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
