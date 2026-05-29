using System.Collections.Generic;
using LabApi.Features.Wrappers;
using RoleAPI.API.Abilities;
using RoleAPI.ApiFeatures;
using SecretAPI.Features.UserSettings;

namespace RoleAPI.Internal.Settings;

internal sealed class AbilityKeybindSetting : CustomKeybindSetting
{
    private static readonly CustomHeader AbilitiesHeader = new("Abilities", hint: "Configure your ability keybinds");

    private static readonly Dictionary<AbilityBase, int> AbilityIdMap = new();
    private static int _nextSettingId = 49000;

    private readonly AbilityBase _ability;

    internal AbilityKeybindSetting(AbilityBase ability)
        : base(
            GetOrCreateId(ability),
            ability.Name,
            ability.DefaultKey,
            true,
            false,
            ability.Description)
    {
        _ability = ability;
        SettingId = Base.SettingId;
    }

    private AbilityKeybindSetting(AbilityBase ability, int existingId)
        : base(
            existingId,
            ability.Name,
            ability.DefaultKey,
            true,
            false,
            ability.Description)
    {
        _ability = ability;
        SettingId = existingId;
    }

    internal int SettingId { get; }

    public override CustomHeader Header => AbilitiesHeader;

    protected override bool CanView(Player player)
    {
        return PlayerAbilityState.TryGet(player, out var state) && state != null && state.HasAbility(_ability);
    }

    protected override void HandleSettingUpdate()
    {
        if (!IsPressed || KnownOwner == null)
            return;
        LogManager.Debug($"Player {KnownOwner.Nickname} triggered ability {_ability.Name} via keybind.");
        AbilityManager.TryExecuteAbility(KnownOwner, _ability);
    }

    protected override CustomSetting CreateDuplicate()
    {
        return new AbilityKeybindSetting(_ability, SettingId);
    }

    private static int GetOrCreateId(AbilityBase ability)
    {
        if (!AbilityIdMap.TryGetValue(ability, out var id))
        {
            id = _nextSettingId++;
            AbilityIdMap[ability] = id;
        }

        return id;
    }
}