using NLog;
using NLog.Layouts;
using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.Common.Instrumentation;

public class CleansingConsoleLogLayout(string format) : Layout
{
    private readonly SimpleLayout _innerLayout = new(format);

    protected override string GetFormattedMessage(LogEventInfo logEvent)
    {
        var message = _innerLayout.Render(logEvent);

        return RuntimeInfo.IsProduction
            ? CleanseLogMessage.Cleanse(message)
            : message;
    }
}
