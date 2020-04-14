﻿using System;
using System.Collections;
using System.Threading;
using CSObjectWrapEditor;
using Extend.AssetService.Editor;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
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

		private static IEnumerator GenerateXLua() {
			Generator.ClearAll();
			Generator.GenAll();
			while( EditorApplication.isCompiling ) {
				yield return null;
			}
		}

		private static IEnumerator RebuildAll(BuildTarget target) {
			yield return RebuildAllABForPlatform(BuildTarget.Android);
			yield return GenerateXLua();

			while( EditorApplication.isCompiling ) {
				yield return null;
			}

			if( SystemInfo.graphicsDeviceName == null ) {
				EditorApplication.Exit(0);
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