#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LockSimulator
{
    public static class LockSimulatorProjectSetup
    {
        [MenuItem("Lock Simulator/Configure Project")]
        public static void ConfigureProject()
        {
            PlayerSettings.productName = "开锁模拟器";
            PlayerSettings.companyName = "Lock Lab Studio";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.locklab.locksimulator");
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.androidIsGame = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/AppIcon.png");
            if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon });
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetArchitecture(BuildTargetGroup.Android, 1);
            // 桌面端：可缩放的竖屏窗口，而不是固定大小/全屏。
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            var scenePath = "Assets/Scenes/Main.unity";
            if (!System.IO.File.Exists(scenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var app = new GameObject("LockSimulatorApp");
                app.AddComponent<LockSimulatorApp>();
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Lock Simulator project configured.");
        }

        [MenuItem("Lock Simulator/Build Android")]
        public static void BuildAndroid()
        {
            ConfigureProject();
            var output = "Builds/Android/LockSimulator.apk";
            System.IO.Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, output, BuildTarget.Android, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Android build failed: " + report.summary.result);
            Debug.Log("Android build complete: " + output);
        }

        [MenuItem("Lock Simulator/Build Mac")]
        public static void BuildMac()
        {
            ConfigureProject();
            var output = "Builds/Mac/LockSimulator.app";
            System.IO.Directory.CreateDirectory("Builds/Mac");
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, output, BuildTarget.StandaloneOSX, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Mac build failed: " + report.summary.result);
            Debug.Log("Mac build complete: " + output);
        }

        /// <summary>
        /// 批处理验证：按物理规则逐项检查仿真，再构建剖面视图和首页 UI。
        /// </summary>
        public static void RuntimeSmokeTest()
        {
            Require(LockCatalog.All.Count == 5, "expected 5 locks");

            // 每把锁、多个随机种子，按卡位顺序逐根拨开必须能打开，且全程无过推无重置。
            foreach (var spec in LockCatalog.All)
            {
                for (int seed = 1; seed <= 30; seed++)
                {
                    var sim = new PinTumblerSim(spec, seed);
                    Require(SimBot.Solve(sim), spec.name + " seed " + seed + " did not open");
                    Require(sim.resets == 0 && sim.oversets == 0, spec.name + " seed " + seed + " had resets/oversets");
                    var ranks = new System.Collections.Generic.HashSet<int>();
                    foreach (var pin in sim.pins) Require(ranks.Add(pin.rank), "duplicate bind rank");
                }
            }

            var plain = new LockSpec { pins = 4, tolerance = 0.03f, spools = 0, maxTension = 0.8f };

            // 过推：抬过头会卡死；放松张力后可以恢复并再次设定。
            var a = new PinTumblerSim(plain, 11);
            a.SetTension(0.3f);
            int bind = a.BindingIndex();
            a.Pick(bind, 1f);
            Run(a, 3f);
            Require(a.pins[bind].state == PinState.Overset && a.oversets == 1, "overset expected");
            a.Release();
            a.SetTension(0.1f);
            Run(a, 0.1f);
            Require(a.pins[bind].state == PinState.Rest, "overset pin should free when tension eases");
            a.SetTension(0.3f);
            a.Pick(bind, a.pins[bind].setH);
            Run(a, 3f);
            Require(a.pins[bind].state == PinState.Set, "pin should set after recovery");

            // 张力太大：卡住的弹子抬不动。
            var b = new PinTumblerSim(plain, 12);
            b.SetTension(b.maxTension + 0.05f);
            int bb = b.BindingIndex();
            b.Pick(bb, b.pins[bb].setH);
            Run(b, 3f);
            Require(b.pins[bb].lift < 0.001f && b.pins[bb].state == PinState.Rest, "over-tension should block lifting");

            // 张力松到接近零：已设定的弹子全部弹回。
            var c = new PinTumblerSim(plain, 13);
            SimBot.Solve(c, 1);
            Require(c.SetCount == 1, "one pin should be set");
            c.SetTension(0.02f);
            Run(c, 0.1f);
            Require(c.SetCount == 0 && c.resets == 1, "releasing tension should reset set pins");

            // 非卡位的弹子是软的：抬到设定高度也不会被设定。
            var d = new PinTumblerSim(plain, 14);
            d.SetTension(0.3f);
            int free = -1;
            for (int i = 0; i < d.pins.Length; i++) if (i != d.BindingIndex()) { free = i; break; }
            d.Pick(free, d.pins[free].setH);
            Run(d, 3f);
            Require(d.pins[free].state == PinState.Rest, "non-binding pin must not set");
            d.Release();
            Run(d, 1.5f);
            Require(d.pins[free].lift < 0.001f, "non-binding pin should spring back");

            // 蘑菇弹子：张力大时只能到假设定，放松张力后才能真正设定。
            var e = new PinTumblerSim(plain, 15);
            int eb = e.BindingIndex();
            e.pins[eb].spool = true;
            e.pins[eb].setH = Mathf.Max(e.pins[eb].setH, 0.4f);
            e.SetTension(0.5f);
            e.Pick(eb, e.pins[eb].setH);
            Run(e, 4f);
            Require(e.pins[eb].state == PinState.FalseSet, "spool should false-set under high tension");
            Require(e.pins[eb].lift < e.pins[eb].setH - e.tolerance, "spool must not pass false set under high tension");
            e.SetTension(0.25f);
            Run(e, 4f);
            Require(e.pins[eb].state == PinState.Set, "spool should set after easing tension");

            // 舞台：各种弹子数下构建、刷新（透视/盲拨）不抛异常。
            foreach (int pins in new[] { 3, 5, 6 })
            {
                var host = new GameObject("StageTest", typeof(RectTransform)).GetComponent<RectTransform>();
                var stage = new LockStage(host);
                stage.Rebuild(pins);
                var sim = new PinTumblerSim(new LockSpec { pins = pins, tolerance = 0.04f, spools = 1, maxTension = 0.7f }, pins);
                SimBot.Solve(sim, 1);
                sim.Pick(sim.BindingIndex(), 0.3f);
                stage.Refresh(sim, true, true, 0.016f, 0f);
                stage.Refresh(sim, false, false, 0.016f, 0f);
                Require(host.childCount > 20, "stage should have been built");
                Object.DestroyImmediate(host.gameObject);
            }

            var root = new GameObject("LockSimulatorSmokeTest");
            try
            {
                var app = root.AddComponent<LockSimulatorApp>();
                typeof(LockSimulatorApp).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            Debug.Log("Lock Simulator runtime smoke test passed: physics rules, stage and home UI verified.");
        }

        static void Run(PinTumblerSim sim, float seconds)
        {
            for (float t = 0f; t < seconds; t += 1f / 60f) sim.Tick(1f / 60f);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new System.Exception("Smoke test failed: " + message);
        }
    }
}
#endif
