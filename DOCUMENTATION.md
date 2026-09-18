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
    // ...or ship the audio inside your DLL (see "Embedded Audio" below):
    // public override string? SoundResource => "Audio/blink.ogg";

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

        // Optional: override the sound for this specific execution
        // ctx.SoundFile = "special_blink.ogg";
        // ctx.SoundResource = "Audio/special_blink.ogg";

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
| `SoundResource`        | `string?`                         | `null`     | Embedded audio resource in the ability's assembly |
| `Sound`                | `AbilityAudio?`                   | `null`     | Explicit audio source (file or embedded resource) |
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
| `SoundFile`             | `string?`              | Set to override the default sound with a file                 |
| `SoundResource`         | `string?`              | Set to override the default sound with an embedded resource   |
| `Sound`                 | `AbilityAudio?`        | Set to override the default sound with any audio source       |
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

| Status         | Meaning                         |
|----------------|---------------------------------|
| `✓ Ready`     | Ability is available            |
| `⏳ Xs`        | On cooldown, ready in X seconds |
| `✗ Exhausted` | No uses remaining               |
| `⚠ Message`   | Condition not met               |
| `⟳ Busy`       | Animation lock is active        |

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

## Embedded Audio (`AbilityAudio`, `EmbeddedResources`)

Audio does not have to sit next to the configs on disk: a plugin can embed its audio files into its own DLL, so there is
nothing for server owners to copy.

### 1. Embed the files

Put the files in your project (e.g. `Audio/`) and add them to your csproj:

```xml
<ItemGroup>
    <EmbeddedResource Include="Audio\*.ogg">
        <LogicalName>MyPlugin.Audio.%(Filename)%(Extension)</LogicalName>
    </EmbeddedResource>
</ItemGroup>
```

> **Note:** the file type still needs a reader installed on the server (e.g. the `SecretLabNAudio.NVorbis` module for
> `.ogg`), exactly like file-based audio.

### 2. Play them

```csharp
public class YippeeAbility : AbilityBase
{
    // Default audio for the ability, read from this ability's own assembly
    public override string? SoundResource => "Audio/yippee.ogg";

    protected override void OnExecute(AbilityExecutionContext ctx)
    {
        // ...or pick one per execution
        ctx.SoundResource = $"Audio/yippee{Random.Range(1, 4)}.ogg";

        // Explicit form, e.g. for a resource in another assembly
        // ctx.Sound = AbilityAudio.Embedded("Audio/yippee.ogg", typeof(MyPlugin).Assembly);
        // ctx.Sound = AbilityAudio.File(Path.Combine(dir, "yippee.ogg"));
    }
}
```

Resolution order for an ability's default audio: **`Sound`** → **`SoundResource`** → **`SoundFile`**. Anything set on
the context overrides all three.

### Resource names

Names are matched against the assembly's manifest resource names in this order: exact, case-insensitive, then by
trailing segments — so `"beep.ogg"`, `"Audio/beep.ogg"` and `"MyPlugin.Audio.beep.ogg"` all resolve
`MyPlugin.Audio.beep.ogg`. `/` and `\` are treated as `.`. An ambiguous name logs a warning and uses the first match.

Resource contents are cached per assembly after the first read, so a repeatedly used sound is only read from the
manifest once.

### Letting server owners replace embedded audio

Register a directory and any file in it replaces the embedded resource with the same file name — so the plugin works out
of the box, but the sounds stay customizable:

```csharp
// In your plugin's Enable()
AbilityAudio.SetOverrideDirectory(Path.Combine(PathManager.Configs.FullName, "MyPlugin", "Audio"));

// In your plugin's Disable()
AbilityAudio.SetOverrideDirectory(null);
```

With the above, `Audio/beep.ogg` (embedded as `MyPlugin.Audio.beep.ogg`) is played from
`configs/MyPlugin/Audio/beep.ogg` when that file exists, and from the DLL when it does not. Only the plain file name is
matched, folders inside the resource name are ignored. The check runs on every use, so files can be swapped without a
server restart.

The override directory applies to the assembly that registered it, so plugins never overwrite each other's audio. Use
the `SetOverrideDirectory(directory, assembly)` overload to register it on behalf of another assembly.

### Non-audio resources

`RoleAPI.API.Resources.EmbeddedResources` exposes the same lookup for any embedded file. Every method defaults to the
calling assembly.

| Member                                             | Description                                         |
|----------------------------------------------------|-----------------------------------------------------|
| `GetNames(assembly?)`                              | All manifest resource names                         |
| `Exists(name, assembly?)`                          | Whether a resource matches the name                 |
| `TryResolveName(name, out fullName, assembly?)`    | Resolves a name to its full manifest name           |
| `GetBytes(name, assembly?)`                        | Resource contents (cached)                          |
| `OpenStream(name, assembly?)`                      | Seekable stream over the resource (caller disposes) |
| `GetText(name, assembly?)`                         | Resource contents as text                           |
| `ExtractToFile(name, path, overwrite?, assembly?)` | Writes the resource to disk                         |
| `ClearCache(assembly?)`                            | Drops cached contents and name lookups              |

`ExtractToFile` is useful for assets another plugin can only load from a path (schematics, configs):

```csharp
EmbeddedResources.ExtractToFile("Schematics/MyRole.json",
    Path.Combine(PathManager.Configs.FullName, "MyPlugin", "MyRole.json"));
```

### `AbilityAudio`

| Member                             | Description                                                   |
|------------------------------------|---------------------------------------------------------------|
| `AbilityAudio.File(path)`          | Audio read from a file on disk                                |
| `AbilityAudio.Embedded(name)`      | Audio read from the calling assembly's resources              |
| `AbilityAudio.Embedded(name, asm)` | Audio read from the given assembly's resources                |
| `SetOverrideDirectory(dir[, asm])` | Directory whose files replace that assembly's embedded audio  |
| `GetOverrideDirectory(asm)`        | The override directory registered for an assembly             |
| `Identifier`                       | File path, or resource name when embedded                     |
| `FileName`                         | Plain file name, the name looked up in the override directory |
| `IsEmbedded`                       | Whether the audio comes from an embedded resource             |
| `Assembly`                         | Source assembly, or `null` for files                          |
| `Exists()`                         | Whether the file, override file or resource can be found      |
| `ResolveOverrideFile()`            | Path of the file replacing this embedded audio, or `null`     |

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
| `SoundResource`        | Embedded audio resource name              |
| `Sound`                | `AbilityAudio` source (file or embedded)  |
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
| `SoundFile`            | Override sound with a file path                         |
| `SoundResource`        | Override sound with an embedded resource name           |
| `Sound`                | Override sound with an `AbilityAudio` source            |
| `ActivationHint`       | Success hint shown after execution                      |
| `LocksDuringExecution` | Enable animation lock                                   |
| `AutoReleaseLock`      | Auto-release lock when audio ends                       |
| `Deny(reason)`         | Cancel with message                                     |
| `PlayAnimation(name)`  | Play animation on the schematic                         |
| `IsAnimationPlaying()` | `true` while animation is running                       |
| `CompleteAnimation()`  | Release lock manually                                   |
