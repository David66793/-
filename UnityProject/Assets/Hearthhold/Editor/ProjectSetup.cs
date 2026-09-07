using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hearthhold.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        static ProjectSetup()
        {
            EditorApplication.delayCall += delegate
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !File.Exists("Assets/Hearthhold/Scenes/Main.unity")) Prepare();
            };
        }

        [MenuItem("Hearthhold/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Hearthhold/Settings");
            Directory.CreateDirectory("Assets/Hearthhold/Resources");
            Directory.CreateDirectory("Assets/Hearthhold/Scenes");
            const string pipelinePath = "Assets/Hearthhold/Settings/HearthholdURP.asset";
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                UniversalRendererData renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Hearthhold/Settings/HearthholdRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.msaaSampleCount = 4;
                pipeline.shadowDistance = 100;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Hearthhold/Resources/ModelPalette.mat") == null)
            {
                Shader shader = Shader.Find("Hearthhold/VertexLit");
                if (shader == null) throw new BuildFailedException("Hearthhold/VertexLit shader failed to import.");
                AssetDatabase.CreateAsset(new Material(shader), "Assets/Hearthhold/Resources/ModelPalette.mat");
            }
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Hearthhold/Resources/WorldPalette.mat") == null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetFloat("_Smoothness", 0.12f);
                AssetDatabase.CreateAsset(material, "Assets/Hearthhold/Resources/WorldPalette.mat");
            }
            PlayerSettings.companyName = "Hearthhold Studio";
            PlayerSettings.productName = "Hearthhold";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            // Input only uses built-in legacy mouse/keyboard APIs in this milestone.
            UnityEngine.Object[] playerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerAssets.Length > 0)
            {
                SerializedObject settings = new SerializedObject(playerAssets[0]);
                SerializedProperty input = settings.FindProperty("activeInputHandler");
                if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            }
            if (!File.Exists("Assets/Hearthhold/Scenes/Main.unity"))
            {
                // Do not replace an existing user's scene. Save and close a separate additive scene.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, "Assets/Hearthhold/Scenes/Main.unity");
                EditorSceneManager.CloseScene(scene, true);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Hearthhold/Scenes/Main.unity", true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Hearthhold/Open main scene")]
        public static void OpenScene()
        {
            Prepare();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene("Assets/Hearthhold/Scenes/Main.unity");
        }

        [MenuItem("Hearthhold/Build Windows x64")]
        public static void BuildWindows()
        {
            Prepare();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/WindowsUnity/Hearthhold.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Hearthhold/Scenes/Main.unity" },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = output,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Windows build failed: " + report.summary.result);
            Debug.Log("Windows build ready: " + output);
        }
    }
}
