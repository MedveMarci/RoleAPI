#if !RueI
using HintServiceMeow.Core.Enum;
#endif

namespace RoleAPI;

public class RoleApiConfig
{
    public bool Debug { get; set; } = false;

#if !RueI
    public float HintY { get; set; } = 750f;
    public float HintX { get; set; } = -850f;
    public int HintFontSize { get; set; } = 18;
    public HintAlignment Alignment { get; set; } = HintAlignment.Right;
#else
    public float AbilityHintPosition { get; set; } = 800f;
    public float FeedbackHintPosition { get; set; } = 700f;
#endif

    public string HudTitle { get; set; } = "<b>── Abilities ──</b>";
    public string StatusReady { get; set; } = "<color=green>Ready</color>";
    public string StatusBusy { get; set; } = "<color=orange>Busy</color>";

    public string StatusCooldown { get; set; } = "<color=yellow>{0:F1}s</color>";
    public string StatusExhausted { get; set; } = "<color=red>No uses left</color>";
    public string StatusConditionFailed { get; set; } = "<color=orange>{0}</color>";
    public string UsesRemaining { get; set; } = "<size=14>Uses: {0}/{1}</size>";

    public string SSSettingsNote { get; set; } =
        "<size=13><color=grey>Keybinds: Server-Specific Settings</color></size>";

    public string FeedbackNoUses { get; set; } = "{0}: No uses remaining.";
    public string FeedbackCooldown { get; set; } = "{0}: {1:F1}s";
    public string FeedbackBusy { get; set; } = "Cannot use abilities while an animation is running.";
    public string FeedbackSoundNotFound { get; set; } = "Sound file not found: {0}";
}
