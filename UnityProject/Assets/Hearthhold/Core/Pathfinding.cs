using System;
using System.Collections.Generic;

namespace Hearthhold.Core
{
    // Integer-cost A*. Destructible walls carry a traversal cost; units must actually
    // destroy a wall before taking the corresponding step. Ranged goals use footprints.
    public static class Pathfinder
    {
        private static readonly int[] DX = { 1, 0, -1, 0 };
        private static readonly int[] DZ = { 0, 1, 0, -1 };
        public static List<Cell> Find(Building[,] occupied, int sx, int sz, Building target, int range, bool sapper)
        {
            int n = Rules.MapSize, count = n * n;
            int[] costs = new int[count], parents = new int[count];
            bool[] closed = new bool[count];
            for (int i = 0; i < count; i++) { costs[i] = int.MaxValue; parents[i] = -1; }
            sx = Math.Max(0, Math.Min(n - 1, sx)); sz = Math.Max(0, Math.Min(n - 1, sz));
            int start = sx + sz * n;
            costs[start] = 0;
            List<int> open = new List<int>(); open.Add(start);
            int found = -1;
            while (open.Count > 0)
            {
                int bestIndex = 0, bestScore = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int id = open[i];
                    int score = costs[id] + Heuristic(id % n, id / n, target, range);
                    if (score < bestScore || (score == bestScore && id < open[bestIndex])) { bestIndex = i; bestScore = score; }
                }
                int current = open[bestIndex]; open.RemoveAt(bestIndex);
                if (closed[current]) continue;
                closed[current] = true;
                int cx = current % n, cz = current / n;
                if (target.DistanceSquared(cx * 1000 + 500, cz * 1000 + 500) <= (long)range * range)
                { found = current; break; }
                for (int d = 0; d < 4; d++)
                {
                    int x = cx + DX[d], z = cz + DZ[d];
                    if (x < 0 || z < 0 || x >= n || z >= n) continue;
                    int id = x + z * n;
                    if (closed[id]) continue;
                    Building block = occupied[x, z];
                    if (block != null && block.Health > 0 && block.Kind != BuildingKind.Wall) continue;
                    int step = 10;
                    if (block != null && block.Health > 0) step += sapper ? 12 : 65;
                    int candidate = costs[current] + step;
                    if (candidate < costs[id]) { costs[id] = candidate; parents[id] = current; open.Add(id); }
                }
            }
            List<Cell> result = new List<Cell>();
            if (found < 0) return result;
            while (found != start && found >= 0) { result.Add(new Cell(found % n, found / n)); found = parents[found]; }
            result.Reverse();
            return result;
        }
        private static int Heuristic(int x, int z, Building target, int range)
        {
            int dx = Math.Max(target.X - x, Math.Max(0, x - target.X - target.Spec.Size));
            int dz = Math.Max(target.Z - z, Math.Max(0, z - target.Z - target.Spec.Size));
            return Math.Max(0, dx + dz - (range + 999) / 1000 - 1) * 10;
        }
    }
}
