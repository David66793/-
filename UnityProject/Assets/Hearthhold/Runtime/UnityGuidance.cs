using Hearthhold.Core;
using UnityEngine;

namespace Hearthhold.UnityClient
{
    public sealed partial class GameBootstrap
    {
        private bool showArmyGuide, showBrief, briefSeen;
        private int demolishId = -1;
        private TroopKind guideTroop = TroopKind.Guardian;
        private bool ExtraModal { get { return showArmyGuide || showBrief || demolishId >= 0; } }
        private void CloseExtraModals() { showArmyGuide = false; showBrief = false; demolishId = -1; }
        private void AskDemolish()
        {
            Building b = session.Find(selected);
            if (session.Battle != null || b == null) return;
            if (b.Kind == BuildingKind.Keep) { session.Notice = "议事堡不可拆除。"; return; }
            demolishId = b.Id; moving = -1; buildKind = null;
        }
        private void DrawTroopCard()
        {
            if (session.Battle == null || session.Battle.Finished) return;
            float x = Screen.width - 267;
            Box(new Rect(x, 164, 245, 307));
            if (heal)
            {
                GUI.Label(new Rect(x + 16, 182, 218, 36), "疗愈之雨", heading);
                GUI.Label(new Rect(x + 16, 235, 218, 212), "按Q后点击友军附近。\n恢复半径5格内存活友军约2/3最大生命。每场2次。\n\n等铁卫受伤再治疗；治疗不能复活阵亡部队。", label);
                return;
            }
            TroopSpec s = Rules.Spec(troop);
            GUI.Label(new Rect(x + 16, 181, 218, 35), s.Name, heading);
            GUI.Label(new Rect(x + 16, 223, 218, 28), s.Role, label);
            GUI.Label(new Rect(x + 16, 262, 218, 48), "生命 " + s.Health + "  单次伤害 " + s.Damage + "\n射程 " + (s.Range / 1000f).ToString("0.##") + "格", small);
            GUI.Label(new Rect(x + 16, 322, 218, 75), s.Tactics, small);
            GUI.Label(new Rect(x + 16, 405, 218, 59), s.Weakness, small);
        }
        private void DrawExtraModals()
        {
            if (!ExtraModal) return;
            GUI.enabled = true;
            GUI.color = new Color(0.025f, 0.055f, 0.05f, 0.94f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
            float w = Mathf.Min(Screen.width - 60, 830), x = (Screen.width - w) / 2, y = Mathf.Max(25, (Screen.height - 535) / 2);
            Box(new Rect(x, y, w, 535));
            if (demolishId >= 0)
            {
                Building b = session.Find(demolishId);
                if (b == null) { demolishId = -1; return; }
                int gold, crystal; session.DemolitionRefund(b, out gold, out crystal);
                GUI.Label(new Rect(x + 25, y + 25, w - 50, 40), "确认拆除 " + b.Spec.Name + "？", heading);
                GUI.Label(new Rect(x + 25, y + 100, w - 50, 235), "等级 " + b.Level + " · 位置 " + b.X + ", " + b.Z + "\n\n返还 " + gold + " 金币、" + crystal + " 晶露。\n返还建设与历次升级投入的50%，受仓储上限限制。\n已产生的收益会先收取。拆除后释放名额，不能撤销。" + (b.Kind == BuildingKind.Barracks && session.Village.Count(b.Kind) == 1 ? "\n\n拆除最后一座远征营后，需重建才能出征。" : ""), label);
                if (GUI.Button(new Rect(x + 25, y + 449, (w - 65) / 2, 48), "保留建筑")) demolishId = -1;
                if (GUI.Button(new Rect(x + 40 + (w - 65) / 2, y + 449, (w - 65) / 2, 48), "确认拆除"))
                { int id = demolishId; demolishId = -1; if (session.Demolish(id)) { selected = -1; RebuildBuildings(); Save(); } }
            }
            else if (showBrief)
            {
                GUI.Label(new Rect(x + 25, y + 25, w - 50, 40), "第一次远征，照这个顺序来", heading);
                GUI.Label(new Rect(x + 25, y + 88, w - 50, 345), "士兵落地后自动行动，你负责位置和时机。\n\n① 按3选铁卫，先投2—3名，吸引防御塔火力。\n② 按4选破城手，紧跟铁卫，从同一侧打开城墙。\n③ 按1选先锋，沿缺口投放，清理基地内建筑。\n④ 按2选游侠，放在后方，用射程提供输出。\n\n在基地外围投兵；首次投兵后才开始180秒计时。\n按住左键可连续投兵。铁卫受伤时按Q选择治疗，点击友军附近。\n\n摧毁议事堡、50%破坏、100%破坏各得一星。", label);
                if (GUI.Button(new Rect(x + 25, y + 462, w - 50, 44), "知道了，开始侦察")) { showBrief = false; briefSeen = true; }
            }
            else if (showArmyGuide)
            {
                GUI.Label(new Rect(x + 25, y + 25, w - 50, 40), "远征兵种图鉴", heading);
                for (int i = 0; i < 4; i++)
                    if (GUI.Button(new Rect(x + 25 + i * (w - 50) / 4, y + 87, (w - 50) / 4 - 8, 42), Rules.Troops[i].Name)) guideTroop = (TroopKind)i;
                TroopSpec s = Rules.Spec(guideTroop);
                GUI.Label(new Rect(x + 25, y + 154, w - 50, 40), s.Name + " · " + s.Role, heading);
                GUI.Label(new Rect(x + 25, y + 212, w - 50, 235), "生命 " + s.Health + " / 单次伤害 " + s.Damage + " / 射程 " + (s.Range / 1000f).ToString("0.##") + "格\n\n" + s.Description + "\n\n怎么用：" + s.Tactics + "\n\n注意：" + s.Weakness, label);
                if (GUI.Button(new Rect(x + 25, y + 462, w - 50, 44), "返回游戏")) showArmyGuide = false;
            }
        }
    }
}
