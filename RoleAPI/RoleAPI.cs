using System.Collections.Generic;
using LabApi.Features.Wrappers;
using PlayerRoles;
using RoleAPI.API.Abilities;
using RoleAPI.API.Roles;
using RoleAPI.ApiFeatures;
using RoleAPI.Internal;
using SecretLabNAudio.Core;
using UncomplicatedCustomRoles.API.Features;

namespace RoleAPI;

public static class RoleAPI
{
    private static readonly List<UcrRoleBase> RegisteredRoles = [];

    /// <summary>Gets all UCR roles currently registered with RoleAPI.</summary>
    public static IReadOnlyList<UcrRoleBase> Roles => RegisteredRoles;

    /// <summary>
    ///     Registers a <see cref="UcrRoleBase" /> role with both UCR and RoleAPI.
    ///     This sets up ability keybinds and enables schematic/hint support for this role.
    /// </summary>
    public static void RegisterRole(UcrRoleBase role)
    {
        CustomRole.Register(role);
        AbilityManager.RegisterRoleAbilities(role);
        RegisteredRoles.Add(role);
        LogManager.Info($"Registered role: {role.Name} (ID: {role.Id}) with {role.Abilities.Count} abilities.");
    }

    /// <summary>
    ///     Unregisters a previously registered <see cref="UcrRoleBase" /> role from both UCR and RoleAPI.
    /// </summary>
    public static void UnregisterRole(UcrRoleBase role)
    {
        CustomRole.Unregister(role);
        AbilityManager.UnregisterRoleAbilities(role);
        RegisteredRoles.Remove(role);
        LogManager.Info($"Unregistered role: {role.Name} (ID: {role.Id}).");
    }

    /// <summary>
    ///     Binds a set of abilities to a vanilla SCP:SL role. Players who spawn as this role
    ///     will automatically receive the specified abilities with keybinds and a status hint.
    /// </summary>
    /// <param name="roleType">The vanilla role type to bind to.</param>
    /// <param name="abilities">The abilities to give to players with this role.</param>
    /// <param name="speakerSettings">Optional speaker settings for ability audio.</param>
    public static void BindToRole(RoleTypeId roleType, IReadOnlyList<AbilityBase> abilities,
        SpeakerSettings? speakerSettings = null)
    {
        AbilityManager.RegisterVanillaBinding(roleType, abilities, speakerSettings);
        LogManager.Info($"Bound {abilities.Count} abilities to vanilla role {roleType}.");
    }

    /// <summary>
    ///     Removes a previously registered vanilla role binding.
    /// </summary>
    public static void UnbindFromRole(RoleTypeId roleType)
    {
        AbilityManager.UnregisterVanillaBinding(roleType);
    }

    /// <summary>
    ///     Gives a single ability to a specific player, regardless of their role.
    ///     The ability keybind and hint are set up automatically.
    /// </summary>
    public static void GiveAbility(Player player, AbilityBase ability)
    {
        AbilityManager.GiveAbility(player, ability);
    }

    /// <summary>
    ///     Removes a previously given ability from a specific player.
    /// </summary>
    public static void RemoveAbility(Player player, AbilityBase ability)
    {
        AbilityManager.RemoveAbility(player, ability);
    }
}