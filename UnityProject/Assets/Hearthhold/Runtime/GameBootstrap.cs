using System;
using System.Collections.Generic;
using System.IO;
using Hearthhold.Core;
using UnityEngine;

namespace Hearthhold.UnityClient
{
    // Presentation adapter: no Unity objects are used by the shared simulation.
    public sealed partial class GameBootstrap : MonoBehaviour
    {
        private GameSession session;
        private readonly ModelViews modelViews = new ModelViews();
        private Camera worldCamera;
        private Transform sceneryRoot, buildingsRoot, unitsRoot;
        private readonly Dictionary<int, GameObject> buildingViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> unitViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        private string savePath, fatalError, smokeCapturePath;
        private int smokeFrames;
        private int selected = -1, moving = -1;
        private BuildingKind? buildKind;
        private TroopKind troop;
        private bool heal, help;
        private float accumulator, saveElapsed, deployElapsed;
        private Vector3 focus = new Vector3(20, 0, 20), previousMouse;
        private GameObject placement;
        private Font uiFont;
        private GUIStyle label, heading, small;
        private static readonly Color Gold = new Color32(235, 186, 88, 255);
        private static readonly Color Mint = new Color32(117, 212, 184, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (FindAnyObjectByType<GameBootstrap>() == null) new GameObject("Hearthhold game").AddComponent<GameBootstrap>();
        }
        private void Awake()
        {
            Application.targetFrameRate = 60;
            smokeCapturePath = CommandLineValue("-hearthhold-smoke");
            if (!string.IsNullOrEmpty(smokeCapturePath))
            {
                Application.runInBackground = true;
                smokeCapturePath = Path.GetFullPath(smokeCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(smokeCapturePath));
                savePath = Path.Combine(Path.GetDirectoryName(smokeCapturePath), "smoke-village.xml");
            }
            else savePath = Path.Combine(Application.persistentDataPath, "village.xml");
            try
            {
                string message;
                session = new GameSession(SaveStore.Load(savePath, out message));
                if (!string.IsNullOrEmpty(message)) session.Notice = message;
            }
            catch (Exception ex) { fatalError = ex.Message; return; }
            sceneryRoot = new GameObject("Island").transform;
            buildingsRoot = new GameObject("Buildings").transform;
            unitsRoot = new GameObject("Units").transform;
            worldCamera = new GameObject("Isometric camera").AddComponent<Camera>();
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 24;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color32(49, 97, 98, 255);
            worldCamera.nearClipPlane = 0.1f;
            worldCamera.farClipPlane = 200;
            worldCamera.transform.rotation = Quaternion.Euler(45, 45, 0);
            MoveCamera();
            Light sun = new GameObject("Afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(0.63f, 0.7f, 0.66f);
            MakeIsland(); RebuildBuildings();
            placement = Piece("Placement", PrimitiveType.Cube, Vector3.zero, new Vector3(1, 0.07f, 1), Mint, transform);
            placement.SetActive(false);
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 16);
        }
        private static string CommandLineValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++) if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            return null;
        }
        private void LateUpdate()
        {
            if (string.IsNullOrEmpty(smokeCapturePath)) return;
            smokeFrames++;
            if (smokeFrames == 20)
            {
                ScreenCapture.CaptureScreenshot(smokeCapturePath);
                Debug.Log("HEARTHHOLD_SMOKE_CAPTURE_REQUESTED: " + smokeCapturePath);
            }
            if (smokeFrames > 20 && File.Exists(smokeCapturePath) && new FileInfo(smokeCapturePath).Length > 1024)
            {
                Debug.Log("HEARTHHOLD_SMOKE_READY: " + smokeCapturePath);
                smokeCapturePath = null;
                Application.Quit(0);
            }
            else if (smokeFrames > 600)
            {
                Debug.LogError("HEARTHHOLD_SMOKE_TIMEOUT: screenshot was not written.");
                Application.Quit(3);
            }
        }
        private Material MaterialFor(Color color)
        {
            Material result;
            if (materials.TryGetValue(color, out result)) return result;
            Material template = Resources.Load<Material>("WorldPalette");
            result = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            result.color = color;
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            materials.Add(color, result); return result;
        }
        private GameObject Piece(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color, Transform parent)
        {
            GameObject obj = GameObject.CreatePrimitive(primitive);
            obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            Collider collider = obj.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            return obj;
        }
        private void MakeIsland()
        {
            Piece("River", PrimitiveType.Cube, new Vector3(20, -1.2f, 20), new Vector3(95, 0.1f, 95), new Color32(49, 105, 108, 255), sceneryRoot);
            Piece("Island cliff", PrimitiveType.Cube, new Vector3(20, -0.65f, 20), new Vector3(40.6f, 1.2f, 40.6f), new Color32(106, 115, 84, 255), sceneryRoot);
            Piece("Meadow", PrimitiveType.Cube, new Vector3(20, -0.08f, 20), new Vector3(40, 0.15f, 40), new Color32(132, 163, 91, 255), sceneryRoot);
            Piece("East road", PrimitiveType.Cube, new Vector3(20.5f, 0.012f, 23.5f), new Vector3(19, 0.03f, 1), new Color32(173, 164, 116, 255), sceneryRoot);
            Piece("North road", PrimitiveType.Cube, new Vector3(20.5f, 0.012f, 19.5f), new Vector3(1, 0.03f, 18), new Color32(173, 164, 116, 255), sceneryRoot);
            for (int i = 0; i < 76; i++)
            {
                float t = i % 19 * 2.15f;
                Vector3 pos = i / 19 == 0 ? new Vector3(t, 0, -0.5f) : i / 19 == 1 ? new Vector3(-0.5f, 0, t) : i / 19 == 2 ? new Vector3(t, 0, 40.5f) : new Vector3(40.5f, 0, t);
                Transform root = new GameObject("Pine").transform; root.SetParent(sceneryRoot); root.localPosition = pos;
                Piece("Trunk", PrimitiveType.Cylinder, new Vector3(0, 0.7f, 0), new Vector3(0.23f, 0.7f, 0.23f), new Color32(113, 87, 52, 255), root);
                for (int n = 0; n < 3; n++) Cone(root, new Vector3(0, 1.1f + n * 0.75f, 0), 1.05f - n * 0.19f, 1.8f, new Color32((byte)(63 + n * 9), (byte)(106 + n * 7), 67, 255));
            }
        }
        private void Cone(Transform parent, Vector3 position, float radius, float height, Color color)
        {
            const int sides = 7;
            List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                int start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                vertices.Add(new Vector3(0, height, 0));
                vertices.Add(new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            Mesh mesh = new Mesh { name = "Procedural cone" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            GameObject obj = new GameObject("Canopy"); obj.transform.SetParent(parent, false); obj.transform.localPosition = position;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
        }
        private void RebuildBuildings()
        {
            foreach (GameObject obj in buildingViews.Values) DestroyBuildingView(obj);
            buildingViews.Clear();
            List<Building> buildings = session.Battle == null ? session.Village.Buildings : session.Battle.Buildings;
            foreach (Building b in buildings) buildingViews.Add(b.Id, CreateBuilding(b));
            foreach (GameObject obj in unitViews.Values) Destroy(obj);
            unitViews.Clear();
        }
        private GameObject CreateBuilding(Building b)
        {
            return modelViews.Building(b, buildingsRoot);
        }
        private void DestroyBuildingView(GameObject obj) { Destroy(obj); }
        private void Update()
        {
            if (session == null) return;
            if (Input.GetKeyDown(KeyCode.F1)) help = !help;
            if (Input.GetKeyDown(KeyCode.Escape)) { help = false; buildKind = null; moving = -1; heal = false; selected = -1; CloseExtraModals(); }
            if (Input.GetKeyDown(KeyCode.I) && demolishId < 0 && !showBrief) showArmyGuide = !showArmyGuide;
            if (Input.GetKeyDown(KeyCode.F11)) Screen.fullScreen = !Screen.fullScreen;
            if (help || ExtraModal) return;
            float dt = Mathf.Min(Time.deltaTime, 0.2f);
            accumulator += dt;
            while (accumulator >= 0.05f) { if (session.Battle != null) session.Battle.Step(); accumulator -= 0.05f; }
            if (session.Battle != null && session.Battle.Finished && session.Settle()) Save();
            Vector3 right = worldCamera.transform.right; right.y = 0; right.Normalize();
            Vector3 forward = Vector3.Cross(right, Vector3.up);
            if (Input.GetKey(KeyCode.W)) focus += forward * dt * 16;
            if (Input.GetKey(KeyCode.S)) focus -= forward * dt * 16;
            if (Input.GetKey(KeyCode.A)) focus -= right * dt * 16;
            if (Input.GetKey(KeyCode.D)) focus += right * dt * 16;
            if (Input.GetMouseButtonDown(2)) previousMouse = Input.mousePosition;
            if (Input.GetMouseButton(2))
            {
                Vector3 delta = Input.mousePosition - previousMouse; previousMouse = Input.mousePosition;
                focus -= (right * delta.x + forward * delta.y) * worldCamera.orthographicSize / Screen.height * 2;
            }
            worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize - Input.mouseScrollDelta.y * 1.5f, 8, 38);
            if (Input.GetKeyDown(KeyCode.Home)) { focus = new Vector3(20, 0, 20); worldCamera.orthographicSize = 24; }
            MoveCamera();
            if (Input.GetMouseButtonDown(1)) { buildKind = null; moving = -1; heal = false; selected = -1; }
            if (session.Battle != null)
            {
                for (int i = 0; i < 4; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) { troop = (TroopKind)i; heal = false; }
                if (Input.GetKeyDown(KeyCode.Q)) heal = !heal;
                SyncBattle();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.C)) { session.Collect(DateTime.UtcNow); Save(); }
                if (Input.GetKeyDown(KeyCode.U) && selected >= 0 && session.Upgrade(selected)) { RebuildBuildings(); Save(); }
                if (Input.GetKeyDown(KeyCode.M)) moving = selected;
                if (Input.GetKeyDown(KeyCode.Delete)) AskDemolish();
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z) && session.Undo(false)) { RebuildBuildings(); Save(); }
                if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Y) && session.Undo(true)) { RebuildBuildings(); Save(); }
            }
            Vector3 ground;
            if (!OverHud() && GroundPoint(out ground))
            {
                int x = Mathf.FloorToInt(ground.x), z = Mathf.FloorToInt(ground.z);
                Building movingBuilding = session.Find(moving);
                BuildingKind? placeKind = movingBuilding != null ? movingBuilding.Kind : buildKind;
                placement.SetActive(session.Battle == null && placeKind.HasValue);
                if (placement.activeSelf)
                {
                    int size = Rules.Spec(placeKind.Value).Size;
                    placement.transform.position = new Vector3(x + size / 2f, 0.1f, z + size / 2f);
                    placement.transform.localScale = new Vector3(size, 0.08f, size);
                    placement.GetComponent<Renderer>().sharedMaterial = MaterialFor(session.Village.CanPlace(placeKind.Value, x, z, moving) && (movingBuilding != null || !session.Village.AtLimit(placeKind.Value) && session.Village.Gold >= Rules.Spec(placeKind.Value).Cost) ? Mint : Color.red);
                }
                deployElapsed += dt;
                bool repeat = Input.GetMouseButton(0) && deployElapsed > 0.15f && (session.Battle != null && !heal || placeKind == BuildingKind.Wall);
                if (Input.GetMouseButtonDown(0) || repeat) { HandleMapClick(x, z); deployElapsed = 0; }
            }
            else placement.SetActive(false);
            saveElapsed += dt; if (saveElapsed >= 15) { Save(); saveElapsed = 0; }
        }
        private void MoveCamera() { worldCamera.transform.position = focus - worldCamera.transform.forward * 65; }
        private bool GroundPoint(out Vector3 point)
        {
            Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition); float distance;
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance)) { point = ray.GetPoint(distance); return true; }
            point = Vector3.zero; return false;
        }
        private bool OverHud()
        {
            float x = Input.mousePosition.x, y = Screen.height - Input.mousePosition.y;
            return help || ExtraModal || session.Battle != null && session.Battle.Finished || y < 100 || y > Screen.height - 155 || x < 250 && y < 365 || (selected >= 0 || session.Battle != null) && x > Screen.width - 270 && y < 490;
        }
        private void HandleMapClick(int x, int z)
        {
            if (session.Battle != null)
            {
                bool done = heal ? session.Battle.CastHeal(x * 1000 + 500, z * 1000 + 500) : session.Battle.Deploy(troop, x * 1000 + 500, z * 1000 + 500);
                session.Notice = done ? "命令已执行。" : "请检查投兵区域、军队余量或法术次数。";
                if (done && heal) heal = false;
            }
            else if (moving >= 0) { if (session.Move(moving, x, z)) { moving = -1; RebuildBuildings(); Save(); } }
            else if (buildKind.HasValue) { if (session.Build(buildKind.Value, x, z)) { RebuildBuildings(); Save(); } }
            else
            {
                RaycastHit hit;
                if (Physics.Raycast(worldCamera.ScreenPointToRay(Input.mousePosition), out hit))
                { BuildingHandle handle = hit.collider.GetComponent<BuildingHandle>(); selected = handle == null ? -1 : handle.Id; }
                else selected = -1;
            }
        }
        private void SyncBattle()
        {
            foreach (Building b in session.Battle.Buildings)
            {
                GameObject obj;
                if (buildingViews.TryGetValue(b.Id, out obj) && b.Health <= 0 && obj.activeSelf) obj.SetActive(false);
            }
            foreach (Unit unit in session.Battle.Units)
            {
                GameObject obj;
                if (!unitViews.TryGetValue(unit.Id, out obj))
                {
                    obj = modelViews.Troop(unit, unitsRoot);
                    unitViews.Add(unit.Id, obj);
                }
                obj.SetActive(unit.Health > 0);
                obj.transform.position = new Vector3(unit.X / 1000f, 0, unit.Z / 1000f);
            }
        }
        private void Save()
        {
            if (session == null) return;
            try { SaveStore.Save(savePath, session.Village); } catch (Exception ex) { session.Notice = "保存失败：" + ex.Message; Debug.LogError(ex); }
        }
        private void OnApplicationQuit() { Save(); }
        private void OnApplicationPause(bool pause) { if (pause) Save(); }
        private void OnDestroy()
        {
            modelViews.Dispose();
            foreach (Material material in materials.Values) Destroy(material);
            if (sceneryRoot != null) foreach (MeshFilter filter in sceneryRoot.GetComponentsInChildren<MeshFilter>()) if (filter.sharedMesh != null && filter.sharedMesh.name == "Procedural cone") Destroy(filter.sharedMesh);
            foreach (GameObject obj in buildingViews.Values) if (obj != null) DestroyBuildingView(obj);
        }
        private void BeginBattle()
        {
            selected = moving = -1; buildKind = null; heal = false;
            session.BeginBattle(); accumulator = 0; RebuildBuildings();
            if (session.Battle != null && !briefSeen) showBrief = true;
        }
        private void ReturnHome()
        {
            session.ReturnHome(); selected = moving = -1; buildKind = null; heal = false;
            RebuildBuildings(); Save();
        }
        private void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 16, wordWrap = true };
                heading = new GUIStyle(label) { fontSize = 25, fontStyle = FontStyle.Bold };
                small = new GUIStyle(label) { fontSize = 13 };
            }
            GUI.skin.font = uiFont;
            if (fatalError != null) { GUI.Box(new Rect(30, 30, Screen.width - 60, 160), "存档加载失败，原文件已保留。\n" + fatalError); return; }
            if (session == null) return;
            bool finished = session.Battle != null && session.Battle.Finished;
            GUI.enabled = !help && !finished && !ExtraModal;
            Box(new Rect(0, 0, Screen.width, 90));
            GUI.Label(new Rect(24, 17, 310, 42), "篝火堡垒  HEARTHHOLD", heading);
            GUI.Label(new Rect(Screen.width - 505, 23, 365, 35), "金币 " + session.Village.Gold + "    晶露 " + session.Village.Crystal, label);
            if (GUI.Button(new Rect(Screen.width - 118, 21, 94, 40), "帮助 F1")) help = true;
            Box(new Rect(20, 112, 225, 235));
            if (session.Battle == null)
            {
                GUI.Label(new Rect(36, 132, 200, 36), "你的聚落", heading);
                GUI.Label(new Rect(36, 178, 200, 50), "议事堡 " + session.Village.KeepLevel + " 级\n远征胜利 " + session.Village.Wins + " 次", label);
                int gold, crystal; session.Income(DateTime.UtcNow, out gold, out crystal);
                if (GUI.Button(new Rect(35, 252, 194, 44), "收取 " + gold + " 金 / " + crystal + " 晶")) { session.Collect(DateTime.UtcNow); Save(); }
                if (GUI.Button(new Rect(35, 304, 194, 32), "兵种图鉴 I")) showArmyGuide = true;
            }
            else
            {
                Battle battle = session.Battle;
                GUI.Label(new Rect(36, 131, 200, 35), Missions.Names[battle.Mission], heading);
                GUI.Label(new Rect(36, 181, 200, 100), (battle.Started ? "战斗中" : "侦察中") + "\n剩余 " + battle.SecondsLeft + " 秒\n破坏率 " + battle.Destruction + "%  /  " + battle.Stars + " 星", label);
            }
            GUI.Label(new Rect(265, 108, Screen.width - 550, 55), session.Notice, small);
            if (selected >= 0 && session.Battle == null)
            {
                Building b = session.Find(selected);
                if (b != null)
                {
                    float x = Screen.width - 265;
                    Box(new Rect(x, 160, 245, 314));
                    GUI.Label(new Rect(x + 16, 176, 212, 35), b.Spec.Name + " " + b.Level + "级", heading);
                    GUI.Label(new Rect(x + 16, 220, 212, 63), b.Spec.Description, small);
                    if (GUI.Button(new Rect(x + 14, 292, 218, 34), "升级 " + session.UpgradeGold(b) + "金 / " + session.UpgradeCrystal(b) + "晶")) { if (session.Upgrade(b.Id)) { RebuildBuildings(); Save(); } }
                    if (GUI.Button(new Rect(x + 14, 336, 218, 34), "移动 M")) { moving = b.Id; buildKind = null; }
                    GUI.Label(new Rect(x + 16, 381, 216, 25), "已建 " + session.Village.Count(b.Kind) + " / " + session.Village.Limit(b.Kind), small);
                    if (b.Kind != BuildingKind.Keep && GUI.Button(new Rect(x + 14, 419, 218, 34), "拆除建筑 Del")) AskDemolish();
                    if (b.Kind == BuildingKind.Keep) GUI.Label(new Rect(x + 16, 422, 216, 28), "聚落核心，不可拆除", small);
                }
            }
            Box(new Rect(0, Screen.height - 150, Screen.width, 150));
            if (session.Battle == null)
            {
                float width = (Screen.width - 315) / 6f;
                for (int i = 1; i <= 6; i++)
                {
                    BuildingKind kind = (BuildingKind)i;
                    GUI.backgroundColor = buildKind == kind ? Gold : Color.white;
                    if (GUI.Button(new Rect(20 + (i - 1) * width, Screen.height - 130, width - 8, 78), Rules.Spec(kind).Name + "\n" + Rules.Spec(kind).Cost + " 金币\n" + session.Village.Count(kind) + "/" + session.Village.Limit(kind)))
                    {
                        if (session.Village.AtLimit(kind)) { session.Notice = Rules.Spec(kind).Name + "已达建造上限，请升级议事堡。"; buildKind = null; }
                        else { buildKind = kind; selected = moving = -1; }
                    }
                }
                GUI.backgroundColor = Color.white;
                if (GUI.Button(new Rect(Screen.width - 277, Screen.height - 130, 32, 30), "<")) session.MissionIndex = (session.MissionIndex + 2) % 3;
                GUI.Label(new Rect(Screen.width - 230, Screen.height - 128, 162, 30), Missions.Names[session.MissionIndex], label);
                if (GUI.Button(new Rect(Screen.width - 54, Screen.height - 130, 32, 30), ">")) session.MissionIndex = (session.MissionIndex + 1) % 3;
                if (GUI.Button(new Rect(Screen.width - 277, Screen.height - 91, 255, 42), "出发远征 →")) BeginBattle();
            }
            else
            {
                float width = (Screen.width - 260) / 5f;
                for (int i = 0; i < 4; i++)
                {
                    GUI.backgroundColor = !heal && troop == (TroopKind)i ? Gold : Color.white;
                    if (GUI.Button(new Rect(20 + i * width, Screen.height - 128, width - 8, 80), (i + 1) + " " + Rules.Troops[i].Name + "\n余量 " + session.Battle.Available[i])) { troop = (TroopKind)i; heal = false; }
                }
                GUI.backgroundColor = heal ? Mint : Color.white;
                if (GUI.Button(new Rect(20 + width * 4, Screen.height - 128, width - 8, 80), "Q 疗愈\n" + session.Battle.SpellCharges + " 次")) heal = !heal;
                GUI.backgroundColor = Color.white;
                if (GUI.Button(new Rect(Screen.width - 221, Screen.height - 110, 199, 48), "结束进攻")) { session.Battle.Finish(); session.Settle(); Save(); }
            }
            GUI.Label(new Rect(22, Screen.height - 32, Screen.width - 30, 30), "WASD / 中键 移动镜头 · 滚轮缩放 · Home复位 · 右键取消 · 1—4投兵 · Q治疗 · Ctrl+Z/Y 撤销/重做移动", small);
            DrawBattleOverlay();
            DrawTroopCard();
            GUI.enabled = true;
            if (finished)
            {
                Battle b = session.Battle; float x = Screen.width / 2f - 220, y = Screen.height / 2f - 145;
                Box(new Rect(x, y, 440, 290));
                GUI.Label(new Rect(x + 34, y + 25, 380, 45), b.Stars > 0 ? "远征凯旋" : "远征结束", heading);
                GUI.Label(new Rect(x + 34, y + 86, 380, 113), "破坏率 " + b.Destruction + "%   /   " + b.Stars + " 星\n+ " + b.GoldReward + " 金币\n+ " + b.CrystalReward + " 晶露", label);
                if (GUI.Button(new Rect(x + 32, y + 219, 376, 45), "返回聚落")) ReturnHome();
            }
            if (help)
            {
                float x = Screen.width / 2f - 270, y = Screen.height / 2f - 190;
                Box(new Rect(x, y, 540, 380));
                GUI.Label(new Rect(x + 25, y + 24, 480, 45), "指挥官手册 · 已暂停", heading);
                GUI.Label(new Rect(x + 25, y + 82, 490, 220), "选择底部建筑，再点击空地建造。\n点击建筑：M 移动，U 升级，Del 拆除。\n升级议事堡可提升等级和建造数量上限。\n按 I 查看兵种图鉴、职责与投放顺序。\n远征在外围投兵，按 1—4 切换兵种。\nQ 选择治疗，点击友军附近恢复生命。\n本地进度每15秒保存。", label);
                if (GUI.Button(new Rect(x + 25, y + 315, 490, 42), "继续游戏")) help = false;
            }
            DrawExtraModals();
        }
        private void DrawBattleOverlay()
        {
            if (session.Battle == null) return;
            foreach (Building b in session.Battle.Buildings) if (b.Health > 0 && b.Health < b.MaxHealth) Bar(new Vector3(b.CenterX / 1000f, 3.5f, b.CenterZ / 1000f), b.Health / (float)b.MaxHealth, Gold);
            foreach (Unit u in session.Battle.Units) if (u.Health > 0) Bar(new Vector3(u.X / 1000f, 2, u.Z / 1000f), u.Health / (float)u.Spec.Health, Mint);
            foreach (CombatEffect fx in session.Battle.Effects)
            {
                Vector3 a = worldCamera.WorldToScreenPoint(new Vector3(fx.X / 1000f, 0.7f, fx.Z / 1000f));
                Vector3 b = worldCamera.WorldToScreenPoint(new Vector3(fx.EndX / 1000f, 1.2f, fx.EndZ / 1000f));
                if (fx.Kind < 2)
                {
                    Matrix4x4 old = GUI.matrix;
                    Vector2 start = new Vector2(a.x, Screen.height - a.y), end = new Vector2(b.x, Screen.height - b.y), d = end - start;
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, start);
                    GUI.color = Gold; GUI.DrawTexture(new Rect(start.x, start.y, d.magnitude, 2), Texture2D.whiteTexture); GUI.color = Color.white; GUI.matrix = old;
                }
            }
            if (!session.Battle.Finished && GroundPoint(out Vector3 point))
            {
                string prompt = heal ? "疗愈范围：5格" : session.Battle.CanDeploy(Mathf.FloorToInt(point.x) * 1000 + 500, Mathf.FloorToInt(point.z) * 1000 + 500) ? "可以投兵" : "请在基地外围投兵";
                GUI.Label(new Rect(Input.mousePosition.x + 18, Screen.height - Input.mousePosition.y + 18, 190, 35), prompt, small);
            }
        }
        private void Bar(Vector3 position, float fraction, Color color)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(position); if (screen.z < 0) return;
            GUI.color = Color.black; GUI.DrawTexture(new Rect(screen.x - 20, Screen.height - screen.y, 40, 5), Texture2D.whiteTexture);
            GUI.color = color; GUI.DrawTexture(new Rect(screen.x - 19, Screen.height - screen.y + 1, 38 * fraction, 3), Texture2D.whiteTexture); GUI.color = Color.white;
        }
        private static void Box(Rect rect)
        {
            GUI.color = new Color(0.075f, 0.14f, 0.12f, 0.96f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
        }
    }

    public sealed class BuildingHandle : MonoBehaviour { public int Id; }
}
