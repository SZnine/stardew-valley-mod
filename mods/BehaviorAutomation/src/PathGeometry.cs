namespace Sznine.BehaviorAutomation;

/// <summary>Costs use tenths of a tile. Diagonals never cut through either blocked corner.</summary>
internal static class PathGeometry
{
    internal static readonly Cell[] Neighbors = Cell.Directions.Concat(new Cell[]
        { new(1, -1), new(1, 1), new(-1, 1), new(-1, -1) }).ToArray();
    internal static bool Diagonal(Cell delta) => delta.X != 0 && delta.Y != 0;
    internal static bool OpenCorner(Cell at, Cell delta, Func<Cell, bool> canWalk)
        => !Diagonal(delta) || canWalk(at.Add(new(delta.X, 0))) && canWalk(at.Add(new(0, delta.Y)));
    internal static int Cost(Cell delta) => Diagonal(delta) ? 14 : 10;
    internal static double Distance(Cell a, Cell b)
    {
        int x = Math.Abs(a.X - b.X), y = Math.Abs(a.Y - b.Y);
        return Math.Max(x, y) + .4 * Math.Min(x, y);
    }
}
