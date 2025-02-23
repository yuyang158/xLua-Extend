﻿using Extend.Common;
using TMPro;
using UnityEngine;

namespace Extend.UI.i18n {
	[RequireComponent(typeof(TextMeshProUGUI)), DisallowMultipleComponent]
	public class StaticI18nText : MonoBehaviour {
		[SerializeField]
		private string m_key;

		public string Key {
			get => m_key;
			set => m_key = value;
		}

		[SerializeField]
		private bool m_forceDynamic;

		public bool ForceDynamic => m_forceDynamic;
		private void Awake() {
			if( ForceDynamic || string.IsNullOrEmpty(m_key) ) {
				return;
			}
			var txt = GetComponent<TextMeshProUGUI>();
			var i18NService = CSharpServiceManager.Get<I18nService>(CSharpServiceManager.ServiceType.I18N);
			if(!i18NService.GetText(m_key, out var text)) {
				Debug.LogWarning($"Key not found {m_key}.", txt);
			}
			else {
				txt.text = text;
			}
		}

		public string GetI18nText() {
			if( string.IsNullOrEmpty(m_key) ) return string.Empty;
			var i18NService = CSharpServiceManager.Get<I18nService>(CSharpServiceManager.ServiceType.I18N);
			if(!i18NService.GetText(m_key, out var text)) {
				Debug.LogWarning($"Key not found {m_key}.");
			}
			return text;
		}
	}
}
