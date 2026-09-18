using System.Collections.Generic;
using RoleAPI.API.Audio;
using RoleAPI.API.Resources;
using SecretLabNAudio.Core;
using UnityEngine;

namespace RoleAPI.API.Abilities;

public abstract class AbilityBase
{
    /// <summary>Gets the display name of this ability, shown in the hint UI.</summary>
    public abstract string Name { get; }

    /// <summary>Gets the description of this ability, shown as a keybind hint.</summary>
    public abstract string Description { get; }

    /// <summary>
    ///     Gets the maximum number of uses for this ability per role assignment.
    ///     Use <c>-1</c> for unlimited uses.
    /// </summary>
    public virtual int MaxUses { get; } = -1;

    /// <summary>
    ///     Gets the cooldown in seconds between uses.
    ///     Only applies when <see cref="MaxUses" /> is not <c>1</c>.
    /// </summary>
    public virtual float Cooldown { get; } = 0f;

    /// <summary>
    ///     Gets the default sound file path played when this ability is executed.
    ///     Can be overridden per-execution via <see cref="AbilityExecutionContext.SoundFile" />.
    /// </summary>
    public virtual string? SoundFile { get; } = null;

    /// <summary>
    ///     Gets the name of an embedded audio resource in the ability's own assembly, played when this ability is
    ///     executed. Takes precedence over <see cref="SoundFile" />.
    ///     Can be overridden per-execution via <see cref="AbilityExecutionContext.SoundResource" />.
    ///     See <see cref="EmbeddedResources" /> for how to embed files and how names are matched.
    /// </summary>
    public virtual string? SoundResource { get; } = null;

    /// <summary>
    ///     Gets the audio source played when this ability is executed, for cases where neither
    ///     <see cref="SoundFile" /> nor <see cref="SoundResource" /> fits (for example a resource in another assembly).
    ///     Takes precedence over both.
    ///     Can be overridden per-execution via <see cref="AbilityExecutionContext.Sound" />.
    /// </summary>
    public virtual AbilityAudio? Sound { get; } = null;

    /// <summary>
    ///     Gets the speaker settings override for this ability's audio.
    ///     When <see langword="null" />, falls back to the role's <see cref="Roles.UcrRoleBase.DefaultSpeakerSettings" />.
    /// </summary>
    public virtual SpeakerSettings? SpeakerSettings { get; } = null;

    /// <summary>
    ///     Gets a value indicating whether this ability locks all other abilities while its audio is playing.
    ///     Set to <c>false</c> to allow other abilities to be used while the sound plays.
    ///     Defaults to <c>true</c>. Can be overridden per-execution via
    ///     <see cref="AbilityExecutionContext.LocksDuringExecution" />.
    /// </summary>
    public virtual bool LocksDuringExecution { get; } = true;

    /// <summary>
    ///     Gets a value indicating whether the animation lock is released automatically when the sound ends.
    ///     Set to <c>false</c> to require a manual <see cref="AbilityExecutionContext.CompleteAnimation" /> call.
    ///     Defaults to <c>true</c>. Only relevant when <see cref="LocksDuringExecution" /> is <c>true</c> and a sound file is
    ///     used.
    /// </summary>
    public virtual bool AutoReleaseLock { get; } = true;

    /// <summary>Gets the suggested default keybind key for this ability.</summary>
    public virtual KeyCode DefaultKey { get; } = KeyCode.None;

    /// <summary>
    ///     Gets the list of conditions that must all pass before this ability can be used.
    ///     Failed conditions display their <see cref="AbilityCondition.FailureMessage" /> in the UI.
    /// </summary>
    public virtual IReadOnlyList<AbilityCondition> Conditions { get; } = [];

    /// <summary>
    ///     Implement the ability's logic here. Call <see cref="AbilityExecutionContext.Deny" /> to cancel execution with a
    ///     message.
    /// </summary>
    /// <param name="ctx">The execution context, providing access to player, audio, and schematic.</param>
    protected abstract void OnExecute(AbilityExecutionContext ctx);

    internal void Execute(AbilityExecutionContext ctx)
    {
        OnExecute(ctx);
    }

    /// <summary>
    ///     Resolves this ability's default audio from <see cref="Sound" />, <see cref="SoundResource" /> or
    ///     <see cref="SoundFile" />, in that order.
    /// </summary>
    internal AbilityAudio? ResolveSound()
    {
        if (Sound != null)
            return Sound;

        if (!string.IsNullOrEmpty(SoundResource))
            return AbilityAudio.Embedded(SoundResource!, GetType().Assembly);

        return string.IsNullOrEmpty(SoundFile) ? null : AbilityAudio.File(SoundFile!);
    }
}