using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private T Installed<T>(string id)
    {
        var info = Helper.ModRegistry.Get(id)!;
        return (T)info.GetType().GetProperty("Mod")!.GetValue(info)!;
    }

    private static ICursorPosition CursorAtUi(Vector2 uiPoint)
    {
        var screen = uiPoint * Game1.options.uiScale / Game1.options.zoomLevel;
        var type = typeof(Mod).Assembly.GetType("StardewModdingAPI.Framework.CursorPosition")!;
        return (ICursorPosition)Activator.CreateInstance(type, screen, screen, Vector2.Zero, Vector2.Zero)!;
    }

    private void RaiseInput(string name, object args)
    {
        object inputEvents = Helper.Events.Input;
        var manager = inputEvents.GetType().BaseType!.GetField("EventManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(inputEvents)!;
        object managed = manager.GetType().GetField(name)!.GetValue(manager)!;
        managed.GetType().GetMethod("Raise", new[] { args.GetType() })!.Invoke(managed, new[] { args });
    }

    private static object InputArgs(Type type, params object[] args) => Activator.CreateInstance(type,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)!;

    private static void SetButtons(ICursorPosition cursor, params SButton[] down)
    {
        object state = Game1.input;
        Type type = state.GetType();
        var buttons = (IDictionary)type.GetProperty("ButtonStates")!.GetValue(state)!;
        buttons.Clear();
        var valueType = buttons.GetType().GetGenericArguments()[1];
        foreach (SButton key in down)
            buttons[key] = Enum.Parse(valueType, "Pressed");
        ((HashSet<SButton>)type.GetField("CustomReleasedKeys", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state)!).Clear();
        ((HashSet<SButton>)type.GetField("CustomPressedKeys", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state)!).Clear();
        type.GetField("HasNewOverrides", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(state, false);
        type.GetField("CursorPositionImpl", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(state, cursor);
        var physical = cursor.ScreenPixels * Game1.options.zoomLevel;
        var mouse = new MouseState((int)physical.X, (int)physical.Y, 0,
            down.Contains(SButton.MouseLeft) ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, down.Contains(SButton.MouseRight) ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, ButtonState.Released);
        type.GetProperty("MouseState")!.SetValue(state, mouse);
        type.GetProperty("KeyboardState")!.SetValue(state, new KeyboardState(down.Where(b => (int)b < 1000).Select(b => (Keys)b).ToArray()));
    }

    private void Click(Vector2 point, SButton modifier = SButton.None, SButton mouse = SButton.MouseLeft)
    {
        var cursor = CursorAtUi(point);
        SetButtons(cursor, modifier == SButton.None ? new[] { mouse } : new[] { modifier, mouse });
        RaiseInput("ButtonPressed", InputArgs(typeof(ButtonPressedEventArgs), mouse, cursor, Game1.input));
    }

}
