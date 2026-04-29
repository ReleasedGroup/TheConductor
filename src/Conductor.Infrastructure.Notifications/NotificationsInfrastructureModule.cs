using Conductor.Core.Application;

namespace Conductor.Infrastructure.Notifications;

public static class NotificationsInfrastructureModule
{
    public static InfrastructureModule Descriptor { get; } = new(
        "Conductor.Infrastructure.Notifications",
        "Email, Teams, Slack, and GitHub comment notification adapters.");
}
