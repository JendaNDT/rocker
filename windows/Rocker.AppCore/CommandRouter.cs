// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.AppCore;

public sealed record CommandContext(DocumentSession Session,string WindowId,DocumentSession? MediaOwner);
public sealed record DesktopCommand(string Id,string Label,Func<CommandContext,bool> Enabled,Func<CommandContext,Task> Execute);
public sealed class CommandRouter
{
    private readonly Dictionary<string,DesktopCommand> commands=[];
    private readonly HashSet<(string Id,string Owner)> running=[];
    public IReadOnlyList<DesktopCommand> All=>commands.Values.ToArray();
    public void Register(DesktopCommand command)
    {
        if(!Rocker.Domain.UserData.ValidId(command.Id) || string.IsNullOrWhiteSpace(command.Label) || !commands.TryAdd(command.Id,command))throw new ArgumentException("Příkaz není platný nebo je registrovaný dvakrát.");
    }
    public bool CanExecute(string id,CommandContext context)=>context.Session.IsAlive && commands.TryGetValue(id,out var command) && !running.Contains((id,context.Session.Id)) && command.Enabled(context);
    public async Task<bool> ExecuteAsync(string id,CommandContext context)
    {
        if(!CanExecute(id,context))return false;var command=commands[id];var key=(id,context.Session.Id);running.Add(key);
        try{await command.Execute(context);return true;}finally{running.Remove(key);}
    }
}
