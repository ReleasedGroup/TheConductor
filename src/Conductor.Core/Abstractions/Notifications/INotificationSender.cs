namespace Conductor.Core.Abstractions.Notifications;

public interface INotificationSender
{
    string Channel { get; }

    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}

public sealed record NotificationMessage(
    string Subject,
    string Body,
    string Severity,
    DateTimeOffset CreatedAtUtc);
