using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NonStandard.Inputs {
	public class InputMapGroup : MonoBehaviour {
		public enum ActionMapState { Disabled, Enabled, Ignored }
		public ActionMapGroup actionMapGroup;
		public bool GloballyAccessibleGrouping = true;
#if UNITY_EDITOR
		private void OnValidate() {
			if (Application.isPlaying && EventSystem.current != null) {
				MakeAccessible(GloballyAccessibleGrouping);
			}
		}
#endif
		[System.Serializable]
		public class ActionMapToggle {
			public string name;
			public ActionMapState state;
			public bool Enabled {
				get { return state == ActionMapState.Enabled; }
				set {
					bool same = (value && state == ActionMapState.Enabled) || (!value && state == ActionMapState.Disabled);
					if (same || !Application.isPlaying || name == null) { return; }
					Enable(true);
				}
			}
			public void Enable(bool enable) {
				state = enable ? ActionMapState.Enabled : ActionMapState.Disabled;
				PlayerInput[] playerInputs = FindObjectsOfType<PlayerInput>();
				for (int i = 0; i < playerInputs.Length; ++i) {
					ApplyActionMapToggle(playerInputs[i], enable);
				}
			}
			public void Unenable() { Enable(false); }
			public void Enable() { Enable(true); }
			public void ApplyActionMapToggle(PlayerInput pi, bool enable) {
				InputActionMap map = pi.actions.FindActionMap(name);
				//Debug.Log(name + " (" + map+") "+enable);
				if (map == null) {
					Debug.Log($"Could not find {name} in [{string.Join(", ", pi.actions.actionMaps)}]");
					return;
				}
				switch (state) {
					case ActionMapState.Enabled: ApplyActionMapToggle(map, enable); break;
					case ActionMapState.Disabled: ApplyActionMapToggle(map, !enable); break;
				}
			}
			public static void ApplyActionMapToggle(InputActionMap map, bool enable) {
				if (map == null) { return; }
				if (enable) {
					//Debug.Log("applying " + map.name);
					map.Enable();
				} else {
					//Debug.Log("unapplying " + map.name);
					map.Disable();
				}
#if UNITY_EDITOR
				InputMapManager.RefreshDebugActiveMap();
#endif
			}
		}

		[System.Serializable]
		public class ActionMapGroup {
			public string name;
			public List<ActionMapToggle> actionMaps = new List<ActionMapToggle> { };
			public void Refresh() {
				Array.ForEach(FindObjectsOfType<PlayerInput>(), pi => ApplyActionMapToggle(actionMaps, pi));
			}
			public void Enable() {
				Array.ForEach(FindObjectsOfType<PlayerInput>(), pi => ApplyActionMapToggle(actionMaps, pi));
			}
			public void Disable() {
				Array.ForEach(FindObjectsOfType<PlayerInput>(), pi => UnapplyActionMapToggle(actionMaps, pi));
			}
		}

		public static void ApplyActionMapToggle(List<ActionMapToggle> actionMaps, PlayerInput pi) {
			for (int i = 0; i < actionMaps.Count; i++) {
				actionMaps[i].ApplyActionMapToggle(pi, true);
			}
		}
		public static void UnapplyActionMapToggle(List<ActionMapToggle> actionMaps, PlayerInput pi) {
			for (int i = 0; i < actionMaps.Count; i++) {
				actionMaps[i].ApplyActionMapToggle(pi, false);
			}
		}

		public static void EnableInputActionMap(string inputMapName, bool enable) {
			ActionMapToggle amt = new ActionMapToggle { name = inputMapName };
			amt.Enabled = enable;
		}
		public static void EnableInputActionMap(string inputMapName) { EnableInputActionMap(inputMapName, true); }
		public static void DisableInputActionMap(string inputMapName) { EnableInputActionMap(inputMapName, false); }

		public void EnableByButtonState(InputAction.CallbackContext context) {
			switch (context.phase) {
				case InputActionPhase.Started: actionMapGroup.Enable(); break;
				case InputActionPhase.Canceled: actionMapGroup.Disable(); break;
			}
		}

		private void OnEnable() {
			if (!GloballyAccessibleGrouping) return;
			MakeAccessible(true);
		}
		private void OnDisable() {
			if (!GloballyAccessibleGrouping) return;
			MakeAccessible(false);
		}
		private void MakeAccessible(bool accessible) {
			if (accessible) {
				if (!InputMapManager.Instance.actionMapGroups.Contains(actionMapGroup)) {
					InputMapManager.Instance.actionMapGroups.Add(actionMapGroup);
				}
			} else {
				InputMapManager.Instance.actionMapGroups.Remove(actionMapGroup);
			}
		}
	}
}