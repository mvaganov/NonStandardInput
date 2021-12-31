using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NonStandard.Inputs {
	public class InputMapManager : MonoBehaviour {
		private static Data _instance;

		[Serializable]
		public class Data {
			public int actionGroupIndex = -1;
			public List<InputMapGroup.ActionMapGroup> actionMapGroups = new List<InputMapGroup.ActionMapGroup>();
			internal InputMapManager modelView;
			public void NextActionGroup() {
				++actionGroupIndex;
				while (actionGroupIndex < 0) { actionGroupIndex += actionMapGroups.Count; }
				while (actionGroupIndex >= actionMapGroups.Count) { actionGroupIndex -= actionMapGroups.Count; }
				Refresh();
			}
			public void Refresh() {
				if (actionGroupIndex >= 0 && actionMapGroups.Count > actionGroupIndex) {
					actionMapGroups[actionGroupIndex].Refresh();
				}
			}
			public void SetActionGroup(string groupName) {
				int index = actionMapGroups.FindIndex(g => g.name == groupName);
				if (index < 0) {
					throw new Exception($"unknown group name \"{groupName}\". valid names:\n" + string.Join("\n", actionMapGroups));
				}
				SetActionGroup(index);
			}
			public void SetActionGroup(int index) {
				actionGroupIndex = index;
				Refresh();
			}
			public void NextActionGroup(InputAction.CallbackContext context) {
				switch (context.phase) { case InputActionPhase.Canceled: NextActionGroup(); break; }
			}
		}
		public Data data;
#if UNITY_EDITOR
		[TextArea(1, 10), SerializeField]
		private string activeInputMaps;
		public static void RefreshDebugActiveMap() {
			if (Instance.modelView == null) { return; }
			Instance.modelView.activeInputMaps = string.Join("\n", InputSystem.ListEnabledActions());
		}
#endif
		public static Data Instance {
			get {
				if (_instance != null) return _instance;
				InputMapManager im = FindObjectOfType<InputMapManager>();
				if (im != null && im.data != null) {
					_instance = im.data;
				} else {
					_instance = new Data();
					EventSystem.current.gameObject.AddComponent<InputMapManager>();
				}
				return _instance;
			}
		}

		private void Awake() {
			if (Instance.modelView == null) { Instance.modelView = this; }
		}
		public void Start() {
			if (Instance == data) {
				data.Refresh();
			} else {
				if (data != null && data.actionMapGroups != null && data.actionMapGroups.Count > 0) {
					if (Instance.actionMapGroups == null) {
						Instance.actionMapGroups = data.actionMapGroups;
					} else {
						Instance.actionMapGroups.AddRange(data.actionMapGroups);
					}
				}
				data = Instance;
			}
#if UNITY_EDITOR
			RefreshDebugActiveMap();
#endif
		}

		public static void EnableInputActionMap(string inputMapName) {
			InputMapGroup.EnableInputActionMap(inputMapName, true);
		}
		public static void EnableInputActionMap(string inputMapName, bool enable) {
			InputMapGroup.EnableInputActionMap(inputMapName, enable);
		}
		public static void DisableInputActionMap(string inputMapName) {
			InputMapGroup.EnableInputActionMap(inputMapName, false);
		}

#if UNITY_EDITOR
		private void OnValidate() {
			if (!Application.isPlaying || data == null) { return; }
			data.Refresh();
		}
#endif
	}
}