using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;
using BehaviorMod = Sznine.BehaviorAutomation.ModEntry;
using SObject = StardewValley.Object;
namespace BehaviorProbe;
public sealed partial class ModEntry
{
    private void Modern()
    {
    }
    private void SelectModern(Tool? tool, Rectangle area, bool smart = false)
    {
        if (tool is not null && !Who.Items.Contains(tool))
            Who.Items[Enumerable.Range(0, 36).First(i => Who.Items[i] is null)] = tool;
        Who.CurrentToolIndex = tool is null ? Enumerable.Range(0, 36).First(i => Who.Items[i] is null) : Who.Items.IndexOf(tool);
        Control.Board.Select(Map, Who, ModeInfo.From(tool) ?? ToolMode.Hand, tool, area, Config, smart: smart);
        Control.Resume();
    }
    private void SmartSelect(Item? held, Rectangle area)
    {
        if (held is not null && !Who.Items.Contains(held))
            Who.Items[Enumerable.Range(0, 36).First(i => Who.Items[i] is null)] = held;
        Who.CurrentToolIndex = held is null ? Enumerable.Range(0, 36).First(i => Who.Items[i] is null) : Who.Items.IndexOf(held);
        Control.Board.Select(Map, Who, ModeInfo.From(held) ?? ToolMode.Auto, held as Tool, area, Config, smart: true);
        Control.Resume();
    }
    private FarmAnimal CareAnimal(string type, long id, int x, int y, string? produce = null)
    {
        var animal = new FarmAnimal(type, id, Who.UniqueMultiplayerID) { Position = new(x * 64, y * 64), currentLocation = Map };
        animal.age.Value = 30;
        animal.wasPet.Value = false;
        animal.currentProduce.Value = produce;
        Map.animals.Add(id, animal);
        return animal;
    }
    private Chest Store(Tool tool, int x = 18, int y = 18)
    {
        var chest = new Chest(true) { TileLocation = new(x, y) };
        Map.Objects[chest.TileLocation] = chest;
        chest.GetItemsForPlayer(Who.UniqueMultiplayerID).Add(tool);
        return chest;
    }
    private static ICursorPosition CursorAtTile(int x, int y)
    {
        var abs = new Vector2(x * 64 + 32, y * 64 + 32);
        var screen = abs - new Vector2(Game1.viewport.X, Game1.viewport.Y);
        return (ICursorPosition)Activator.CreateInstance(typeof(Mod).Assembly.GetType("StardewModdingAPI.Framework.CursorPosition")!, abs, screen, new Vector2(x, y), new Vector2(20, 20))!;
    }
    private void Buttons(ICursorPosition cursor, params SButton[] keys)
    {
        SetButtons(cursor, keys);
        RaiseInput("ButtonsChanged", InputArgs(typeof(ButtonsChangedEventArgs), cursor, Game1.input));
    }
    private void ModTick() => AccessTools.Method(typeof(BehaviorMod), "Tick").Invoke(Behavior, new object?[] { null, null });
    private DragSelection? Drag => ((PerScreen<DragSelection?>)AccessTools.Field(typeof(BehaviorMod), "drag").GetValue(Behavior)!).Value;
    private void DragWith(SButton button, int x, int y, int right, int bottom)
    {
        var from = CursorAtTile(x, y);
        Buttons(from, SButton.LeftShift, button);
        RaiseInput("ButtonPressed", InputArgs(typeof(ButtonPressedEventArgs), button, from, Game1.input));
        Assert(Drag?.Button == button && Helper.Input.IsSuppressed(button), "Drag button not captured");
        var end = CursorAtTile(right, bottom);
        Buttons(end, SButton.LeftShift);
        ModTick();
        Buttons(end);
        ModTick();
    }
    private void InHouse(Action<AnimalHouse, GameLocation> test)
    {
        var outside = Map;
        var house = new AnimalHouse("Maps/Farm", "BehaviorCareHouse");
        house.Map = outside.Map;
        Game1.locations.Add(house);
        Game1.currentLocation = house;
        Who.currentLocation = house;
        var tile = house.Map.GetLayer("Back").Tiles[23, 20];
        tile.Properties["Trough"] = "T";
        try
        {
            test(house, outside);
        }
        finally { Control.Clear(); tile.Properties.Remove("Trough"); Game1.locations.Remove(house); Game1.currentLocation = outside; Who.currentLocation = outside; }
    }
}
