using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotCell.Editor
{
    [InitializeOnLoad]
    public static class PrototypeProject
    {
        const string ScenePath = "Assets/HotCell/Scenes/Prototype.unity";

        static PrototypeProject()
        {
            EditorApplication.delayCall += GenerateOnFirstImport;
        }

        static void GenerateOnFirstImport()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += GenerateOnFirstImport;
                return;
            }
            if (!File.Exists(ScenePath)) Generate();
        }

        [MenuItem("HOT CELL/Generate Prototype")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before generating the prototype scene.");
            PlayerSettings.companyName = "HOT CELL";
            PlayerSettings.productName = "HOT CELL";
            PlayerSettings.runInBackground = true;
            var settingAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingAssets.Length == 0) throw new BuildFailedException("Unity PlayerSettings asset is unavailable.");
            var settings = new SerializedObject(settingAssets[0]);
            var microphone = settings.FindProperty("microphoneUsageDescription");
            if (microphone != null) microphone.stringValue = "HOT CELL uses the microphone for push-to-talk proximity voice during multiplayer shifts.";
            var handler = settings.FindProperty("activeInputHandler");
            if (handler != null) handler.intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EnsureRuntimeShaders();
            if (File.Exists(ScenePath))
            {
                IncludeSceneInBuild();
                Debug.Log("HOT CELL: existing prototype scene preserved at " + ScenePath);
                return;
            }

            Directory.CreateDirectory("Assets/HotCell/Scenes");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var bootstrap = new GameObject("HotCellBootstrap");
                bootstrap.AddComponent<HotCellBootstrap>();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save " + ScenePath);
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousScene);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.Refresh();
            IncludeSceneInBuild();
            Debug.Log("HOT CELL: prototype generated. Open " + ScenePath + " and press Play.");
        }

        static void EnsureRuntimeShaders()
        {
            // Runtime-created materials still need shader references in the player build.
            const string folder = "Assets/HotCell/Resources";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var shader = Shader.Find("Standard");
            if (shader == null) throw new BuildFailedException("HOT CELL requires the built-in Standard shader.");
            const string opaquePath = folder + "/GreyboxSurface.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(opaquePath) == null)
            {
                var material = new Material(shader) { name = "GreyboxSurface" };
                material.SetFloat("_Glossiness", 0.18f);
                AssetDatabase.CreateAsset(material, opaquePath);
            }
            const string glassPath = folder + "/GreyboxGlass.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(glassPath) == null)
            {
                var material = new Material(shader) { name = "GreyboxGlass", color = new Color(0.38f, 0.41f, 0.36f, 0.18f) };
                material.SetFloat("_Mode", 2f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = 3000;
                AssetDatabase.CreateAsset(material, glassPath);
            }
            AssetDatabase.SaveAssets();
        }

        static void IncludeSceneInBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            bool found = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ScenePath)
                {
                    scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                    found = true;
                }
                else scenes.Add(scene);
            }
            if (!found) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("HOT CELL/Build/macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/HOT CELL.app");

        [MenuItem("HOT CELL/Build/Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/HOT CELL.exe");

        [MenuItem("HOT CELL/Build/Linux")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/HOT CELL.x86_64");

        static void Build(BuildTarget target, string destination)
        {
            Generate();
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new BuildFailedException("Install the Unity build support module for " + target + " in Unity Hub.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = destination,
                target = target,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("HOT CELL build failed: " + report.summary.result + "; errors: " + report.summary.totalErrors);
            Debug.Log("HOT CELL built: " + destination + " (" + report.summary.totalSize + " bytes)");
        }
    }
}
