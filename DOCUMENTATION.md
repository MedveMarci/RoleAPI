## Quick Start

### 1. Register Your Role

```csharp
// In your plugin's Enable():
RoleAPI.RoleAPI.RegisterRole(new MyCustomRole());
```

```csharp
// In your plugin's Disable():
RoleAPI.RoleAPI.UnregisterRole(myRoleInstance);
```

---

## Creating a Custom Role

Extend `UcrRoleBase` (which extends UCR's `EventCustomRole`):

```csharp
using System.Collections.Generic;
using PlayerRoles;
using RoleAPI.API.Abilities;
using RoleAPI.API.Roles;
using RoleAPI.API.Schematics;
using SecretLabNAudio.Core;

public class MyCustomRole : UcrRoleBase
{
    // --- UCR required properties ---
    public override int Id { get; set; } = 100;
    public override string Name { get; set; } = "Custodian";
    public override RoleTypeId Role { get; set; } = RoleTypeId.ClassD;

    // --- RoleAPI properties ---

    /// Default speaker settings for all abilities (can be overridden per ability)
    public override SpeakerSettings? DefaultSpeakerSettings { get; } =
        new SpeakerSettings(IsSpatial: true, Volume: 1f, MinDistance: 5f, MaxDistance: 30f);

    /// The schematic attached to players with this role
    public override RoleSchematic? Schematic { get; } = new RoleSchematic
    {
        Name = "custodian_model",          // ProjectMER schematic name
        PositionOffset = Vector3.zero,
        RotationOffset = Vector3.zero,
        HideCarrierModel = true,           // Applies Fade(255) effect, hiding the player model
    };

    /// Abilities available to this role
    public override IReadOnlyList<AbilityBase> Abilities { get; } = new List<AbilityBase>
    {
        new TeleportAbility(),
        new HealAbility(),
    };
}
```

---

## Creating an Ability

Extend `AbilityBase`:

```csharp
using System.Collections.Generic;
using RoleAPI.API.Abilities;
using UnityEngine;

public class TeleportAbility : AbilityBase
{
    public override string Name => "Blink";
    public override string Description => "Teleport forward 5 metres.";

    public override int MaxUses => 3;           // 3 uses per life, -1 = unlimited
    public override float Cooldown => 8f;       // 8 second cooldown
    public override KeyCode DefaultKey => KeyCode.F;  // Default keybind (player can rebind)
    public override string? SoundFile => "blink.ogg"; // Optional sound file path

    // Override speaker settings just for this ability (null = use role default)
    public override SpeakerSettings? SpeakerSettings => null;

    // Optional: conditions that must pass before the ability runs
    public override IReadOnlyList<AbilityCondition> Conditions { get; } = new List<AbilityCondition>
    {
        new FullStaminaCondition(),
    };

    protected override void OnExecute(AbilityExecutionContext ctx)
    {
        var player = ctx.Player;

        // Optional: deny execution with a message (won't consume use/cooldown)
        if (player.Health < 30f)
        {
            ctx.Deny("You need at least 30 HP to blink.");
            return;
        }

        // Optional: override the sound file for this specific execution
        // ctx.SoundFile = "special_blink.ogg";

        // Optional: lock other abilities while this animation plays
        // ctx.LocksDuringExecution = true;
        // ... call ctx.CompleteAnimation() when done

        // Implement ability logic:
        var forward = player.GameObject.transform.forward;
        player.Position += forward * 5f;
    }
}
```

### Ability Properties Reference

| Property               | Type                              | Default    | Description                                       |
|------------------------|-----------------------------------|------------|---------------------------------------------------|
| `Name`                 | `string`                          | *required* | Display name in the ability HUD                   |
| `Description`          | `string`                          | *required* | Shown as SSS keybind description                  |
| `MaxUses`              | `int`                             | `-1`       | Max uses per life. `-1` = unlimited               |
| `Cooldown`             | `float`                           | `0`        | Cooldown in seconds between uses                  |
| `SoundFile`            | `string?`                         | `null`     | Path to audio file played on use                  |
| `SpeakerSettings`      | `SpeakerSettings?`                | `null`     | Overrides role's `DefaultSpeakerSettings`         |
| `LocksDuringExecution` | `bool`                            | `true`     | Blocks other abilities while audio plays          |
| `AutoReleaseLock`      | `bool`                            | `true`     | Auto-releases lock when audio ends                |
| `DefaultKey`           | `KeyCode`                         | `None`     | Suggested keybind (player can rebind in SSS menu) |
| `Conditions`           | `IReadOnlyList<AbilityCondition>` | empty      | Pre-use condition checks                          |

---

## Creating a Condition

```csharp
using LabApi.Features.Wrappers;
using RoleAPI.API.Abilities;

public class FullStaminaCondition : AbilityCondition
{
    public override string FailureMessage => "Need: Full Stamina";

    public override bool IsMet(Player player)
    {
        return player.Stamina >= 1f;
    }
}
```

You can also create inline conditions without a separate class:

```csharp
AbilityCondition.Create(p => p.Health >= 50f, "Need: 50 HP")
```

Conditions are checked **before** `OnExecute` is called. If any condition fails, the player sees the `FailureMessage` as
a hint and the ability is blocked (no use/cooldown consumed).

The ability HUD also shows the failure message inline when the condition is not met.

---

## Execution Context (`AbilityExecutionContext`)

The context passed to `OnExecute` provides:

| Member                  | Type                   | Description                                                   |
|-------------------------|------------------------|---------------------------------------------------------------|
| `Player`                | `Player`               | The player using the ability                                  |
| `AudioPlayer`           | `AudioPlayer?`         | Pre-allocated audio player                                    |
| `RoleSchematic`         | `SchematicObject?`     | The active schematic for this player                          |
| `Animator`              | `AnimationController?` | Animation controller for the schematic                        |
| `SoundFile`             | `string?`              | Set to override the default sound                             |
| `ActivationHint`        | `string?`              | Success hint shown to the player after execution              |
| `LocksDuringExecution`  | `bool`                 | If `true`, blocks other abilities until `CompleteAnimation()` |
| `AutoReleaseLock`       | `bool`                 | If `true`, releases the lock automatically when audio ends    |
| `IsDenied`              | `bool`                 | Whether `Deny()` has been called                              |
| `DenialReason`          | `string?`              | The denial message                                            |
| `Deny(string)`          | method                 | Cancel execution with a message                               |
| `PlayAnimation(string)` | method                 | Play an animation on the schematic's animator                 |
| `IsAnimationPlaying()`  | method                 | Returns `true` while the animation is in progress             |
| `CompleteAnimation()`   | method                 | Release the animation lock manually                           |

### Animation Lock Example

```csharp
protected override void OnExecute(AbilityExecutionContext ctx)
{
    ctx.LocksDuringExecution = true;  // Block other abilities
    ctx.SoundFile = "long_animation.ogg";  // Lock auto-releases when audio ends

    // If no SoundFile is set with LocksDuringExecution, you must call:
    // ctx.CompleteAnimation();  // somewhere later (e.g., in a coroutine)

    // ... do stuff
}
```

> **Note:** If `LocksDuringExecution = true` and a `SoundFile` is set, the lock is **automatically released** when the
> audio ends. If no sound file is used, you must call `ctx.CompleteAnimation()` manually.

---

## Schematic Configuration (`RoleSchematic`)

```csharp
public override RoleSchematic? Schematic { get; } = new RoleSchematic
{
    Name = "my_schematic",               // Required: ProjectMER schematic name
    PositionOffset = Vector3.zero,       // Position offset relative to player
    RotationOffset = Vector3.zero,       // Euler rotation offset relative to player facing
    HideCarrierModel = true,             // Applies Fade(255), hiding the player model
};
```

The schematic is parented to the player's transform on spawn so it follows their movement.

Set `Schematic = null` (or don't override) for no schematic.

---

## Ability HUD

Players automatically see a persistent hint showing all their abilities:

```
── Abilities ──
[F] Blink  ✓ Ready
   Uses: 3/3
[G] Heal  ⏳ 4.2s
[H] Shield  ⚠ Need: Full Stamina
```

| Status        | Meaning                         |
|---------------|---------------------------------|
| `✓ Ready`     | Ability is available            |
| `⏳ Xs`        | On cooldown, ready in X seconds |
| `✗ Exhausted` | No uses remaining               |
| `⚠ Message`   | Condition not met               |
| `⟳ Busy`      | Animation lock is active        |

The HUD updates dynamically via the configured hint backend.

---

## Audio / Speaker Settings

Speaker settings control how 3D audio is played:

```csharp
// Globally audible (non-spatial, everyone hears it)
public override SpeakerSettings? DefaultSpeakerSettings { get; } =
    SpeakerSettings.GloballyAudible;

// Spatial (3D sound, heard by nearby players)
public override SpeakerSettings? DefaultSpeakerSettings { get; } =
    new SpeakerSettings(IsSpatial: true, Volume: 1f, MinDistance: 5f, MaxDistance: 40f);
```

Priority: **ability `SpeakerSettings`** → **role `DefaultSpeakerSettings`** → **`SpeakerSettings.Default`**

AudioPlayers are pooled via `AudioPlayerPool` (backed by `SpeakerToyPool`).

---

## Keybind System

Ability keybinds use **SecretAPI's Server-Specific Settings (SSS)** system:

- Each ability gets one SSS keybind setting registered globally
- Only players **currently assigned the ability** see that keybind in their settings menu
- Players can rebind keys from the in-game settings menu
- Settings are automatically refreshed when a player gets/loses a UCR role

---

## Lifecycle

| Event                                     | What Happens                                                           |
|-------------------------------------------|------------------------------------------------------------------------|
| `UcrRoleBase.OnSpawned()`                 | Schematic spawned, hint added, SSS keybinds sent                       |
| Player dies (`PlayerDying`)               | Schematic destroyed, hint removed, effects cleared, keybinds refreshed |
| Player changes role (`PlayerChangedRole`) | Same cleanup if player had a UcrRoleBase role                          |
| Player disconnects (`PlayerLeft`)         | Full cleanup                                                           |

---

## Full Example Plugin

```csharp
using System.Collections.Generic;
using System.Reflection;
using LabApi.Loader.Features.Plugins;
using PlayerRoles;
using RoleAPI.API.Abilities;
using RoleAPI.API.Roles;
using RoleAPI.API.Schematics;
using UnityEngine;

public class MyPlugin : Plugin
{
    public override string Name => "MyPlugin";
    public override string Author => "Me";
    public override System.Version Version => Assembly.GetExecutingAssembly().GetName().Version;
    public override System.Version RequiredApiVersion => LabApi.Features.LabApiProperties.CompiledVersion;

    private static readonly MyRole RoleInstance = new();

    public override void Enable()
    {
        RoleAPI.RoleAPI.RegisterRole(RoleInstance);
    }

    public override void Disable()
    {
        RoleAPI.RoleAPI.UnregisterRole(RoleInstance);
    }
}

// ── Role definition ──────────────────────────────────────────────

public class MyRole : UcrRoleBase
{
    public override int Id { get; set; } = 200;
    public override string Name { get; set; } = "Shadow Agent";
    public override PlayerRoles.RoleTypeId Role { get; set; } = RoleTypeId.NtfCaptain;

    public override RoleSchematic? Schematic { get; } = new RoleSchematic
    {
        Name = "shadow_agent",
        HideCarrierModel = true,
    };

    public override IReadOnlyList<AbilityBase> Abilities { get; } = new List<AbilityBase>
    {
        new ShadowStepAbility(),
    };
}

// ── Ability definition ───────────────────────────────────────────

public class ShadowStepAbility : AbilityBase
{
    public override string Name => "Shadow Step";
    public override string Description => "Dash through shadows, becoming invisible briefly.";

    public override int MaxUses => -1;
    public override float Cooldown => 12f;
    public override KeyCode DefaultKey => KeyCode.F;
    public override string? SoundFile => "shadow_dash.ogg";

    protected override void OnExecute(AbilityExecutionContext ctx)
    {
        ctx.Player.EnableEffect<CustomPlayerEffects.Invisible>(255, 2f);
        ctx.Player.Position += ctx.Player.GameObject.transform.forward * 6f;
    }
}
```

---

## API Reference Summary

### `RoleAPI` (static)

| Method                                                | Description                           |
|-------------------------------------------------------|---------------------------------------|
| `RegisterRole(UcrRoleBase)`                           | Registers a role with UCR and RoleAPI |
| `UnregisterRole(UcrRoleBase)`                         | Unregisters a role from both          |
| `BindToRole(RoleTypeId, abilities, speakerSettings?)` | Binds abilities to a vanilla role     |
| `UnbindFromRole(RoleTypeId)`                          | Removes a vanilla role binding        |
| `GiveAbility(Player, AbilityBase)`                    | Gives a single ability to a player    |
| `RemoveAbility(Player, AbilityBase)`                  | Removes an ability from a player      |
| `Roles`                                               | All currently registered UCR roles    |

### `UcrRoleBase`

| Member                   | Description                               |
|--------------------------|-------------------------------------------|
| `DefaultSpeakerSettings` | Default audio settings for abilities      |
| `Schematic`              | Schematic config (or `null`)              |
| `Abilities`              | List of abilities **(required override)** |

### `AbilityBase`

| Member                 | Description                               |
|------------------------|-------------------------------------------|
| `Name`                 | Display name **(required override)**      |
| `Description`          | Keybind hint text **(required override)** |
| `MaxUses`              | `-1` = unlimited                          |
| `Cooldown`             | Seconds between uses                      |
| `SoundFile`            | Audio file path                           |
| `SpeakerSettings`      | Audio speaker override                    |
| `LocksDuringExecution` | Block other abilities while audio plays   |
| `AutoReleaseLock`      | Auto-release lock when audio ends         |
| `DefaultKey`           | Suggested keybind                         |
| `Conditions`           | Pre-use condition list                    |
| `OnExecute(ctx)`       | Ability logic **(required override)**     |

### `AbilityCondition`

| Member                       | Description                                        |
|------------------------------|----------------------------------------------------|
| `FailureMessage`             | Shown when condition fails **(required override)** |
| `IsMet(player)`              | Return `true` to allow **(required override)**     |
| `Create(predicate, message)` | Create an inline condition without a subclass      |

### `RoleSchematic`

| Property           | Type      | Description                                |
|--------------------|-----------|--------------------------------------------|
| `Name`             | `string`  | ProjectMER schematic name                  |
| `PositionOffset`   | `Vector3` | Offset from player position                |
| `RotationOffset`   | `Vector3` | Euler rotation offset relative to player   |
| `HideCarrierModel` | `bool`    | Apply `Fade(255)` to hide the player model |

### `AbilityExecutionContext`

| Member                 | Description                                             |
|------------------------|---------------------------------------------------------|
| `Player`               | The executing player                                    |
| `AudioPlayer`          | Allocated audio player                                  |
| `RoleSchematic`        | Active `SchematicObject` (may be `null`)                |
| `Animator`             | `AnimationController` for the schematic (may be `null`) |
| `SoundFile`            | Override sound file                                     |
| `ActivationHint`       | Success hint shown after execution                      |
| `LocksDuringExecution` | Enable animation lock                                   |
| `AutoReleaseLock`      | Auto-release lock when audio ends                       |
| `Deny(reason)`         | Cancel with message                                     |
| `PlayAnimation(name)`  | Play animation on the schematic                         |
| `IsAnimationPlaying()` | `true` while animation is running                       |
| `CompleteAnimation()`  | Release lock manually                                   |
