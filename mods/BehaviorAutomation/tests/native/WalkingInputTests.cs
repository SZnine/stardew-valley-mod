using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Tools;
using Sznine.BehaviorAutomation;

namespace BehaviorProbe;

public sealed partial class ModEntry
{
    private void WalkingInputTests()
    {
        Check("real native idle input reproduced stride restarts and scoped fix preserves the walk cycle", () =>
        {
            byte oldMode = Game1.gameMode;
            var evidence = new List<object>();
            try
            {
                Game1.gameMode = 3;
                var input = AccessTools.Method(typeof(Game1), "UpdateControlInput");
                var frameIndex = AccessTools.Field(typeof(AnimatedSprite), "currentAnimationIndex");
                var distinct = new List<int>();
                foreach (bool repaired in new[] { false, true })
                {
                    Reset();
                    Game1.oldKBState = new KeyboardState();
                    Game1.oldMouseState = new MouseState();
                    SetButtons(CursorAtTile(20, 20));
                    Who.setRunning(true, true);
                    CropAt(50, 20);
                    SelectModern(new WateringCan { WaterLeft = 20 }, new(50, 20, 1, 1));
                    Until(() => Who.controller is WalkRoute);
                    PathFindController route = Who.controller!;
                    if (!repaired)
                        Who.controller = route = new LegacyWalkRoute(Who, Map, Enumerable.Range(21, 28).Select(x => new Cell(x, 20)).ToList());
                    var samples = new List<object>();
                    var phases = new HashSet<int>();
                    int stopped = 0;
                    for (int i = 0; i < 80; i++)
                    {
                        var time = new GameTime(Game1.currentGameTime.TotalGameTime + TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
                        Game1.currentGameTime = time;
                        input.Invoke(Game1.game1, new object[] { time });
                        if (Who.movementDirections.Count == 0)
                            stopped++;
                        route.update(time);
                        Who.updateMovementAnimation(time);
                        int phase = (int)frameIndex.GetValue(Who.FarmerSprite)!;
                        phases.Add(phase);
                        samples.Add(new
                        {
                            phase,
                            frame = Who.FarmerSprite.CurrentFrame,
                            x = Who.Position.X,
                            running = Who.running
                        });
                    }
                    distinct.Add(phases.Count);
                    evidence.Add(new
                    {
                        repaired,
                        phases = phases.Count,
                        stopped,
                        samples
                    });
                }
                File.WriteAllText(Path.Combine(Evidence, "stride-native-input.json"), System.Text.Json.JsonSerializer.Serialize(evidence));
                Assert(distinct[0] <= 2 && distinct[1] >= 4, $"Native input regression not resolved: old={distinct[0]}, new={distinct[1]}");
            }
            finally { Game1.gameMode = oldMode; }
        });
        Check("stride guard never blocks explicit stops or menu and manual movement takeover", () =>
        {
            CropAt(40, 20);
            SelectModern(new WateringCan { WaterLeft = 20 }, new(40, 20, 1, 1));
            Until(() => Control.OwnsWalking(Who));
            Who.Halt();
            Assert(Who.movementDirections.Count == 0, "Explicit Halt outside input blocked");
            Control.ObserveMovement(Who, true, 16);
            Assert(Who.controller is null && !Control.OwnsWalking(Who), "Manual movement captured");
            Who.SetMovingLeft(true);
            Who.Halt();
            Assert(Who.movementDirections.Count == 0, "Manual stop intercepted");
        });
    }
}
