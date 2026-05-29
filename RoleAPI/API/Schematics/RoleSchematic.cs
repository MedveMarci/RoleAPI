using UnityEngine;

namespace RoleAPI.API.Schematics;

public sealed class RoleSchematic
{
    /// <summary>Gets the name of the ProjectMER schematic to spawn.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the position offset applied relative to the player's position.</summary>
    public Vector3 PositionOffset { get; init; } = Vector3.zero;

    /// <summary>Gets the rotation offset applied relative to the player's rotation.</summary>
    public Vector3 RotationOffset { get; init; } = Vector3.zero;

    /// <summary>
    ///     Gets a value indicating whether the player's default character model should be hidden
    ///     from all other players by applying the <c>Fade</c> status effect.
    /// </summary>
    public bool HideCarrierModel { get; init; }
}