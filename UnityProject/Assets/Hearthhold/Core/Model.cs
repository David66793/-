using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Hearthhold.Core
{
    public enum BuildingKind { Keep, Mine, Reservoir, Barracks, Cannon, Watchtower, Wall }
    public enum TroopKind { Vanguard, Ranger, Guardian, Sapper }

    public sealed class BuildingSpec
    {
        public string Name, Description;
        public int Size, Health, Cost, Damage, Range, Cooldown;
        public BuildingSpec(string name, string description, int size, int health, int cost, int damage, int range, int cooldown)
        { Name = name; Description = description; Size = size; Health = health; Cost = cost; Damage = damage; Range = range; Cooldown = cooldown; }
    }

    public sealed class TroopSpec
    {
        public string Name, Role;
        public int Health, Damage, Range, Speed, Cooldown, Count;
        public TroopSpec(string name, string role, int health, int damage, int range, int speed, int cooldown, int count)
        { Name = name; Role = role; Health = health; Damage = damage; Range = range; Speed = speed; Cooldown = cooldown; Count = count; }
        public string Description, Tactics, Weakness;
    }

    public static class Rules
    {
        public const int MapSize = 40;
        public const int Scale = 1000;
        public const int TicksPerSecond = 20;
        public static readonly BuildingSpec[] Buildings = {
            new BuildingSpec("议事堡", "聚落的中心。升级后提高其他建筑的等级上限。", 4, 2200, 0, 0, 0, 0),
            new BuildingSpec("金矿", "持续产出金币。点击收取，将离线收益收入仓库。", 3, 650, 180, 0, 0, 0),
            new BuildingSpec("晶露池", "收集用于建筑升级的晶露。", 3, 600, 160, 0, 0, 0),
            new BuildingSpec("远征营", "远征队的营地。原型提供四种预设兵种。", 3, 800, 240, 0, 0, 0),
            new BuildingSpec("重弩炮", "强力单体防御，擅长击退重甲目标。", 2, 850, 220, 38, 6500, 24),
            new BuildingSpec("哨塔", "视野开阔、射程更远的防御塔。", 2, 650, 200, 19, 8000, 15),
            new BuildingSpec("石墙", "阻挡地面部队，迫使对手绕行或破墙。", 1, 430, 20, 0, 0, 0)
        };
        public static readonly TroopSpec[] Troops = {
            new TroopSpec("先锋", "近战 · 均衡", 270, 42, 1050, 150, 15, 12),
            new TroopSpec("游侠", "远程 · 跨墙射击", 130, 32, 4500, 125, 17, 10),
            new TroopSpec("铁卫", "重甲 · 优先防御", 1300, 72, 1100, 85, 26, 3),
            new TroopSpec("破城手", "攻城 · 城墙特攻", 180, 36, 1100, 165, 18, 4)
        };
        public static BuildingSpec Spec(BuildingKind kind) { return Buildings[(int)kind]; }
        public static TroopSpec Spec(TroopKind kind) { return Troops[(int)kind]; }
        static Rules()
        {
            Troops[0].Description = "持剑近战步兵，攻击距离约1格。自动选择附近建筑，遇到挡路城墙会破墙。";
            Troops[0].Tactics = "在铁卫吸引火力、破城手打开缺口后成组投放，清理资源建筑。";
            Troops[0].Weakness = "不能隔墙攻击；单兵冲进防御塔火力区容易阵亡。";
            Troops[1].Description = "持弓远程射手，射程4.5格。可以隔着城墙攻击建筑，但自身生命较低。";
            Troops[1].Tactics = "放在铁卫和先锋后方；利用射程先打掉靠外的建筑。";
            Troops[1].Weakness = "生命只有130，别让游侠第一个吸引防御塔火力。";
            Troops[2].Description = "持盾重甲卫士，生命1300。优先攻击重弩炮、哨塔等防御建筑。";
            Troops[2].Tactics = "优先投放2—3名铁卫吸引火力，再跟进破城手和其他兵种。";
            Troops[2].Weakness = "移动慢、攻击间隔长。需要输出部队配合，不能只靠铁卫。";
            Troops[3].Description = "携带爆破器材的攻城手，优先攻击城墙；对城墙每次造成10倍伤害。";
            Troops[3].Tactics = "紧跟铁卫，从同一侧投下，打开缺口让近战部队进入基地。";
            Troops[3].Weakness = "生命较低；城墙拆完后的普通伤害有限，别作为主力输出。";
        }
        public static int BuildLimit(BuildingKind kind, int keepLevel)
        {
            int tier = Math.Max(1, Math.Min(3, keepLevel));
            switch (kind)
            {
                case BuildingKind.Keep: return 1;
                case BuildingKind.Mine: return 2 + tier;
                case BuildingKind.Reservoir: return 1 + tier;
                case BuildingKind.Barracks: return tier;
                case BuildingKind.Cannon: case BuildingKind.Watchtower: return 1 + tier;
                case BuildingKind.Wall: return 10 + tier * 30;
                default: return 0;
            }
        }
    }

    public sealed class Building
    {
        public int Id, X, Z, Level = 1, Health, Cooldown;
        public BuildingKind Kind;
        [XmlIgnore] public BuildingSpec Spec { get { return Rules.Spec(Kind); } }
        [XmlIgnore] public int MaxHealth { get { return Spec.Health * (Level + 1) / 2; } }
        [XmlIgnore] public int CenterX { get { return X * 1000 + Spec.Size * 500; } }
        [XmlIgnore] public int CenterZ { get { return Z * 1000 + Spec.Size * 500; } }
        public Building Copy() { return (Building)MemberwiseClone(); }
        public bool Contains(int x, int z) { return x >= X && z >= Z && x < X + Spec.Size && z < Z + Spec.Size; }
        public long DistanceSquared(int x, int z)
        {
            long dx = Math.Max(X * 1000 - x, Math.Max(0, x - (X + Spec.Size) * 1000));
            long dz = Math.Max(Z * 1000 - z, Math.Max(0, z - (Z + Spec.Size) * 1000));
            return dx * dx + dz * dz;
        }
    }

    public struct Cell
    {
        public int X, Z;
        public Cell(int x, int z) { X = x; Z = z; }
    }

    public sealed class Unit
    {
        public int Id, X, Z, Health, Cooldown, TargetId = -1, PathRevision = -1, RepathTick;
        public TroopKind Kind;
        public List<Cell> Path = new List<Cell>();
        public int PathIndex;
        public TroopSpec Spec { get { return Rules.Spec(Kind); } }
    }

    public sealed class CombatEffect
    {
        public int X, Z, EndX, EndZ, Ticks, Kind;
        public CombatEffect(int x, int z, int endX, int endZ, int ticks, int kind)
        { X = x; Z = z; EndX = endX; EndZ = endZ; Ticks = ticks; Kind = kind; }
    }

    public sealed class VillageData
    {
        public int Version = 1, Gold = 1600, Crystal = 700, NextId = 1, Wins;
        public long LastIncomeUtcTicks;
        public List<Building> Buildings = new List<Building>();
        public static VillageData Create()
        {
            VillageData v = new VillageData();
            v.LastIncomeUtcTicks = DateTime.UtcNow.AddMinutes(-2).Ticks;
            v.Add(BuildingKind.Keep, 18, 16);
            v.Add(BuildingKind.Mine, 12, 18);
            v.Add(BuildingKind.Mine, 26, 20);
            v.Add(BuildingKind.Reservoir, 23, 14);
            v.Add(BuildingKind.Barracks, 16, 23);
            v.Add(BuildingKind.Cannon, 15, 15);
            v.Add(BuildingKind.Watchtower, 23, 24);
            for (int x = 15; x <= 24; x++) v.Add(BuildingKind.Wall, x, 21);
            return v;
        }
        public Building Add(BuildingKind kind, int x, int z)
        {
            Building b = new Building { Id = NextId++, Kind = kind, X = x, Z = z };
            b.Health = b.MaxHealth;
            Buildings.Add(b);
            return b;
        }
        public int Capacity { get { return 5000 + (KeepLevel - 1) * 5000; } }
        public int KeepLevel
        {
            get { foreach (Building b in Buildings) if (b.Kind == BuildingKind.Keep) return b.Level; return 1; }
        }
        public Building At(int x, int z)
        { foreach (Building b in Buildings) if (b.Contains(x, z)) return b; return null; }
        public int Count(BuildingKind kind) { int count = 0; foreach (Building b in Buildings) if (b.Kind == kind) count++; return count; }
        public int Limit(BuildingKind kind) { return Rules.BuildLimit(kind, KeepLevel); }
        public bool AtLimit(BuildingKind kind) { return Count(kind) >= Limit(kind); }
        public bool CanPlace(BuildingKind kind, int x, int z, int ignoreId)
        {
            int size = Rules.Spec(kind).Size;
            if (x < 2 || z < 2 || x + size > Rules.MapSize - 2 || z + size > Rules.MapSize - 2) return false;
            foreach (Building b in Buildings)
                if (b.Id != ignoreId && x < b.X + b.Spec.Size && x + size > b.X && z < b.Z + b.Spec.Size && z + size > b.Z) return false;
            return true;
        }
    }

    public static class Missions
    {
        public static readonly string[] Names = { "松林前哨", "河谷营地", "灰岩要塞" };
        public static List<Building> Create(int index)
        {
            VillageData v = new VillageData();
            v.Add(BuildingKind.Keep, 18, 18);
            v.Add(BuildingKind.Mine, 15, 14);
            v.Add(BuildingKind.Reservoir, 23, 20);
            v.Add(BuildingKind.Barracks, 17, 25);
            v.Add(BuildingKind.Cannon, 14, 20);
            v.Add(BuildingKind.Watchtower, 24, 15);
            for (int x = 12; x <= 27; x++) { v.Add(BuildingKind.Wall, x, 12); v.Add(BuildingKind.Wall, x, 29); }
            for (int z = 13; z <= 28; z++) { v.Add(BuildingKind.Wall, 12, z); v.Add(BuildingKind.Wall, 27, z); }
            if (index >= 1) { v.Add(BuildingKind.Cannon, 22, 25); v.Add(BuildingKind.Watchtower, 20, 14); }
            if (index >= 2)
            {
                v.Add(BuildingKind.Cannon, 23, 18);
                for (int z = 17; z < 25; z++) v.Add(BuildingKind.Wall, 22, z);
                foreach (Building b in v.Buildings) { b.Level = 2; b.Health = b.MaxHealth; }
            }
            return v.Buildings;
        }
    }
}
