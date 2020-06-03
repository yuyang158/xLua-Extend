using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Extend.Asset.Editor {
	[Serializable]
	public class StaticABSetting {
		public enum Operation {
			ALL_IN_ONE,
			STAY_RESOURCES,
			EACH_FOLDER_ONE,
			EACH_A_AB
		}
		
		public DefaultAsset FolderPath;
		public Operation Op;

		public SpecialBundleLoadLogic[] LoadLogics;

		public string Path => FolderPath ? AssetDatabase.GetAssetPath( FolderPath ) : string.Empty;
	}

	[Serializable]
	public class SpecialBundleLoadLogic {
		[Serializable]
		public enum BundleLoadLogic {
			None,
			DontUnload
		}
		
		public string BundleName;
		public BundleLoadLogic LoadLogic = BundleLoadLogic.None;

	}
	
	public class StaticABSettings : ScriptableObject {
		public DefaultAsset SpriteAtlasFolder;
		
		public StaticABSetting[] Settings;

		public Object[] ExtraDependencyAssets;

		public bool ContainExtraObject(Object obj) {
			return Array.Find(ExtraDependencyAssets, dep => dep == obj);
		}
	}
}