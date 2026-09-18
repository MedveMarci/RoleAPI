using System;
using System.Reflection;
using LabApi.Features.Wrappers;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using RoleAPI.API.Audio;
using SecretLabNAudio.Core;

namespace RoleAPI.API.Abilities;

public sealed class AbilityExecutionContext
{
    private readonly Assembly _abilityAssembly;

    /// <summary>Gets the player who triggered this ability.</summary>
    public Player Player { get; }

    /// <summary>
    ///     Gets the <see cref="SecretLabNAudio.Core.AudioPlayer" /> allocated for this execution.
    ///     Set <see cref="SoundFile" /> to play audio, or configure the player directly.
    /// </summary>
    public AudioPlayer? AudioPlayer { get; }

    /// <summary>Gets the <see cref="SchematicObject" /> attached to the player's current role, if any.</summary>
    public SchematicObject? RoleSchematic { get; }

    /// <summary>Gets the <see cref="AnimationController" /> for the role's schematic, if any.</summary>
    public AnimationController? Animator { get; }

    /// <summary>
    ///     Gets or sets the audio played when the ability executes.
    ///     Overrides the ability's default <see cref="AbilityBase.Sound" />, <see cref="AbilityBase.SoundResource" />
    ///     and <see cref="AbilityBase.SoundFile" />.
    /// </summary>
    public AbilityAudio? Sound { get; set; }

    /// <summary>
    ///     Gets or sets the sound file path to play when the ability executes.
    ///     Shorthand for assigning <see cref="AbilityAudio.File" /> to <see cref="Sound" />.
    /// </summary>
    public string? SoundFile
    {
        get => Sound is { IsEmbedded: false } audio ? audio.Identifier : null;
        set => Sound = string.IsNullOrEmpty(value) ? null : AbilityAudio.File(value!);
    }

    /// <summary>
    ///     Gets or sets the name of an embedded audio resource to play when the ability executes.
    ///     The resource is looked up in the executing ability's own assembly.
    ///     Shorthand for assigning <see cref="AbilityAudio.Embedded(string, Assembly)" /> to <see cref="Sound" />.
    ///     See <see cref="Resources.EmbeddedResources" /> for how names are matched.
    /// </summary>
    public string? SoundResource
    {
        get => Sound is { IsEmbedded: true } audio ? audio.Identifier : null;
        set => Sound = string.IsNullOrEmpty(value) ? null : AbilityAudio.Embedded(value!, _abilityAssembly);
    }

    /// <summary>Gets a value indicating whether execution has been denied.</summary>
    public bool IsDenied => DenialReason != null;

    /// <summary>Gets the denial reason, or <see langword="null" /> if not denied.</summary>
    public string? DenialReason { get; private set; }

    /// <summary>
    ///     Gets or sets an optional success hint shown to the player after the ability executes.
    ///     Only displayed when the execution is not denied.
    /// </summary>
    public string? ActivationHint { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this ability execution locks all other abilities
    ///     until <see cref="CompleteAnimation" /> is called or the associated audio ends.
    /// </summary>
    public bool LocksDuringExecution { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the animation lock is released automatically when the sound ends.
    ///     Set to <c>false</c> to require a manual <see cref="CompleteAnimation" /> call even when audio is playing.
    /// </summary>
    public bool AutoReleaseLock { get; set; }

    internal Action? OnAnimationComplete { get; set; }

    internal AbilityExecutionContext(Player player, AudioPlayer? audioPlayer, SchematicObject? schematic, Assembly abilityAssembly)
    {
        Player = player;
        AudioPlayer = audioPlayer;
        RoleSchematic = schematic;
        Animator = schematic?.AnimationController;
        _abilityAssembly = abilityAssembly;
    }

    /// <summary>Plays an animation by name on the role schematic's animator.</summary>
    public void PlayAnimation(string animationName)
    {
        Animator?.Play(animationName);
    }

    /// <summary>Returns <see langword="true" /> while the schematic's animation is still in progress.</summary>
    public bool IsAnimationPlaying()
    {
        return Animator is { Animators.Count: > 0 } && Animator.Animators[0].GetCurrentAnimatorStateInfo(0).normalizedTime < 1f;
    }

    /// <summary>
    ///     Denies this ability execution with a reason shown to the player.
    ///     This prevents use/cooldown consumption.
    /// </summary>
    /// <param name="reason">The reason displayed to the player.</param>
    public void Deny(string reason)
    {
        DenialReason = reason;
    }

    /// <summary>Signals that the animation or locked action has completed, releasing the ability lock.</summary>
    public void CompleteAnimation()
    {
        OnAnimationComplete?.Invoke();
    }
}