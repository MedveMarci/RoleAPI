using System.Collections.Generic;
using RoleAPI.API.Abilities;
using RoleAPI.API.Schematics;
using RoleAPI.Internal;
using SecretLabNAudio.Core;
using UncomplicatedCustomRoles.API.Features;

namespace RoleAPI.API.Roles;

/// <summary>
///     Base class for UCR custom roles with ability and schematic support.
///     Derive from this class and register via <see cref="RoleAPI.RegisterRole" /> to enable RoleAPI features.
/// </summary>
/// <example>
///     <code>
/// public class MyRole : UcrRoleBase
/// {
///     public override int Id { get; set; } = 100;
///     public override string Name { get; set; } = "My Role";
///     public override PlayerRoles.RoleTypeId Role { get; set; } = PlayerRoles.RoleTypeId.ClassD;
/// 
///     public override IReadOnlyList&lt;AbilityBase&gt; Abilities { get; } = new List&lt;AbilityBase&gt;
///     {
///         new MyAbility()
///     };
/// }
/// </code>
/// </example>
public abstract class UcrRoleBase : EventCustomRole
{
    /// <summary>
    ///     Gets the default <see cref="SecretLabNAudio.Core.SpeakerSettings" /> used for ability audio in this role.
    ///     Individual abilities can override this via <see cref="AbilityBase.SpeakerSettings" />.
    /// </summary>
    public virtual SpeakerSettings? DefaultSpeakerSettings { get; } = null;

    /// <summary>
    ///     Gets the schematic configuration to attach to players with this role.
    ///     Return <see langword="null" /> if no schematic is needed.
    /// </summary>
    public virtual RoleSchematic? Schematic { get; } = null;

    /// <summary>Gets the list of abilities available to players assigned this role.</summary>
    public abstract IReadOnlyList<AbilityBase> Abilities { get; }

    /// <inheritdoc />
    public override void OnSpawned(SummonedCustomRole role)
    {
        base.OnSpawned(role);
        AbilityManager.OnRoleAssigned(role.Player, this);
    }
}