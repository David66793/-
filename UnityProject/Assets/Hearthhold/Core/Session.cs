using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Hearthhold.Core
{
    public sealed class GameSession
    {
        public VillageData Village;
        public Battle Battle;
        public int MissionIndex;
        public string Notice = "欢迎来到篝火堡垒。建设聚落，带领远征队出发。";
        private readonly Stack<LayoutMove> undo = new Stack<LayoutMove>();
        private readonly Stack<LayoutMove> redo = new Stack<LayoutMove>();
        private struct LayoutMove { public int Id, OldX, OldZ, NewX, NewZ; }
        public GameSession(VillageData village) { Village = village; }
        public bool Build(BuildingKind kind, int x, int z)
        {
            if (Battle != null) return false;
            if ((int)kind <= 0 || (int)kind >= Rules.Buildings.Length) { Notice = "议事堡只能拥有一座。"; return false; }
            if (Village.AtLimit(kind)) { Notice = Rules.Spec(kind).Name + "已达数量上限 " + Village.Limit(kind) + "。升级议事堡可提高上限。"; return false; }
            if (!Village.CanPlace(kind, x, z, -1)) { Notice = "这里没有足够的空间。请选择绿色区域。"; return false; }
            int cost = Rules.Spec(kind).Cost;
            if (Village.Gold < cost) { Notice = "金币不足，收取产出或完成一次远征。"; return false; }
            Collect(DateTime.UtcNow);
            Village.Gold -= cost;
            Village.Add(kind, x, z);
            undo.Clear(); redo.Clear();
            Notice = Rules.Spec(kind).Name + "建造完成。";
            return true;
        }
        public bool Move(int id, int x, int z)
        {
            if (Battle != null) return false;
            Building b = Find(id);
            if (b == null || !Village.CanPlace(b.Kind, x, z, id)) { Notice = "建筑移动失败：目标位置被占用。"; return false; }
            if (b.X == x && b.Z == z) return false;
            undo.Push(new LayoutMove { Id = id, OldX = b.X, OldZ = b.Z, NewX = x, NewZ = z });
            redo.Clear(); b.X = x; b.Z = z;
            Notice = "布局已更新 · Ctrl+Z 可撤销移动。";
            return true;
        }
        public bool Undo(bool forward)
        {
            if (Battle != null) return false;
            Stack<LayoutMove> from = forward ? redo : undo, to = forward ? undo : redo;
            if (from.Count == 0) { Notice = "没有可" + (forward ? "重做" : "撤销") + "的移动。"; return false; }
            LayoutMove move = from.Peek();
            Building b = Find(move.Id);
            int x = forward ? move.NewX : move.OldX, z = forward ? move.NewZ : move.OldZ;
            if (b == null || !Village.CanPlace(b.Kind, x, z, b.Id)) return false;
            from.Pop(); to.Push(move); b.X = x; b.Z = z;
            Notice = forward ? "已重做建筑移动。" : "已撤销建筑移动。";
            return true;
        }
        public Building Find(int id) { foreach (Building b in Village.Buildings) if (b.Id == id) return b; return null; }
        public void DemolitionRefund(Building b, out int gold, out int crystal)
        {
            gold = crystal = 0;
            if (b == null || b.Kind == BuildingKind.Keep) return;
            int upgradeTiers = b.Level * (b.Level - 1) / 2;
            gold = (b.Spec.Cost + Math.Max(80, b.Spec.Cost) * upgradeTiers) / 2;
            crystal = 55 * upgradeTiers / 2;
        }
        public bool Demolish(int id)
        {
            if (Battle != null) { Notice = "远征期间不能拆除基地建筑。"; return false; }
            Building b = Find(id);
            if (b == null) { Notice = "建筑不存在或已拆除。"; return false; }
            if (b.Kind == BuildingKind.Keep) { Notice = "议事堡是聚落核心，不能拆除。"; return false; }
            int gold, crystal; DemolitionRefund(b, out gold, out crystal);
            Collect(DateTime.UtcNow);
            int actualGold = Math.Min(gold, Village.Capacity - Village.Gold), actualCrystal = Math.Min(crystal, Village.Capacity - Village.Crystal);
            Village.Buildings.Remove(b);
            Village.Gold += actualGold; Village.Crystal += actualCrystal;
            undo.Clear(); redo.Clear();
            Notice = "已拆除" + b.Spec.Name + "，返还 " + actualGold + " 金币、" + actualCrystal + " 晶露。";
            return true;
        }
        public int UpgradeGold(Building b) { return b.Kind == BuildingKind.Keep ? 650 * b.Level : Math.Max(80, b.Spec.Cost) * b.Level; }
        public int UpgradeCrystal(Building b) { return (b.Kind == BuildingKind.Keep ? 180 : 55) * b.Level; }
        public bool Upgrade(int id)
        {
            if (Battle != null) return false;
            Building b = Find(id); if (b == null) return false;
            if (b.Level >= 3) { Notice = "已达到原型的最高等级。"; return false; }
            if (b.Kind != BuildingKind.Keep && b.Level >= Village.KeepLevel) { Notice = "请先升级议事堡，解锁建筑等级。"; return false; }
            int gold = UpgradeGold(b), crystal = UpgradeCrystal(b);
            if (Village.Gold < gold || Village.Crystal < crystal) { Notice = "升级资源不足。"; return false; }
            Collect(DateTime.UtcNow);
            Village.Gold -= gold; Village.Crystal -= crystal; b.Level++; b.Health = b.MaxHealth;
            Notice = b.Spec.Name + "已升至 " + b.Level + " 级。";
            return true;
        }
        public void Income(DateTime utcNow, out int gold, out int crystal)
        {
            long elapsed = Math.Max(0, utcNow.Ticks - Village.LastIncomeUtcTicks);
            long seconds = Math.Min(8 * 3600, elapsed / TimeSpan.TicksPerSecond);
            int goldRate = 0, crystalRate = 0;
            foreach (Building b in Village.Buildings)
            {
                if (b.Kind == BuildingKind.Mine) goldRate += 24 * b.Level;
                if (b.Kind == BuildingKind.Reservoir) crystalRate += 18 * b.Level;
            }
            gold = (int)Math.Min(Village.Capacity - Village.Gold, seconds * goldRate / 60);
            crystal = (int)Math.Min(Village.Capacity - Village.Crystal, seconds * crystalRate / 60);
        }
        public void Collect(DateTime utcNow)
        {
            if (Battle != null) return;
            int gold, crystal; Income(utcNow, out gold, out crystal);
            Village.Gold += gold; Village.Crystal += crystal;
            // Local demo clock, intentionally not an online-economy trust boundary.
            Village.LastIncomeUtcTicks = utcNow.Ticks;
            Notice = "收取 " + gold + " 金币、" + crystal + " 晶露。";
        }
        public void BeginBattle()
        {
            if (Battle != null) return;
            if (Village.Count(BuildingKind.Barracks) == 0) { Notice = "请先建造远征营，再率领部队出征。"; return; }
            Battle = new Battle(MissionIndex);
            Notice = "侦察阶段 · 在外围投下第一名士兵后开始计时。";
        }
        public bool Settle()
        {
            if (Battle == null || !Battle.Finished || Battle.Settled) return false;
            Battle.Settled = true;
            Village.Gold = Math.Min(Village.Capacity, Village.Gold + Battle.GoldReward);
            Village.Crystal = Math.Min(Village.Capacity, Village.Crystal + Battle.CrystalReward);
            if (Battle.Stars > 0) Village.Wins++;
            Notice = "远征结束：" + Battle.Stars + " 星，获得 " + Battle.GoldReward + " 金币。";
            return true;
        }
        public void ReturnHome()
        {
            if (Battle == null) return;
            Battle.Finish(); Settle(); Battle = null;
        }
    }

    public static class SaveStore
    {
        public static VillageData Load(string path, out string message)
        {
            message = "";
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return VillageData.Create();
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    VillageData v;
                    using (FileStream file = File.OpenRead(candidate)) v = (VillageData)new XmlSerializer(typeof(VillageData)).Deserialize(file);
                    Validate(v);
                    if (candidate != path) message = "主存档无法读取，已从备份恢复。";
                    return v;
                }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is InvalidOperationException || ex is InvalidDataException || ex is UnauthorizedAccessException)) throw;
                    message = "存档无法读取：" + ex.Message;
                }
            }
            // Never silently replace an existing corrupt or newer-version save.
            throw new InvalidDataException(message + " 原文件已保留，请备份后检查。");
        }
        public static void Save(string path, VillageData village)
        {
            Validate(village);
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            using (FileStream file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new XmlSerializer(typeof(VillageData)).Serialize(file, village);
                file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        public static void Validate(VillageData v)
        {
            if (v == null || v.Version != 1 || v.Buildings == null || v.Buildings.Count > 1200 || v.Wins < 0)
                throw new InvalidDataException("不支持的存档版本或数据。");
            HashSet<int> ids = new HashSet<int>();
            int keepCount = 0, maxId = 0;
            foreach (Building b in v.Buildings)
            {
                if (b == null || b.Id <= 0 || !ids.Add(b.Id) || (int)b.Kind < 0 || (int)b.Kind >= Rules.Buildings.Length || b.Level < 1 || b.Level > 3)
                    throw new InvalidDataException("建筑数据无效。");
                maxId = Math.Max(maxId, b.Id);
                if (b.Kind == BuildingKind.Keep) keepCount++;
            }
            if (keepCount != 1 || v.NextId <= maxId || v.Gold < 0 || v.Crystal < 0 || v.Gold > v.Capacity || v.Crystal > v.Capacity || v.LastIncomeUtcTicks <= 0 || v.LastIncomeUtcTicks > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("资源或主城数据无效。");
            foreach (Building b in v.Buildings)
                if (b.Health != b.MaxHealth || !v.CanPlace(b.Kind, b.X, b.Z, b.Id)) throw new InvalidDataException("建筑位置或生命值无效。");
        }
    }
}
