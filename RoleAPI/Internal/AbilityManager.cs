#if RueI
using RueI.API;
using RueI.API.Elements;
#else
using System.Reflection;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Models.UnityAdaptors.Parameters;
using HintServiceMeow.Core.Utilities;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using NorthwoodLib.Pools;
using PlayerRoles;
using ProjectMER.Features;
using RoleAPI.API.Abilities;
using RoleAPI.API.Roles;
using RoleAPI.API.Schematics;
using RoleAPI.ApiFeatures;
using RoleAPI.Internal.Settings;
using SecretAPI.Features.UserSettings;
using SecretLabNAudio.Core;
using SecretLabNAudio.Core.Extensions;
using SecretLabNAudio.Core.Pools;
using UnityEngine;

namespace RoleAPI.Internal;

internal static class AbilityManager
{
    private static readonly Dictionary<AbilityBase, AbilityKeybindSetting> RegisteredKeybinds = new();
#if !RueI
    private static readonly string HintGroupName = Assembly.GetExecutingAssembly().FullName;
#endif

    private static readonly Dictionary<RoleTypeId, VanillaRoleBinding> VanillaBindings = new();

    private static RoleApiConfig Cfg => RoleApiPlugin.Singleton?.Config ?? new RoleApiConfig();

    private static void EnsureKeybindRegistered(AbilityBase ability)
    {
        if (RegisteredKeybinds.ContainsKey(ability))
            return;

        var setting = new AbilityKeybindSetting(ability);
        RegisteredKeybinds[ability] = setting;
        CustomSetting.Register(setting);
        LogManager.Debug($"Registered keybind for ability: {ability.Name}");
    }

    private static void UnregisterKeybind(AbilityBase ability)
    {
        if (!RegisteredKeybinds.TryGetValue(ability, out var setting))
            return;

        CustomSetting.UnRegister(setting);
        RegisteredKeybinds.Remove(ability);
    }

    internal static void RegisterRoleAbilities(UcrRoleBase role)
    {
        foreach (var ability in role.Abilities)
            EnsureKeybindRegistered(ability);
    }

    internal static void UnregisterRoleAbilities(UcrRoleBase role)
    {
        foreach (var ability in role.Abilities)
            UnregisterKeybind(ability);
    }

    internal static void RegisterVanillaBinding(RoleTypeId roleType, IReadOnlyList<AbilityBase> abilities,
        SpeakerSettings? speakerSettings)
    {
        VanillaBindings[roleType] = new VanillaRoleBinding(abilities, speakerSettings);

        foreach (var ability in abilities)
            EnsureKeybindRegistered(ability);

        LogManager.Debug($"Registered vanilla binding for {roleType} with {abilities.Count} abilities.");
    }

    internal static void UnregisterVanillaBinding(RoleTypeId roleType)
    {
        VanillaBindings.Remove(roleType);
        LogManager.Debug($"Unregistered vanilla binding for {roleType}.");
    }

    internal static bool TryGetVanillaBinding(RoleTypeId roleType, out VanillaRoleBinding? binding)
    {
        return VanillaBindings.TryGetValue(roleType, out binding);
    }

    internal static void OnRoleAssigned(Player player, UcrRoleBase role)
    {
        try
        {
            CleanupPlayer(player);

            var state = PlayerAbilityState.CreateForRole(player, role);

            SetupSchematic(player, state, state.SchematicConfig);

            if (state.Abilities.Count > 0)
                SetupHint(player, state);

            CustomSetting.SendSettingsToPlayer(player);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error in OnRoleAssigned {ex}");
        }
    }

    internal static void OnVanillaRoleAssigned(Player player, VanillaRoleBinding binding)
    {
        try
        {
            CleanupPlayer(player);

            var state = PlayerAbilityState.CreateForVanilla(player, binding.Abilities, binding.SpeakerSettings);

            if (state.Abilities.Count > 0)
                SetupHint(player, state);

            CustomSetting.SendSettingsToPlayer(player);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error in OnVanillaRoleAssigned\n{ex}");
        }
    }

    internal static void OnRoleRemoved(Player player)
    {
        try
        {
            if (!PlayerAbilityState.TryGet(player, out _))
                return;

            CleanupPlayer(player);
            CustomSetting.SendSettingsToPlayer(player);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error in OnRoleRemoved\n{ex}");
        }
    }

    internal static void GiveAbility(Player player, AbilityBase ability)
    {
        try
        {
            EnsureKeybindRegistered(ability);

            var state = PlayerAbilityState.GetOrCreate(player);
            state.AddAbility(ability);

            RebuildHint(player, state);
            CustomSetting.SendSettingsToPlayer(player);

            LogManager.Debug($"Gave ability {ability.Name} to {player.Nickname}.");
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error in GiveAbility\n{ex}");
        }
    }

    internal static void RemoveAbility(Player player, AbilityBase ability)
    {
        try
        {
            if (!PlayerAbilityState.TryGet(player, out var state) || state == null)
                return;

            if (!state.RemoveAbility(ability))
                return;

            if (state.Abilities.Count == 0)
                CleanupPlayer(player);
            else
                RebuildHint(player, state);

            CustomSetting.SendSettingsToPlayer(player);
            LogManager.Debug($"Removed ability {ability.Name} from {player.Nickname}.");
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error in RemoveAbility\n{ex}");
        }
    }

    internal static bool TryExecuteAbility(Player player, AbilityBase ability)
    {
        try
        {
            LogManager.Debug($"Attempting to execute ability {ability.Name} for player {player.Nickname}.");
            if (!PlayerAbilityState.TryGet(player, out var state) || state == null)
                return false;
            LogManager.Debug(
                $"Player {player.Nickname} has {state.Abilities.Count} abilities. Checking for {ability.Name}.");
            if (!state.HasAbility(ability))
                return false;
            LogManager.Debug(
                $"Player {player.Nickname} has ability {ability.Name}. Checking conditions and cooldowns.");
            if (state.IsAnimationLocked)
            {
                ShowFailHint(player, Cfg.FeedbackBusy);
                return false;
            }

            LogManager.Debug($"Player {player.Nickname} is not busy. Checking ability data for {ability.Name}.");
            if (!state.TryGetAbilityData(ability, out var abilityData) || abilityData == null)
                return false;
            LogManager.Debug(
                $"Ability data for {ability.Name} - UsesRemaining: {abilityData.UsesRemaining}, CooldownEndsAt: {abilityData.CooldownEndsAt}, CurrentTime: {Time.time}");
            if (abilityData.IsExhausted)
            {
                ShowFailHint(player, string.Format(Cfg.FeedbackNoUses, ability.Name));
                return false;
            }

            LogManager.Debug($"Ability {ability.Name} is not exhausted. Checking cooldown.");
            if (abilityData.IsOnCooldown)
            {
                ShowFailHint(player, string.Format(Cfg.FeedbackCooldown, ability.Name, abilityData.RemainingCooldown));
                return false;
            }

            LogManager.Debug($"Ability {ability.Name} is not on cooldown. Checking conditions.");
            foreach (var condition in ability.Conditions)
                if (!condition.IsMet(player))
                {
                    ShowFailHint(player, condition.FailureMessage);
                    return false;
                }

            var speakerSettings = ability.SpeakerSettings
                                  ?? state.SpeakerSettings
                                  ?? SpeakerSettings.Default;
            LogManager.Debug($"Player {player.Nickname} is executing ability {ability.Name} with speaker settings: " +
                             $"Volume={speakerSettings.Volume}, Pitch={speakerSettings.IsSpatial}, Max={speakerSettings.MaxDistance}, Min={speakerSettings.MinDistance}");
            var audioPlayer = AudioPlayerPool.Rent(speakerSettings, player.GameObject?.transform);

            var ctx = new AbilityExecutionContext(player, audioPlayer, state.SpawnedSchematic)
            {
                OnAnimationComplete = () => state.SetAnimationLock(false),
                LocksDuringExecution = ability.LocksDuringExecution,
                AutoReleaseLock = ability.AutoReleaseLock
            };

            ability.Execute(ctx);

            if (ctx.IsDenied)
            {
                AudioPlayerPool.Return(audioPlayer);
                ShowFailHint(player, ctx.DenialReason ?? "Ability denied.");
                return false;
            }

            if (ability.MaxUses != -1)
                abilityData.UsesRemaining--;

            if (ability.Cooldown > 0f)
                abilityData.CooldownEndsAt = Time.time + ability.Cooldown;

            if (!string.IsNullOrEmpty(ctx.ActivationHint))
                ShowSuccessHint(player, ctx.ActivationHint!);

            var soundFile = ctx.SoundFile ?? ability.SoundFile;
            LogManager.Debug(
                $"Ability {ability.Name} execution by {player.Nickname} resulted in sound file: {soundFile ?? "null"}");
            if (!string.IsNullOrEmpty(soundFile))
            {
                if (!File.Exists(soundFile))
                {
                    LogManager.Warn($"Sound file not found for ability '{ability.Name}': {soundFile}");
                    ShowFailHint(player, string.Format(Cfg.FeedbackSoundNotFound, Path.GetFileName(soundFile)));
                    AudioPlayerPool.Return(audioPlayer);
                    return true;
                }

                audioPlayer.UseFileSafe(soundFile);
                audioPlayer.Ended += () => AudioPlayerPool.Return(audioPlayer);

                if (!ctx.LocksDuringExecution) return true;
                state.SetAnimationLock(true);
                if (ctx.AutoReleaseLock)
                    audioPlayer.Ended += () => state.SetAnimationLock(false);
            }
            else
            {
                AudioPlayerPool.Return(audioPlayer);

                if (ctx.LocksDuringExecution)
                    state.SetAnimationLock(true);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error executing ability\n{ex}");
            return false;
        }
    }

    private static void SetupSchematic(Player player, PlayerAbilityState state, RoleSchematic? config)
    {
        if (config == null || string.IsNullOrEmpty(config.Name))
            return;

        try
        {
            var playerTransform = player.GameObject?.transform;
            var position = player.Position + config.PositionOffset;
            var rotation = playerTransform != null
                ? playerTransform.rotation * Quaternion.Euler(config.RotationOffset)
                : Quaternion.Euler(config.RotationOffset);

            var schematic = ObjectSpawner.SpawnSchematic(config.Name, position, rotation);

            if (schematic == null)
            {
                LogManager.Warn($"Failed to spawn schematic '{config.Name}' for {player.Nickname}.");
                return;
            }

            foreach (var adminToy in schematic.AdminToyBases)
                adminToy.syncInterval = 0f;

            state.SpawnedSchematic = schematic;

            if (config.HideCarrierModel)
                player.EnableEffect<Fade>(255);

            if (playerTransform != null)
                schematic.transform.SetParent(playerTransform, true);
        }
        catch (Exception ex)
        {
            LogManager.Error($"Error setting up schematic\n{ex}");
        }
    }

    private static void SetupHint(Player player, PlayerAbilityState state)
    {
#if RueI
        var cfg = Cfg;
        var element = new DynamicElement(cfg.AbilityHintPosition, () => BuildAbilityHintText(player, state))
        {
            UpdateInterval = TimeSpan.FromSeconds(1)
        };
        RueDisplay.Get(player.ReferenceHub).Show(state.AbilityHintTag, element);
#else
        var cfg = Cfg;
        var hint = new Hint
        {
            YCoordinate = cfg.HintY,
            XCoordinate = cfg.HintX,
            Alignment = cfg.Alignment,
            AutoText = _ => BuildAbilityHintText(player, state)
        };

        if (cfg.HintFontSize != 0)
            hint.FontSize = cfg.HintFontSize;

        for (int i = 0; i < state.Abilities.Count; i++)
        {
            if (RegisteredKeybinds.TryGetValue(state.Abilities[i], out var keybind))
                hint.Parameters.Add(i.ToString(), new SSKeybindParameter(keybind.SettingId));
        }

        state.AbilityHint = hint;
        PlayerDisplay.Get(player).AddHint(hint, HintGroupName);
#endif
    }

    private static void RebuildHint(Player player, PlayerAbilityState state)
    {
#if RueI
        RueDisplay.Get(player.ReferenceHub).Remove(state.AbilityHintTag);
#else
        if (state.AbilityHint != null)
        {
            try
            {
                PlayerDisplay.Get(player).RemoveHint(state.AbilityHint, HintGroupName);
            }
            catch
            {
                /* ignored */
            }

            state.AbilityHint = null;
        }
#endif

        if (state.Abilities.Count > 0)
            SetupHint(player, state);
    }

    private static string BuildAbilityHintText(Player player, PlayerAbilityState state)
    {
        var cfg = Cfg;
        var sb = StringBuilderPool.Shared.Rent();
        try
        {
            sb.AppendLine(cfg.HudTitle);

            for (int i = 0; i < state.Abilities.Count; i++)
            {
                var ability = state.Abilities[i];
                if (!state.TryGetAbilityData(ability, out var data) || data == null)
                    continue;

                sb.Append(ability.Name);
                if (RegisteredKeybinds.ContainsKey(ability))
                {
                    sb.Append(" {");
                    sb.Append(i);
                    sb.Append('}');
                }

                if (state.IsAnimationLocked)
                {
                    sb.Append(" — ");
                    sb.AppendLine(cfg.StatusBusy);
                }
                else if (data.IsExhausted)
                {
                    sb.Append(" — ");
                    sb.AppendLine(cfg.StatusExhausted);
                }
                else if (data.IsOnCooldown)
                {
                    sb.Append(" — ");
                    sb.AppendLine(string.Format(cfg.StatusCooldown, data.RemainingCooldown));
                }
                else
                {
                    string? failMsg = null;
                    foreach (var cond in ability.Conditions)
                        if (!cond.IsMet(player))
                        {
                            failMsg = cond.FailureMessage;
                            break;
                        }

                    sb.Append(" — ");
                    sb.AppendLine(failMsg != null
                        ? string.Format(cfg.StatusConditionFailed, failMsg)
                        : cfg.StatusReady);
                }

                if (ability.MaxUses > 0 && data.UsesRemaining >= 0)
                    sb.AppendLine(string.Format(cfg.UsesRemaining, data.UsesRemaining, ability.MaxUses));
            }

            sb.AppendLine(cfg.SSSettingsNote);

            return sb.ToString();
        }
        finally
        {
            StringBuilderPool.Shared.Return(sb);
        }
    }

    private static void ShowFeedbackHint(Player player, string text)
    {
#if RueI
        var element = new BasicElement(Cfg.FeedbackHintPosition, text);
        if (PlayerAbilityState.TryGet(player, out var state) && state != null)
            RueDisplay.Get(player.ReferenceHub).Show(state.FeedbackHintTag, element, 5f);
        else
            RueDisplay.Get(player.ReferenceHub).Show(element, 5f);
#else
        if (PlayerAbilityState.TryGet(player, out var state) && state != null)
        {
            if (state.FeedbackHint != null)
            {
                state.FeedbackHint.Hide = false;
                state.FeedbackHint.Text = text;
                state.FeedbackHint.HideAfter(5);
                return;
            }

            var hint = new Hint { Text = text };
            hint.HideAfter(5);
            state.FeedbackHint = hint;
            PlayerDisplay.Get(player).AddHint(hint, HintGroupName);
        }
        else
        {
            var hint = new Hint { Text = text };
            PlayerDisplay.Get(player).ShowHint(hint, 5);
        }
#endif
    }

    private static void ShowFailHint(Player player, string message)
    {
        ShowFeedbackHint(player, $"<color=red>{message}</color>");
    }

    private static void ShowSuccessHint(Player player, string message)
    {
        ShowFeedbackHint(player, $"<color=green>{message}</color>");
    }

    private static void CleanupPlayer(Player player)
    {
        if (!PlayerAbilityState.TryGet(player, out var state) || state == null)
            return;

        if (state.SpawnedSchematic != null)
            try
            {
                state.SpawnedSchematic.Destroy();
                state.SpawnedSchematic = null;
            }
            catch (Exception ex)
            {
                LogManager.Error($"Error destroying schematic\n{ex}");
            }

        if (state.ActiveAudioPlayer != null)
        {
            AudioPlayerPool.Return(state.ActiveAudioPlayer);
            state.ActiveAudioPlayer = null;
        }

#if RueI
        try
        {
            var display = RueDisplay.Get(player.ReferenceHub);
            display.Remove(state.AbilityHintTag);
            display.Remove(state.FeedbackHintTag);
        }
        catch
        {
            /* ignored — player may have disconnected */
        }
#else
        if (state.AbilityHint != null)
        {
            try
            {
                PlayerDisplay.Get(player).RemoveHint(state.AbilityHint, HintGroupName);
            }
            catch
            {
                /* ignored — player may have disconnected */
            }

            state.AbilityHint = null;
        }

        if (state.FeedbackHint != null)
        {
            try
            {
                PlayerDisplay.Get(player).RemoveHint(state.FeedbackHint, HintGroupName);
            }
            catch
            {
                /* ignored — player may have disconnected */
            }

            state.FeedbackHint = null;
        }
#endif

        var schematicConfig = state.SchematicConfig;
        if (schematicConfig?.HideCarrierModel == true)
            try
            {
                player.DisableEffect<Fade>();
            }
            catch
            {
                /* ignored */
            }

        state.SetAnimationLock(false);

        PlayerAbilityState.Remove(player);

        LogManager.Debug($"Cleaned up state for player {player.Nickname}.");
    }

    internal sealed class VanillaRoleBinding
    {
        internal VanillaRoleBinding(IReadOnlyList<AbilityBase> abilities, SpeakerSettings? speakerSettings)
        {
            Abilities = abilities;
            SpeakerSettings = speakerSettings;
        }

        internal IReadOnlyList<AbilityBase> Abilities { get; }
        internal SpeakerSettings? SpeakerSettings { get; }
    }
}