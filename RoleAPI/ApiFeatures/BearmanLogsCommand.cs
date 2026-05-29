using System;
using CommandSystem;

namespace RoleAPI.ApiFeatures;

[CommandHandler(typeof(GameConsoleCommandHandler))]
public class BearmanLogs999 : ICommand
{
    public string Command => "bearmanlogsroleapi";

    public string[] Aliases { get; } = ["bmlogsroleapi"];

    public string Description => "Sends collected plugin logs to the log server and returns the log id.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var getLogHistory = LogManager.GetLogHistory();
        response = getLogHistory.logResult;
        return getLogHistory.success;
    }
}