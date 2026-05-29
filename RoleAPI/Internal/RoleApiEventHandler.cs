using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using RoleAPI.ApiFeatures;

namespace RoleAPI.Internal;

internal sealed class RoleApiEventHandler : CustomEventsHandler
{
    public override void OnServerWaitingForPlayers()
    {
        ApiManager.CheckForUpdates();
        base.OnServerWaitingForPlayers();
    }

    public override void OnPlayerDying(PlayerDyingEventArgs ev)
    {
        AbilityManager.OnRoleRemoved(ev.Player);
        base.OnPlayerDying(ev);
    }

    public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (PlayerAbilityState.TryGet(ev.Player, out _))
            AbilityManager.OnRoleRemoved(ev.Player);

        if (AbilityManager.TryGetVanillaBinding(ev.NewRole.RoleTypeId, out var binding) && binding != null)
            AbilityManager.OnVanillaRoleAssigned(ev.Player, binding);
        base.OnPlayerChangedRole(ev);
    }

    public override void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        AbilityManager.OnRoleRemoved(ev.Player);
        base.OnPlayerLeft(ev);
    }
}