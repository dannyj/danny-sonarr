using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Dashboard
{
    public class CaptureDashboardSnapshotCommand : Command
    {
        public override string CompletionMessage => "Dashboard snapshot captured";
    }
}
