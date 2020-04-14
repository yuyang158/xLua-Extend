﻿using System;
using System.Collections;
using System.IO;
using System.Threading;
using CSObjectWrapEditor;
using Extend.AssetService.Editor;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Extend.Editor {
	public static class EditorUtility {
		[MenuItem("Tools/常用工具/打开Log目录")]
		private static void OpenPersistencePath() {
			UnityEditor.EditorUtility.RevealInFinder(Application.persistentDataPath);
		}

		private static IEnumerator RebuildAllABForPlatform(BuildTarget target) {
			var finish = false;
			Debug.LogWarning($"Rebuild all ab for platform {target}");
			StaticAssetBundleWindow.RebuildAllAssetBundles(target, () => {
				Debug.LogWarning($"Rebuild all ab for platform {target} success");
				finish = true;
			});

			while( !finish ) {
				yield return null;
			}
		}

		private static void GenerateXLua() {
			Generator.ClearAll();
			Generator.GenAll();
		}

		private static IEnumerator RebuildAll(BuildTarget target) {
			yield return RebuildAllABForPlatform(target);
			GenerateXLua();

			Directory.Delete(Application.dataPath + "/Resources", true);

			var buildPlayerOptions = new BuildPlayerOptions();
			var scenes = new string[EditorBuildSettings.scenes.Length];
			for( var i = 0; i < scenes.Length; i++ ) {
				scenes[i] = EditorBuildSettings.scenes[i].path;
			}
			buildPlayerOptions.scenes = scenes;
			string platform;
			if( target == BuildTarget.Android ) {
				platform = "Android";
			}
			else if( target == BuildTarget.iOS ) {
				platform = "iOS";
			}
			else {
				platform = "StandaloneWindows";
			}
			
			buildPlayerOptions.assetBundleManifestPath = $"{Application.persistentDataPath}/ABBuild/{platform}.manifest";
			buildPlayerOptions.locationPathName = $"{platform}Build";
			buildPlayerOptions.target = target;
			buildPlayerOptions.options = BuildOptions.Development | BuildOptions.AcceptExternalModificationsToPlayer;

			var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
			var summary = report.summary;
			if( summary.result == BuildResult.Succeeded ) {
				Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
			}

			if( summary.result == BuildResult.Failed ) {
				Debug.Log("Build failed");
			}

			Debug.LogWarning("Compiling finished");
			if( Environment.CurrentDirectory.Contains("Jenkins") ) {
				Debug.LogWarning($"DEVICE NAME : {SystemInfo.graphicsDeviceName}");
				EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
			}
		}

		[MenuItem("Tools/CI/Rebuild Android")]
		private static void RebuildAllAndroid() {
			EditorCoroutineUtility.StartCoroutineOwnerless(RebuildAll(BuildTarget.Android));
		}

		[MenuItem("Tools/CI/Rebuild iOS")]
		private static void RebuildAllABiOS() {
			EditorCoroutineUtility.StartCoroutineOwnerless(RebuildAll(BuildTarget.iOS));
		}

		[MenuItem("Tools/CI/Rebuild AB Windows")]
		private static void RebuildAllABWindows() {
			EditorCoroutineUtility.StartCoroutineOwnerless(RebuildAll(BuildTarget.StandaloneWindows64));
		}
	}
}