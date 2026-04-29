using Conductor.Core.Domain.Ids;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal static class StronglyTypedIdValueConverters
{
    public static readonly ValueConverter<AlertId, Guid> AlertId = new(
        id => id.Value,
        value => new AlertId(value));

    public static readonly ValueConverter<AuditEventId, Guid> AuditEventId = new(
        id => id.Value,
        value => new AuditEventId(value));

    public static readonly ValueConverter<BackgroundOperationId, Guid> BackgroundOperationId = new(
        id => id.Value,
        value => new BackgroundOperationId(value));

    public static readonly ValueConverter<EventId, Guid> EventId = new(
        id => id.Value,
        value => new EventId(value));

    public static readonly ValueConverter<InstanceSnapshotId, Guid> InstanceSnapshotId = new(
        id => id.Value,
        value => new InstanceSnapshotId(value));

    public static readonly ValueConverter<ProjectId, Guid> ProjectId = new(
        id => id.Value,
        value => new ProjectId(value));

    public static readonly ValueConverter<ProjectId?, Guid?> NullableProjectId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new ProjectId(value.Value) : null);

    public static readonly ValueConverter<RepositoryId, Guid> RepositoryId = new(
        id => id.Value,
        value => new RepositoryId(value));

    public static readonly ValueConverter<RepositoryId?, Guid?> NullableRepositoryId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new RepositoryId(value.Value) : null);

    public static readonly ValueConverter<ReportId, Guid> ReportId = new(
        id => id.Value,
        value => new ReportId(value));

    public static readonly ValueConverter<RunId, Guid> RunId = new(
        id => id.Value,
        value => new RunId(value));

    public static readonly ValueConverter<RunId?, Guid?> NullableRunId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new RunId(value.Value) : null);

    public static readonly ValueConverter<RunAttemptId, Guid> RunAttemptId = new(
        id => id.Value,
        value => new RunAttemptId(value));

    public static readonly ValueConverter<SecretId, Guid> SecretId = new(
        id => id.Value,
        value => new SecretId(value));

    public static readonly ValueConverter<SecretId?, Guid?> NullableSecretId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new SecretId(value.Value) : null);

    public static readonly ValueConverter<SymphonyInstanceId, Guid> SymphonyInstanceId = new(
        id => id.Value,
        value => new SymphonyInstanceId(value));

    public static readonly ValueConverter<SymphonyInstanceId?, Guid?> NullableSymphonyInstanceId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new SymphonyInstanceId(value.Value) : null);

    public static readonly ValueConverter<TrackedIssueId, Guid> TrackedIssueId = new(
        id => id.Value,
        value => new TrackedIssueId(value));

    public static readonly ValueConverter<WorkflowProfileId, Guid> WorkflowProfileId = new(
        id => id.Value,
        value => new WorkflowProfileId(value));

    public static readonly ValueConverter<Uri, string> AbsoluteUri = new(
        uri => uri.ToString(),
        value => new Uri(value, UriKind.Absolute));

    public static readonly ValueConverter<Uri?, string?> NullableAbsoluteUri = new(
        uri => uri == null ? null : uri.ToString(),
        value => value == null ? null : new Uri(value, UriKind.Absolute));
}
