using Conductor.Core.Domain.Ids;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Conductor.Infrastructure.Persistence.Sqlite.Configurations;

internal static class StronglyTypedIdValueConverters
{
    public static readonly ValueConverter<ProjectId, Guid> ProjectId = new(
        id => id.Value,
        value => new ProjectId(value));

    public static readonly ValueConverter<ProjectId?, Guid?> NullableProjectId = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new ProjectId(value.Value) : null);

    public static readonly ValueConverter<RepositoryId, Guid> RepositoryId = new(
        id => id.Value,
        value => new RepositoryId(value));

    public static readonly ValueConverter<SecretId, Guid> SecretId = new(
        id => id.Value,
        value => new SecretId(value));

    public static readonly ValueConverter<SymphonyInstanceId, Guid> SymphonyInstanceId = new(
        id => id.Value,
        value => new SymphonyInstanceId(value));

    public static readonly ValueConverter<WorkflowProfileId, Guid> WorkflowProfileId = new(
        id => id.Value,
        value => new WorkflowProfileId(value));

    public static readonly ValueConverter<Uri, string> AbsoluteUri = new(
        uri => uri.ToString(),
        value => new Uri(value, UriKind.Absolute));
}
