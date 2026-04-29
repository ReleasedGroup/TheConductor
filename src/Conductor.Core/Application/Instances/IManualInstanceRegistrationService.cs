namespace Conductor.Core.Application.Instances;

public interface IManualInstanceRegistrationService
{
    Task<ManualInstanceRegistrationResult> RegisterAsync(
        ManualInstanceRegistrationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ManualInstanceRegistrationRequest(
    string BaseUrl,
    string? DisplayName = null,
    string? RequestedByUserId = null);

public sealed record ManualInstanceRegistrationResult(
    string InstanceId,
    string RepositoryId,
    string RepositoryFullName,
    string DisplayName,
    Uri BaseUrl,
    string LifecycleStatus,
    string HealthStatus,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? LastSeenAtUtc,
    string? SymphonyVersion,
    string SnapshotId);

public sealed class ManualInstanceRegistrationValidationException : Exception
{
    public ManualInstanceRegistrationValidationException(
        IReadOnlyDictionary<string, string[]> errors)
        : base("Manual instance registration failed validation.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

public sealed class DuplicateSymphonyInstanceRegistrationException : Exception
{
    public DuplicateSymphonyInstanceRegistrationException(string existingInstanceId, Uri baseUrl)
        : base($"A Symphony instance is already registered for {baseUrl}.")
    {
        ExistingInstanceId = existingInstanceId;
        BaseUrl = baseUrl;
    }

    public string ExistingInstanceId { get; }

    public Uri BaseUrl { get; }
}
