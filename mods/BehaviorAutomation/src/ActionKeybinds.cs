using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace Sznine.BehaviorAutomation;

public sealed partial class ActionMenu
{
    private string? bindingKey;
    private readonly HashSet<SButton> bindingButtons = new();
    public bool CapturingBinding => bindingKey is not null;
    private KeybindList BindingValue(string key) => key switch
    {
        "config.select" => config.SelectKey,
        "config.cancel" => config.CancelKey,
        _ => config.ActionMenuKey
    };
    private static KeybindList DefaultBinding(string key) => KeybindList.Parse(key == "config.menu" ? "F8" : "LeftShift, RightShift");
    private void SetBinding(string key, KeybindList value)
    {
        switch (key)
        {
            case "config.select": config.SelectKey = value; break;
            case "config.cancel": config.CancelKey = value; break;
            default: config.ActionMenuKey = value; break;
        }
    }
    private void BeginBinding(string key) { bindingKey = key; bindingButtons.Clear(); hover = ""; }
    private void StopBinding() { bindingKey = null; bindingButtons.Clear(); }

    /// <summary>Collect one chord and commit when released, before normal menu hotkeys run.</summary>
    public void CaptureBinding(IEnumerable<SButton> pressed, IEnumerable<SButton> held, Action<SButton> suppress)
    {
        if (bindingKey is null) return;
        var fresh = pressed.ToArray();
        var down = fresh.Concat(held).ToHashSet();
        foreach (var button in down) suppress(button);
        if (fresh.Contains(SButton.Escape)) { StopBinding(); return; }
        bool Allowed(SButton b) => b is not (SButton.None or SButton.MouseLeft or SButton.MouseRight);
        if (fresh.Any(Allowed)) bindingButtons.UnionWith(down.Where(Allowed));
        if (bindingButtons.Count == 0 || bindingButtons.Any(down.Contains)) return;
        var binding = KeybindList.Parse(string.Join(" + ", bindingButtons.OrderBy(b => b)));
        SetBinding(bindingKey, binding);
        StopBinding();
        Save();
    }
}
