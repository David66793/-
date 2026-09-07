using System;
using System.Collections.Generic;
using System.Text;

namespace Hearthhold.Core
{
    public sealed class Battle
    {
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<Unit> Units = new List<Unit>();
        public readonly List<CombatEffect> Effects = new List<CombatEffect>();
        public readonly List<string> Commands = new List<string>();
        public readonly int[] Available = new int[4];
        public readonly Building[,] Occupied = new Building[Rules.MapSize, Rules.MapSize];
        public int TickNumber, Revision, Mission, SpellCharges = 2, NextUnitId = 1;
        public bool Started, Finished, Settled;
        public int GoldReward, CrystalReward;
        public Battle(int mission) : this(Missions.Create(mission), mission) { }
        public Battle(List<Building> buildings, int mission)
        {
            Mission = mission;
            foreach (Building original in buildings) { Building b = original.Copy(); b.Cooldown = 0; Buildings.Add(b); }
            for (int i = 0; i < Available.Length; i++) Available[i] = Rules.Troops[i].Count;
            RebuildGrid();
        }
        public int SecondsLeft { get { return Math.Max(0, 180 - TickNumber / Rules.TicksPerSecond); } }
        public int Destruction
        {
            get
            {
                int total = 0, destroyed = 0;
                foreach (Building b in Buildings) if (b.Kind != BuildingKind.Wall) { total++; if (b.Health <= 0) destroyed++; }
                return total == 0 ? 100 : destroyed * 100 / total;
            }
        }
        public int Stars
        {
            get
            {
                int stars = Destruction >= 50 ? 1 : 0;
                foreach (Building b in Buildings) if (b.Kind == BuildingKind.Keep && b.Health <= 0) { stars++; break; }
                if (Destruction == 100) stars++;
                return stars;
            }
        }
        public int AliveCount { get { int count = 0; foreach (Unit u in Units) if (u.Health > 0) count++; return count; } }
        public bool CanDeploy(int x, int z)
        {
            if (Finished || x < 500 || z < 500 || x >= 39500 || z >= 39500) return false;
            // A fixed, clearly rendered outer deployment band keeps scouting predictable.
            if (x > 10500 && x < 30500 && z > 10500 && z < 31500) return false;
            return Occupied[x / 1000, z / 1000] == null;
        }
        public bool Deploy(TroopKind kind, int x, int z)
        {
            if ((int)kind < 0 || (int)kind >= Available.Length || Available[(int)kind] <= 0 || !CanDeploy(x, z)) return false;
            x = x / 1000 * 1000 + 500; z = z / 1000 * 1000 + 500;
            if (!CanDeploy(x, z)) return false;
            Available[(int)kind]--;
            Units.Add(new Unit { Id = NextUnitId++, Kind = kind, X = x, Z = z, Health = Rules.Spec(kind).Health });
            Commands.Add(TickNumber + ":deploy:" + (int)kind + ":" + x + ":" + z);
            Started = true;
            return true;
        }
        public bool CastHeal(int x, int z)
        {
            if (!Started || Finished || SpellCharges <= 0 || x < 0 || z < 0 || x >= 40000 || z >= 40000) return false;
            SpellCharges--;
            foreach (Unit u in Units)
                if (u.Health > 0 && Distance(u.X, u.Z, x, z) <= 5000L * 5000)
                    u.Health = Math.Min(u.Spec.Health, u.Health + u.Spec.Health * 2 / 3);
            Effects.Add(new CombatEffect(x, z, x, z, 24, 3));
            Commands.Add(TickNumber + ":heal:" + x + ":" + z);
            return true;
        }
        public void Step()
        {
            if (!Started || Finished) return;
            TickNumber++;
            for (int i = Effects.Count - 1; i >= 0; i--) if (--Effects[i].Ticks <= 0) Effects.RemoveAt(i);
            foreach (Unit u in Units) if (u.Health > 0) StepUnit(u);
            foreach (Building b in Buildings)
            {
                if (b.Health <= 0 || b.Spec.Damage == 0) continue;
                if (b.Cooldown > 0) { b.Cooldown--; continue; }
                Unit target = null; long nearest = long.MaxValue;
                foreach (Unit u in Units)
                {
                    if (u.Health <= 0) continue;
                    long distance = Distance(b.CenterX, b.CenterZ, u.X, u.Z);
                    if (distance <= (long)b.Spec.Range * b.Spec.Range && distance < nearest) { nearest = distance; target = u; }
                }
                if (target != null)
                {
                    target.Health = Math.Max(0, target.Health - b.Spec.Damage * (b.Level + 1) / 2);
                    b.Cooldown = b.Spec.Cooldown;
                    Effects.Add(new CombatEffect(b.CenterX, b.CenterZ, target.X, target.Z, 5, 1));
                }
            }
            bool reserves = false; foreach (int count in Available) if (count > 0) reserves = true;
            if (Destruction >= 100 || SecondsLeft == 0 || (!reserves && AliveCount == 0)) Finish();
        }
        private void StepUnit(Unit u)
        {
            if (u.Cooldown > 0) u.Cooldown--;
            Building target = FindById(u.TargetId);
            if (target == null || target.Health <= 0)
            {
                target = SelectTarget(u);
                if (target == null) return;
                u.TargetId = target.Id; u.PathRevision = -1;
            }
            if (target.DistanceSquared(u.X, u.Z) <= (long)u.Spec.Range * u.Spec.Range)
            { Attack(u, target); return; }
            if (u.PathRevision != Revision || u.PathIndex >= u.Path.Count && TickNumber >= u.RepathTick)
            {
                u.Path = Pathfinder.Find(Occupied, u.X / 1000, u.Z / 1000, target, u.Spec.Range, u.Kind == TroopKind.Sapper);
                u.PathIndex = 0; u.PathRevision = Revision; u.RepathTick = TickNumber + 20;
            }
            if (u.PathIndex >= u.Path.Count) return;
            Cell cell = u.Path[u.PathIndex];
            Building obstacle = Occupied[cell.X, cell.Z];
            if (obstacle != null && obstacle.Health > 0)
            {
                if (obstacle.Kind == BuildingKind.Wall && obstacle.DistanceSquared(u.X, u.Z) <= 1200L * 1200)
                { Attack(u, obstacle); return; }
                if (obstacle.Kind != BuildingKind.Wall) { u.PathRevision = -1; return; }
            }
            int px = cell.X * 1000 + 500, pz = cell.Z * 1000 + 500;
            long dx = px - u.X, dz = pz - u.Z;
            int length = IntegerSqrt(dx * dx + dz * dz);
            if (length <= u.Spec.Speed) { u.X = px; u.Z = pz; u.PathIndex++; }
            else { u.X += (int)(dx * u.Spec.Speed / length); u.Z += (int)(dz * u.Spec.Speed / length); }
        }
        private Building SelectTarget(Unit u)
        {
            Building best = null; long bestScore = long.MaxValue;
            foreach (Building b in Buildings)
            {
                if (b.Health <= 0) continue;
                long score = b.DistanceSquared(u.X, u.Z);
                if (b.Kind == BuildingKind.Wall && u.Kind != TroopKind.Sapper) score += 20000000000L;
                if (u.Kind == TroopKind.Guardian && b.Spec.Damage == 0) score += 10000000000L;
                if (u.Kind == TroopKind.Sapper && b.Kind != BuildingKind.Wall) score += 10000000000L;
                if (score < bestScore) { bestScore = score; best = b; }
            }
            return best;
        }
        private void Attack(Unit u, Building b)
        {
            if (u.Cooldown > 0) return;
            int damage = u.Spec.Damage;
            if (u.Kind == TroopKind.Sapper && b.Kind == BuildingKind.Wall) damage *= 10;
            b.Health = Math.Max(0, b.Health - damage);
            u.Cooldown = u.Spec.Cooldown;
            Effects.Add(new CombatEffect(u.X, u.Z, b.CenterX, b.CenterZ, 5, u.Kind == TroopKind.Ranger ? 0 : 2));
            if (b.Health == 0)
            {
                Effects.Add(new CombatEffect(b.CenterX, b.CenterZ, b.CenterX, b.CenterZ, 22, 4));
                Revision++; RebuildGrid();
            }
        }
        private void RebuildGrid()
        {
            Array.Clear(Occupied, 0, Occupied.Length);
            foreach (Building b in Buildings) if (b.Health > 0)
                for (int x = b.X; x < b.X + b.Spec.Size; x++)
                    for (int z = b.Z; z < b.Z + b.Spec.Size; z++) Occupied[x, z] = b;
        }
        private Building FindById(int id) { foreach (Building b in Buildings) if (b.Id == id) return b; return null; }
        public void Finish()
        {
            if (Finished) return;
            Finished = true;
            GoldReward = Destruction * (4 + Mission * 2) + Stars * 100;
            CrystalReward = Destruction * (2 + Mission) + Stars * 40;
        }
        public string StateFingerprint()
        {
            StringBuilder s = new StringBuilder();
            s.Append(TickNumber).Append('|').Append(Finished).Append('|').Append(SpellCharges);
            foreach (int count in Available) s.Append('|').Append(count);
            foreach (Building b in Buildings) s.Append(';').Append(b.Id).Append(',').Append(b.Health).Append(',').Append(b.Cooldown);
            foreach (Unit u in Units) s.Append(';').Append(u.Id).Append(',').Append(u.X).Append(',').Append(u.Z).Append(',').Append(u.Health);
            return s.ToString();
        }
        private static long Distance(int x1, int z1, int x2, int z2) { long x = x1 - x2, z = z1 - z2; return x * x + z * z; }
        private static int IntegerSqrt(long value)
        {
            long lo = 0, hi = Math.Min(value, 1000000);
            while (lo < hi) { long mid = (lo + hi + 1) / 2; if (mid * mid <= value) lo = mid; else hi = mid - 1; }
            return (int)lo;
        }
    }
}
