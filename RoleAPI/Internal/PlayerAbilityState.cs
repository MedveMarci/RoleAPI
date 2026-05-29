#if RueI
using RueI.API.Elements;
#else
using HintServiceMeow.Core.Models.Hints;
#endif
using System.Collections.Generic;
using LabApi.Features.Wrappers;
using ProjectMER.Features.Objects;
using RoleAPI.API.Abilities;
using RoleAPI.API.Roles;
using RoleAPI.API.Schematics;
using SecretLabNAudio.Core;
using UnityEngine;

namespace RoleAPI.Internal;

internal sealed class PlayerAbilityState
{
    private static readonly Dictionary<Player, PlayerAbilityState> States = new();
    private static readonly object StatesLock = new();

    private readonly List<AbilityBase> _abilities = [];
    private readonly Dictionary<AbilityBase, PerAbilityData> _abilityData = new();
    private volatile bool _isAnimationLocked;

    private PlayerAbilityState(Player player)
    {
        Player = player;
    }

    internal Player Player { get; }

    /// <summary>The UCR role this state was created from, or null for vanilla/dynamic states.</summary>
    internal UcrRoleBase? Role { get; private set; }

    internal IReadOnlyList<AbilityBase> Abilities => _abilities;
    internal SpeakerSettings? SpeakerSettings { get; set; }
    internal RoleSchematic? SchematicConfig { get; private set; }

    internal SchematicObject? SpawnedSchematic { get; set; }
    internal AudioPlayer? ActiveAudioPlayer { get; set; }

#if RueI
    internal Tag AbilityHintTag { get; } = new();
    internal Tag FeedbackHintTag { get; } = new();
#else
    internal Hint? AbilityHint { get; set; }
    internal Hint? FeedbackHint { get; set; }
#endif

    internal bool IsAnimationLocked => _isAnimationLocked;

    internal static bool TryGet(Player player, out PlayerAbilityState? state)
    {
        lock (StatesLock)
        {
            return States.TryGetValue(player, out state);
        }
    }

    internal static PlayerAbilityState CreateForRole(Player player, UcrRoleBase role)
    {
        lock (StatesLock)
        {
            var state = new PlayerAbilityState(player)
            {
                Role = role,
                SpeakerSettings = role.DefaultSpeakerSettings,
                SchematicConfig = role.Schematic
            };

            foreach (var ability in role.Abilities)
            {
                state._abilities.Add(ability);
                state._abilityData[ability] = new PerAbilityData(ability.MaxUses);
            }

            States[player] = state;
            return state;
        }
    }

    internal static PlayerAbilityState CreateForVanilla(Player player, IReadOnlyList<AbilityBase> abilities,
        SpeakerSettings? speakerSettings)
    {
        lock (StatesLock)
        {
            var state = new PlayerAbilityState(player)
            {
                SpeakerSettings = speakerSettings
            };

            foreach (var ability in abilities)
            {
                state._abilities.Add(ability);
                state._abilityData[ability] = new PerAbilityData(ability.MaxUses);
            }

            States[player] = state;
            return state;
        }
    }

    internal static PlayerAbilityState GetOrCreate(Player player)
    {
        lock (StatesLock)
        {
            if (States.TryGetValue(player, out var existing))
                return existing;

            var state = new PlayerAbilityState(player);
            States[player] = state;
            return state;
        }
    }

    internal static void Remove(Player player)
    {
        lock (StatesLock)
        {
            States.Remove(player);
        }
    }

    internal bool HasAbility(AbilityBase ability)
    {
        return _abilityData.ContainsKey(ability);
    }

    internal bool TryGetAbilityData(AbilityBase ability, out PerAbilityData? data)
    {
        return _abilityData.TryGetValue(ability, out data);
    }

    internal void AddAbility(AbilityBase ability)
    {
        if (_abilityData.ContainsKey(ability))
            return;

        _abilities.Add(ability);
        _abilityData[ability] = new PerAbilityData(ability.MaxUses);
    }

    internal bool RemoveAbility(AbilityBase ability)
    {
        if (!_abilityData.Remove(ability))
            return false;

        _abilities.Remove(ability);
        return true;
    }

    internal void SetAnimationLock(bool locked)
    {
        _isAnimationLocked = locked;
    }

    internal sealed class PerAbilityData
    {
        internal PerAbilityData(int maxUses)
        {
            UsesRemaining = maxUses;
        }

        internal int UsesRemaining { get; set; }
        internal float CooldownEndsAt { get; set; }

        internal bool IsOnCooldown => CooldownEndsAt > Time.time;
        internal float RemainingCooldown => Mathf.Max(0f, CooldownEndsAt - Time.time);
        internal bool IsExhausted => UsesRemaining == 0;
    }
}