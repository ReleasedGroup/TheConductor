namespace Conductor.Core.Domain.Ids;

public readonly record struct BackgroundOperationId
{
    public BackgroundOperationId(Guid value)
    {
        Value = EntityIdGuards.NotEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static BackgroundOperationId New() => new(Guid.NewGuid());

    public static BackgroundOperationId Parse(string value) => new(EntityIdGuards.Parse(value));

    public static bool TryParse(string? value, out BackgroundOperationId id)
    {
        if (EntityIdGuards.TryParse(value, out Guid parsed))
        {
            id = new BackgroundOperationId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProjectId
{
    public ProjectId(Guid value)
    {
        Value = EntityIdGuards.NotEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProjectId New() => new(Guid.NewGuid());

    public static ProjectId Parse(string value) => new(EntityIdGuards.Parse(value));

    public static bool TryParse(string? value, out ProjectId id)
    {
        if (EntityIdGuards.TryParse(value, out Guid parsed))
        {
            id = new ProjectId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ReportId
{
    public ReportId(Guid value)
    {
        Value = EntityIdGuards.NotEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static ReportId New() => new(Guid.NewGuid());

    public static ReportId Parse(string value) => new(EntityIdGuards.Parse(value));

    public static bool TryParse(string? value, out ReportId id)
    {
        if (EntityIdGuards.TryParse(value, out Guid parsed))
        {
            id = new ReportId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct RepositoryId
{
    public RepositoryId(Guid value)
    {
        Value = EntityIdGuards.NotEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static RepositoryId New() => new(Guid.NewGuid());

    public static RepositoryId Parse(string value) => new(EntityIdGuards.Parse(value));

    public static bool TryParse(string? value, out RepositoryId id)
    {
        if (EntityIdGuards.TryParse(value, out Guid parsed))
        {
            id = new RepositoryId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct RunId
{
    public RunId(Guid value)
    {
        Value = EntityIdGuards.NotEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static RunId New() => new(Guid.NewGuid());

    public static RunId Parse(string value) => new(EntityIdGuards.Parse(value));

    public static bool TryParse(string? value, out RunId id)
    {
        if (EntityIdGuards.TryParse(value, out Guid parsed))
        {
            id = new RunId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct SecretId
{
    public SecretId(Guid value)
    {
        Value = EntityIdGuards.NotEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static SecretId New() => new(Guid.NewGuid());

    public static SecretId Parse(string value) => new(EntityIdGuards.Parse(value));

    public static bool TryParse(string? value, out SecretId id)
    {
        if (EntityIdGuards.TryParse(value, out Guid parsed))
        {
            id = new SecretId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct SymphonyInstanceId
{
    public SymphonyInstanceId(Guid value)
    {
        Value = EntityIdGuards.NotEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static SymphonyInstanceId New() => new(Guid.NewGuid());

    public static SymphonyInstanceId Parse(string value) => new(EntityIdGuards.Parse(value));

    public static bool TryParse(string? value, out SymphonyInstanceId id)
    {
        if (EntityIdGuards.TryParse(value, out Guid parsed))
        {
            id = new SymphonyInstanceId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

public readonly record struct WorkflowProfileId
{
    public WorkflowProfileId(Guid value)
    {
        Value = EntityIdGuards.NotEmpty(value, nameof(value));
    }

    public Guid Value { get; }

    public static WorkflowProfileId New() => new(Guid.NewGuid());

    public static WorkflowProfileId Parse(string value) => new(EntityIdGuards.Parse(value));

    public static bool TryParse(string? value, out WorkflowProfileId id)
    {
        if (EntityIdGuards.TryParse(value, out Guid parsed))
        {
            id = new WorkflowProfileId(parsed);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("D");
}

file static class EntityIdGuards
{
    public static Guid NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An entity identifier cannot be empty.", parameterName);
        }

        return value;
    }

    public static Guid Parse(string value)
    {
        Guid parsed = Guid.Parse(value);
        return NotEmpty(parsed, nameof(value));
    }

    public static bool TryParse(string? value, out Guid parsed)
    {
        if (Guid.TryParse(value, out parsed) && parsed != Guid.Empty)
        {
            return true;
        }

        parsed = default;
        return false;
    }
}
