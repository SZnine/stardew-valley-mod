using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace Sznine.BehaviorAutomation;

public static class WorldTargets
{
    internal static bool IsSprinklerObject(SObject obj) => obj.IsSprinkler()
        // A few content packs preserve the vanilla item number while changing
        // the qualified namespace from object to big-craftable.
        || obj.QualifiedItemId is "(O)599" or "(O)621" or "(O)645"
            or "(BC)599" or "(BC)621" or "(BC)645";
    private static bool Same(GameLocation map, Cell p, object obj) => obj switch
    {
        TerrainFeature t => map.terrainFeatures.TryGetValue(p.Tile, out var found) && ReferenceEquals(t, found),
        SObject o => map.Objects.TryGetValue(p.Tile, out var found) && ReferenceEquals(o, found),
        _ => false
    };
    public static IEnumerable<ResourceClump> Clumps(GameLocation map) => map.resourceClumps;
    private static HoeDirt? Soil(object entity) => entity as HoeDirt ?? (entity as IndoorPot)?.hoeDirt.Value;
    internal static bool HarvestReady(Crop crop) => !crop.dead.Value && (crop.forageCrop.Value
        ? crop.whichForageCrop.Value == "1"
        : crop.phaseDays.Count > 0 && crop.currentPhase.Value >= crop.phaseDays.Count - 1
            && (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0));
    private static bool Harvestable(HoeDirt soil, ToolMode mode, Tool? tool) => soil.crop is { } crop
        && HarvestReady(crop) && soil.readyForHarvest()
        && (mode == ToolMode.Scythe || crop.GetHarvestMethod() == HarvestMethod.Grab);
    private static bool FloorCovered(GameLocation map, Cell at) => map.Objects.ContainsKey(at.Tile)
        || map.buildings.Any(b => b.occupiesTile(at.Tile))
        || map.furniture.Any(f => f.GetBoundingBox().Intersects(new Rectangle(at.X * 64, at.Y * 64, 64, 64)));
    private static bool Dry(HoeDirt soil) => soil.crop is not null && !soil.crop.dead.Value && soil.state.Value != 1 && soil.needsWatering();
    private static ActionKind? Classify(object entity, ToolMode mode, Tool? tool)
    {
        if (entity is Pet pet)
            return mode == ToolMode.Hand && (!pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out int day) || day != Game1.Date.TotalDays) ? ActionKind.Pet : null;
        if (entity is PetBowl bowl)
            return mode == ToolMode.WateringCan && !bowl.watered.Value ? ActionKind.WaterBowl : null;
        if (entity is FarmAnimal animal)
        {
            if (mode == ToolMode.Hand && !animal.wasPet.Value)
                return ActionKind.Pet;
            if (mode is ToolMode.MilkPail or ToolMode.Shears && tool is not null && animal.isAdult() && animal.currentProduce.Value is not null && animal.CanGetProduceWithTool(tool))
                return mode == ToolMode.MilkPail ? ActionKind.Milk : ActionKind.Shear;
            return null;
        }
        if (Soil(entity) is { } soil)
        {
            if (mode == ToolMode.WateringCan && Dry(soil))
                return ActionKind.Water;
            if (mode is ToolMode.Hand or ToolMode.Scythe && Harvestable(soil, mode, tool))
                return ActionKind.HarvestCrop;
            if (mode == ToolMode.Scythe && soil.crop?.dead.Value == true)
                return ActionKind.DeadCrop;
            return null;
        }
        if (entity is Tree moss && mode == ToolMode.Scythe && moss.growthStage.Value >= 5 && moss.hasMoss.Value && moss.health.Value > -99)
            return ActionKind.Forage;
        if (entity is Tree tree && mode == ToolMode.Axe && !tree.tapped.Value && tree.health.Value > -99)
            return tree.stump.Value ? ActionKind.TreeStump : tree.growthStage.Value >= 5 ? ActionKind.WildTree : ActionKind.Sapling;
        if (entity is FruitTree fruit)
            return mode is ToolMode.Hand or ToolMode.Scythe && fruit.fruit.Count > 0 ? ActionKind.Fruit : mode == ToolMode.Axe ? ActionKind.FruitTree : null;
        if (entity is Bush bush && mode is ToolMode.Hand or ToolMode.Scythe && bush.readyForHarvest() && bush.inBloom())
            return ActionKind.Bush;
        if (entity is Flooring && mode is ToolMode.Axe or ToolMode.Pickaxe)
            return ActionKind.RemoveFloor;
        if (entity is Grass && mode == ToolMode.Scythe)
            return ActionKind.Grass;
        if (entity is ResourceClump clump)
        {
            if (clump is GiantCrop)
                return mode == ToolMode.Axe ? ActionKind.LargeWood : null;
            if (clump.IsGreenRainBush() && mode is ToolMode.Scythe or ToolMode.Axe or ToolMode.Pickaxe)
                return ActionKind.Weed;
            if (mode == ToolMode.Axe && (clump.parentSheetIndex.Value is 600 or 602 || clump.IsGreenRainBush()))
                return ActionKind.LargeWood;
            if (mode == ToolMode.Pickaxe && clump.parentSheetIndex.Value is 148 or 622 or 672 or 752 or 754 or 756 or 758)
                return ActionKind.LargeRock;
            return null;
        }
        if (entity is SObject obj && obj is not Chest && obj is not Fence)
        {
            // Sprinklers are normal objects in the 1.6 item registry, but they
            // must never enter the hand/forage pool. Touching one is not a
            // useful automated action and can trap a watering session when a
            // compatibility mod makes the tile passable.
            if (IsSprinklerObject(obj))
                return null;
            if (mode == ToolMode.Hand && AnimalCare.Grabber(obj) is { } grabber && !grabber.isEmpty())
                return ActionKind.Machine;
            if (mode == ToolMode.Pickaxe && (obj.IsBreakableStone() || obj.Name.Contains("Boulder", StringComparison.Ordinal)))
                return ActionKind.Stone;
            if (mode == ToolMode.Axe && obj.Name.Contains("Twig", StringComparison.Ordinal))
                return ActionKind.Twig;
            if (mode is ToolMode.Axe or ToolMode.Pickaxe or ToolMode.Scythe && obj.IsWeeds())
                return ActionKind.Weed;
            if (mode == ToolMode.Hoe && obj.QualifiedItemId is "(O)590" or "(O)SeedSpot")
                return ActionKind.Artifact;
            if (mode is ToolMode.Hand or ToolMode.Scythe)
            {
                // Litter can inherit CanBeGrabbed=true from Object construction; it still needs a tool.
                if (obj.IsBreakableStone() || obj.IsTwig() || obj.IsWeeds() || obj.Name.Contains("Boulder", StringComparison.Ordinal) || obj.QualifiedItemId is "(O)590" or "(O)SeedSpot")
                    return null;
                if (mode == ToolMode.Hand && obj.bigCraftable.Value && obj.readyForHarvest.Value && obj.heldObject.Value is not null)
                    return ActionKind.Machine;
                // Match GameLocation.checkAction's ground-pickup branch. CanBeGrabbed
                // defaults to true, and isForage describes item categories, not world state.
                // Crafting/interactive objects dispatch their own action before that branch.
                if (!obj.bigCraftable.Value && obj.isSpawnedObject.Value && obj.Type is not ("Crafting" or "interactive"))
                    return ActionKind.Forage;
            }
        }
        return null;
    }
    public static List<WorkTarget> Scan(GameLocation map, Farmer who, ToolMode mode, Tool? tool, Rectangle area, ModConfig config, bool includeTill = true, WorkScope scope = WorkScope.Held)
    {
        var targets = new List<WorkTarget>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Add(object entity, Cell origin, Rectangle bounds)
        {
            if (!area.Intersects(bounds) || !seen.Add(entity))
                return;
            if (entity is SObject { QualifiedItemId: "(O)178" } && map is AnimalHouse && map.doesTileHaveProperty(origin.X, origin.Y, "Trough", "Back") is not null)
                return;
            // Removing the floor beneath a placed object is ambiguous and cannot use the native tool safely.
            if (entity is Flooring && FloorCovered(map, origin))
                return;
            var kind = Classify(entity, mode, tool);
            if (kind is null || !config.Allows(kind.Value, scope))
                return;
            targets.Add(new()
            {
                Scope = scope,
                Mode = mode,
                Kind = kind.Value,
                Entity = entity,
                Origin = origin,
                Area = bounds,
                Tool = tool
            });
        }
        foreach (var pair in map.terrainFeatures.Pairs)
        {
            var p = Cell.At(pair.Key);
            Add(pair.Value, p, new(p.X, p.Y, 1, 1));
        }
        foreach (var pair in map.Objects.Pairs)
        {
            var p = Cell.At(pair.Key);
            Add(pair.Value, p, new(p.X, p.Y, 1, 1));
        }
        foreach (var feature in map.largeTerrainFeatures)
        {
            var p = Cell.At(feature.Tile);
            var b = feature.getBoundingBox();
            Add(feature, p, new(b.X / 64, b.Y / 64, Math.Max(1, b.Width / 64), Math.Max(1, b.Height / 64)));
        }
        foreach (var clump in Clumps(map))
        {
            var p = Cell.At(clump.Tile);
            Add(clump, p, new(p.X, p.Y, clump.width.Value, clump.height.Value));
        }
        foreach (var animal in map.animals.Values.Cast<Character>().Concat(map.characters.OfType<Pet>()))
        {
            var b = AnimalSelectionBounds(animal);
            var p = Cell.At(animal.GetBoundingBox().Center.ToVector2() / 64);
            Add(animal, p, new(b.Left / 64, b.Top / 64, Math.Max(1, (b.Right - 1) / 64 - b.Left / 64 + 1), Math.Max(1, (b.Bottom - 1) / 64 - b.Top / 64 + 1)));
        }
        if (mode == ToolMode.WateringCan)
            foreach (var bowl in map.buildings.OfType<PetBowl>())
                for (int y = bowl.tileY.Value; y < bowl.tileY.Value + bowl.tilesHigh.Value; y++)
                    for (int x = bowl.tileX.Value; x < bowl.tileX.Value + bowl.tilesWide.Value; x++)
                    {
                        string property = null!;
                        if (bowl.doesTileHaveProperty(x, y, "PetBowl", "Buildings", ref property))
                            Add(bowl, new(x, y), new(x, y, 1, 1));
                    }
        if (includeTill && mode == ToolMode.Hand && config.Allows(ActionKind.Feed, scope) && map is AnimalHouse house)
            for (int y = area.Top; y < area.Bottom; y++)
                for (int x = area.Left; x < area.Right; x++)
                {
                    var p = new Cell(x, y);
                    if (AnimalCare.EmptyTrough(map, p))
                        targets.Add(new()
                        {
                            Scope = scope,
                            Mode = mode,
                            Kind = ActionKind.Feed,
                            Entity = new FeedSpot(house, p),
                            Origin = p,
                            Area = new(x, y, 1, 1)
                        });
                }
        if (includeTill && mode == ToolMode.Hoe && config.Allows(ActionKind.Till, scope))
            for (int y = area.Top; y < area.Bottom; y++)
                for (int x = area.Left; x < area.Right; x++)
                {
                    var p = new Cell(x, y);
                    if (Tillable(map, p))
                        targets.Add(new()
                        {
                            Scope = scope,
                            Mode = mode,
                            Kind = ActionKind.Till,
                            Entity = p,
                            Origin = p,
                            Area = new(x, y, 1, 1),
                            Tool = tool
                        });
                }
        return targets;
    }
    public static bool Tillable(GameLocation map, Cell p) => !map.terrainFeatures.ContainsKey(p.Tile) && !map.Objects.ContainsKey(p.Tile)
        && map.doesTileHaveProperty(p.X, p.Y, "Diggable", "Back") is not null && map.isTileLocationOpen(p.Tile) && map.doesTileHaveProperty(p.X, p.Y, "NoDig", "Back") is null;
    public static Rectangle AnimalSelectionBounds(Character animal)
    {
        var body = animal.GetBoundingBox();
        var sprite = animal.Sprite;
        var visual = new Rectangle((int)animal.Position.X, (int)animal.Position.Y - 24, sprite.SpriteWidth * 4, sprite.SpriteHeight * 4);
        return Rectangle.Union(body, visual);
    }
    public static bool NeedsGather(WorkTarget t, Tool tool) => t.Kind is ActionKind.Fruit or ActionKind.Bush
        || t.Kind == ActionKind.Forage && t.Entity is SObject
        || t.Kind == ActionKind.HarvestCrop && Soil(t.Entity)?.crop?.GetHarvestMethod() == HarvestMethod.Grab && tool.QualifiedItemId != "(W)66";
    public static void Gather(GameLocation map, Farmer who, WorkTarget target)
    {
        // Recheck at the point of consumption, including crops already harvested earlier in this swing.
        if (Soil(target.Entity) is { } current && !Harvestable(current, target.Mode, target.Tool))
            return;
        switch (target.Entity)
        {
            case FruitTree fruit:
                fruit.performUseAction(target.Origin.Tile);
                break;
            case Bush bush:
                bush.performUseAction(target.Origin.Tile);
                break;
            case HoeDirt soil:
                Harvest(soil);
                break;
            case IndoorPot pot:
                Harvest(pot.hoeDirt.Value);
                break;
            default:
                // This is the pickup after an already-completed swing. A still-held scythe
                // lets Harvest With Scythe convert checkAction into another, unowned swing.
                // Native stowing hides the held item without creating a temporary inventory hole
                // which the pickup could fill and then lose when the tool is restored.
                bool stowed = who.netItemStowed.Value;
                try
                {
                    who.netItemStowed.Value = true;
                    who.UpdateItemStow();
                    map.checkAction(new xTile.Dimensions.Location(target.Origin.X, target.Origin.Y), Game1.viewport, who);
                }
                finally { who.netItemStowed.Value = stowed; who.UpdateItemStow(); }
                break;
        }
        void Harvest(HoeDirt soil)
        {
            if (soil.crop?.harvest(target.Origin.X, target.Origin.Y, soil, null, isForcedScytheHarvest: true) == true)
            {
                if (map is IslandLocation && Game1.random.NextDouble() < .05)
                    who.team.RequestLimitedNutDrops("IslandFarming", map, target.Origin.X * 64, target.Origin.Y * 64, 5);
                soil.destroyCrop(showAnimation: true);
            }
        }
    }
    public static bool Pending(GameLocation map, WorkTarget target, ModConfig config)
    {
        if (target.Entity is BuildingDoor door)
            return door.Exit ? map == door.Inside : config.WorkInsideBuildings && map == door.Outside
                && map.buildings.Contains(door.Building) && door.Building.daysOfConstructionLeft.Value <= 0
                && ReferenceEquals(door.Building.GetIndoors(), door.Inside);
        if (!config.Allows(target.Kind, target.Scope))
            return false;
        if (target.Entity is Flooring && FloorCovered(map, target.Origin))
            return false;
        if (target.Entity is WaterSource water)
            return water.Can.WaterLeft <= 0 && map.CanRefillWateringCanOnTile(water.Tile.X, water.Tile.Y);
        if (target.Entity is FeedSpot feed)
            return feed.House == map && AnimalCare.EmptyTrough(map, feed.Cell);
        if (target.Material is { } seed)
            return target.Mode == ToolMode.Place ? Placement.CanPlace(map, seed, target.Origin) : Planting.OpenSpot(map, seed, target.Origin);
        bool exists = target.Entity switch
        {
            FarmAnimal a => map.animals.TryGetValue(a.myID.Value, out var found) && ReferenceEquals(a, found),
            Pet p => map.characters.Contains(p),
            PetBowl b => map.buildings.Contains(b),
            ResourceClump c => Clumps(map).Contains(c),
            LargeTerrainFeature l => map.largeTerrainFeatures.Contains(l),
            Cell p => Tillable(map, p),
            _ => Same(map, target.Origin, target.Entity)
        };
        if (!exists)
            return false;
        if (target.Kind == ActionKind.Till)
            return true;
        var kind = Classify(target.Entity, target.Mode, target.Tool);
        return kind == target.Kind && config.Allows(kind.Value, target.Scope);
    }
    public static string? Unable(WorkTarget target, Farmer who, ModConfig config)
    {
        if (target.Material is { } seed && Placement.FindStock(who, seed) is null)
            return target.Mode == ToolMode.Place ? "materials" : "seeds";
        if (target.Kind == ActionKind.Feed && !AnimalCare.HasHay(who))
            return "hay";
        if (target.Entity is FarmAnimal && target.Kind == ActionKind.Pet && Game1.timeOfDay >= 1900)
            return "sleeping";
        if (target.Tool is { } tool && !who.Items.Contains(tool))
        {
            if (!config.UseStoredTools || target.Storage?.Available != true)
                return "missing-tool";
            if (ToolStorage.EmptySlot(who) < 0)
                return "empty-slot";
        }
        if (!ToolRequirements.Allows(target.Entity, target.Tool))
            return "upgrade";
        if (target.Entity is not WaterSource && target.Mode == ToolMode.WateringCan && target.Tool is WateringCan can && can.WaterLeft <= 0 && !who.hasWateringCanEnchantment)
            return "water";
        float cost = target.Entity is WaterSource ? 0 : Cost(target.Tool, who);
        if (cost > 0 && who.Stamina - cost < config.ReserveStamina)
            return "stamina";
        if (target.Entity is FarmAnimal animal && target.Kind is ActionKind.Milk or ActionKind.Shear && animal.currentProduce.Value is { } id)
        {
            var item = ItemRegistry.Create<SObject>("(O)" + id, animal.hasEatenAnimalCracker.Value ? 2 : 1, animal.produceQuality.Value);
            if (!who.couldInventoryAcceptThisItem(item))
                return "inventory";
        }
        if (target.Kind == ActionKind.Forage && target.Entity is SObject forage && !who.couldInventoryAcceptThisItem(forage))
            return "inventory";
        if (target.Kind == ActionKind.Machine && target.Entity is SObject machine)
        {
            if (AnimalCare.Grabber(machine) is not null)
                return AnimalCare.GrabberBlocked(machine, who);
            if (machine.heldObject.Value is { } output && !who.couldInventoryAcceptThisItem(output))
                return "inventory";
        }
        return null;
    }
    public static float Cost(Tool? tool, Farmer who) => tool switch
    {
        MilkPail or Shears => 4,
        null or MeleeWeapon => 0,
        _ when tool.IsEfficient => 0,
        Pickaxe => Math.Max(0, 2 - who.MiningLevel * .1f),
        Axe => Math.Max(0, 2 - who.ForagingLevel * .1f),
        _ => Math.Max(0, 2 - who.FarmingLevel * .1f)
    };
    public static bool CanStand(GameLocation map, Farmer who, Cell p)
    {
        if (map.Map is null || p.X < 0 || p.Y < 0 || p.X >= map.Map.Layers[0].LayerWidth || p.Y >= map.Map.Layers[0].LayerHeight)
            return false;
        if (map.warps.Any(w => w.X == p.X && w.Y == p.Y) || map.doesTileHaveProperty(p.X, p.Y, "TouchAction", "Back") is not null)
            return false;
        using var probe = PassabilityProbe.Enter();
        return !map.isCollidingPosition(new Rectangle(p.X * 64 + 8, p.Y * 64 + 16, 48, 32), Game1.viewport, true, 0, false, who, pathfinding: true, skipCollisionEffects: true);
    }
    public static double Progress(WorkTarget target) => target.Entity switch
    {
        WaterSource w => w.Can.WaterLeft,
        FarmAnimal a => target.Kind == ActionKind.Pet ? (a.wasPet.Value ? 1 : 0) : a.currentProduce.Value is null ? 1 : 0,
        Pet p => p.lastPetDay.GetValueOrDefault(Game1.player.UniqueMultiplayerID, -1),
        Tree t => t.health.Value + (t.stump.Value ? 0 : 10000),
        FruitTree t => target.Kind == ActionKind.Fruit ? t.fruit.Count : t.health.Value,
        ResourceClump c => c.health.Value,
        Grass g => g.numberOfWeeds.Value,
        HoeDirt s => CropProgress(s),
        IndoorPot p => CropProgress(p.hoeDirt.Value),
        SObject o => o.MinutesUntilReady,
        _ => 0
    };
    private static double CropProgress(HoeDirt soil) => soil.crop is { } crop
        ? crop.currentPhase.Value * 1000 + crop.dayOfCurrentPhase.Value + (crop.fullyGrown.Value ? 100000 : 0)
        : -1;
}
