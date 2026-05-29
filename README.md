# RoleAPI

[![Version](https://img.shields.io/github/v/release/MedveMarci/RoleAPI?&label=Version&color=d500ff)](https://github.com/MedveMarci/RoleAPI/releases/latest) [![LabAPI Version](https://img.shields.io/badge/LabAPI_Version-1.1.6-b84ee87)](https://github.com/northwood-studios/LabAPI/releases/tag/1.1.6) [![SCP:SL Version](https://img.shields.io/badge/SCP:SL_Version-14.2.6-blue?&color=e5b200)](https://store.steampowered.com/app/700330/SCP_Secret_Laboratory/)

---

## Role ability framework for SCP: Secret Laboratory

RoleAPI is a developer library built on top of **UncomplicatedCustomRoles (UCR)** and **LabAPI**. It gives custom UCR roles:

- **Abilities** — activatable via per-player Server-Specific Settings keybinds
- **Persistent Hint UI** — persistent HUD showing ability names, cooldowns, and status
- **Schematics** — ProjectMER schematic attached to the player, following their movement
- **Pooled Audio** — SecretLabNAudio SpeakerToy pool for ability sound effects

---

## Dependencies

| Dependency | Required | Description |
|---|---|---|
| [UncomplicatedCustomRoles](https://github.com/UncomplicatedCustomServer/UncomplicatedCustomRoles) | Yes | Custom role framework RoleAPI builds on |
| [ProjectMER](https://github.com/Michal78900/ProjectMER) | Yes | Schematic spawning and management |
| [SecretLabNAudio](https://github.com/Axwabo/SecretLabNAudio) | Yes | Audio playback via pooled SpeakerToys |
| [SecretAPI](https://github.com/MedveMarci/SecretAPI) | Yes | Server-Specific Settings for ability keybinds |

**Hint backend — pick one DLL variant:**

| DLL variant | Hint plugin | Notes |
|---|---|---|
| `RoleAPI.dll` | [HintServiceMeow](https://github.com/MrAfitol/HintServiceMeow) | Recommended |
| `RoleAPI-RueI.dll` | [RueI](https://github.com/Semper-Viventem/RueI) | Alternative |

> - **Only install one RoleAPI DLL at a time.**
> - Every dependency's installation guide can be found in their respective GitHub READMEs.

---

## Installation (Server)

1. Download from the [latest release](https://github.com/MedveMarci/RoleAPI/releases/latest):
   - `RoleAPI.dll` *(HintServiceMeow)* **or** `RoleAPI-RueI.dll` *(RueI)* — pick one
2. Place the chosen DLL in `LabAPI/plugins/global/`.
3. Install all required dependencies listed above, including the matching hint plugin.
4. Start the server — RoleAPI loads automatically with high priority.

---

## Installation (Developers)

Add `RoleAPI.dll` as a reference in your plugin project:

```xml
<Reference Include="RoleAPI" HintPath="$(LABAPI_PLUGINS)\RoleAPI.dll"/>
```

---

## Quick Start

### 1. Register your role in your plugin

```csharp
public override void Enable()
{
    RoleAPI.RoleAPI.RegisterRole(new MyCustomRole());
}

public override void Disable()
{
    RoleAPI.RoleAPI.UnregisterRole(myRoleInstance);
}
```

### 2. Define the role

```csharp
public class MyCustomRole : UcrRoleBase
{
    public override int Id { get; set; } = 100;
    public override string Name { get; set; } = "Shadow Agent";
    public override RoleTypeId Role { get; set; } = RoleTypeId.NtfCaptain;

    public override RoleSchematic? Schematic { get; } = new RoleSchematic
    {
        Name = "shadow_agent",       // ProjectMER schematic name
        HideCarrierModel = true,     // Hides the default player model
    };

    public override IReadOnlyList<AbilityBase> Abilities { get; } = new List<AbilityBase>
    {
        new ShadowStepAbility(),
    };
}
```

### 3. Define the ability

```csharp
public class ShadowStepAbility : AbilityBase
{
    public override string Name => "Shadow Step";
    public override string Description => "Dash forward and briefly turn invisible.";

    public override float Cooldown => 12f;
    public override KeyCode DefaultKey => KeyCode.F;
    public override string? SoundFile => "shadow_dash.ogg";

    protected override void OnExecute(AbilityExecutionContext ctx)
    {
        ctx.Player.EnableEffect<Invisible>(255, 2f);
        ctx.Player.Position += ctx.Player.GameObject.transform.forward * 6f;
    }
}
```

See [DOCUMENTATION.md](DOCUMENTATION.md) for the full API reference.

---

## Credits

- Developed by **MedveMarci**
- Custom role framework [UncomplicatedCustomRoles](https://github.com/UncomplicatedCustomServer/UncomplicatedCustomRoles) by **UncomplicatedCustomServer**
- Map plugin [ProjectMER](https://github.com/Michal78900/ProjectMER) by **Michal78900**
- Audio plugin [SecretLabNAudio](https://github.com/Axwabo/SecretLabNAudio) by **Axwabo**
