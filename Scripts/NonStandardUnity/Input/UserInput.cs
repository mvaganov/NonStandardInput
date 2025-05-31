// code by michael vaganov, released to the public domain via the unlicense (https://unlicense.org/)
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Linq;
using UnityEditor;

namespace NonStandard.Inputs {
	public class UserInput : MonoBehaviour {
#if HAS_INPUTSYSTEM
		[Tooltip("The Character's Controls")]
		public InputActionAsset inputActionAsset;
#endif
		public List<InputControlBinding> InputControlBindings;
		private bool _initialized = false;
		public List<string> ActionMapToBindOnStart = new List<string>();
		public Callbacks callbacks;

		public bool IsInitialized => _initialized;

		[Serializable] public class UnityEvent_string : UnityEvent<string> { }
		[Serializable] public class Callbacks {
			public bool enable = true;
			public UnityEvent_string OnInputChange;
		}

		private void OnEnable() {
			if (!_initialized) return;
			Bind(InputControlBindings, true);
		}

		void Start() {
			Bind(InputControlBindings, true);
			_initialized = true;
			ActionMapToBindOnStart.ForEach(EnableActionMap);
		}

		private void OnDisable() {
			if (!_initialized) return;
			Bind(InputControlBindings, false);
		}

		/// <summary>
		/// enables actions in the given action map. eg: "CmdLine" enables "CmdLine/UpArrow" and "CmdLine/DownArrow"
		/// </summary>
		public void EnableActionMap(string actionMapName) {
			// Debug.Log("enabling action map: "+actionMapName);
			if (IsInitialized) {
				InputActionMap iam = inputActionAsset.FindActionMap(actionMapName);
				if (iam != null) {
					if (!iam.enabled) {
						//Debug.Log("enabling "+ actionMapName);
						iam.Enable();
					} else {
						Debug.LogWarning(actionMapName + " already enabled.");
					}
				}
				if (callbacks.enable && callbacks.OnInputChange.GetPersistentEventCount() > 0) {
					callbacks.OnInputChange.Invoke(GetInputDescription());
				}
				return;
			}
			AddDefaultActionMapToBind(actionMapName);
		}

		public void AddDefaultActionMapToBind(string mapName) {
			if (ActionMapToBindOnStart.IndexOf(mapName) != -1) { return; }
			ActionMapToBindOnStart.Add(mapName);
		}

		public bool RemoveDefaultActionMapToBind(string mapName) {
			if (_initialized) { throw new Exception("removing default action map after Start does nothing"); }
			int index = ActionMapToBindOnStart.IndexOf(mapName);
			if (index < 0) { return false; }
			ActionMapToBindOnStart.RemoveAt(index);
			return true;
		}

		public void EnableActionMap(string actionMapName, bool enable) {
			if (enable) {
				EnableActionMap(actionMapName);
				return;
			}
			DisableActionMap(actionMapName);
		}

		/// <summary>
		/// disables actions in the given action map. eg: "CmdLine" disables "CmdLine/UpArrow" and "CmdLine/DownArrow"
		/// </summary>
		public void DisableActionMap(string actionMapName) {
			// Debug.Log("disabling action map: " + actionMapName);
			if (IsInitialized) {
				inputActionAsset.FindActionMap(actionMapName)?.Disable();
				if (callbacks.enable && callbacks.OnInputChange.GetPersistentEventCount() > 0) {
					callbacks.OnInputChange.Invoke(GetInputDescription());
				}
				return;
			}
			RemoveDefaultActionMapToBind(actionMapName);
		}

		public void AddBindingIfMissing(InputControlBinding binding, bool enabled = true) {
			if (InputControlBindings != null && InputControlBindings.Find(b => b.description == binding.description) != null) {
				return;
			}
			AddBinding(binding, enabled);
		}

		public void AddBinding(InputControlBinding b, bool enabled = true) {
			if (InputControlBindings == null) { InputControlBindings = new List<InputControlBinding>(); }
			InputControlBindings.Add(b);
			b.Bind(inputActionAsset, enabled);
#if UNITY_EDITOR
			EditorUtility.SetDirty(this);
#endif
		}

		public void Bind(IList<InputControlBinding> inputs, bool enable) {
			if (inputActionAsset == null) {
				inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
			}
			for (int i = 0; i < inputs.Count; ++i) {
				//Debug.Log("binding "+inputs[i].actionName);
				inputs[i].Bind(inputActionAsset, enable);
			}
		}

		public void DisableBinding(string actionName) {
			InputControlBinding b = GetBinding(actionName);
			if (b != null) { b.Bind(inputActionAsset, false); }
		}

		public void EnableBinding(string actionName) {
			InputControlBinding b = GetBinding(actionName);
			if (b != null) { b.Bind(inputActionAsset, true); }
		}

		public InputControlBinding GetBinding(string name) {
			return InputControlBindings.Find(b => b.actionName == name);
		}

		public IList<string> GetAllActionNames() {
			return InputControlBinding.GetAllActionNames(inputActionAsset);
		}

		private static string[] defaultPreferredFirst = new string[] { "Player" };
		private static string[] defaultPreferredLast = new string[] { "UI" };
		public static string GetInputDescription() => GetInputDescription(defaultPreferredLast, defaultPreferredLast);
		public static string GetInputDescription(IList<string> preferredFirst, IList<string> preferredLast) {
			StringBuilder sb = new StringBuilder();
			Dictionary<InputActionMap, List<InputAction>> allEnabledActionsByMap = 
				AllEnabledActionsByMap(out List<InputAction> unmapped);
			List<InputActionMap> mapOrder = new List<InputActionMap>(allEnabledActionsByMap.Keys);
			OrderListByPreference(mapOrder, preferredFirst, preferredLast);
			foreach (InputActionMap inputActionMap in mapOrder) {
				AppendInputActionMapInfo(sb, inputActionMap, allEnabledActionsByMap);
			}
			if (unmapped != null && unmapped.Count > 0) {
				sb.AppendLine("---").Append("\n");
				unmapped.ForEach(action => AppendInputActionInfo(sb, action));
			}
			return sb.ToString();
		}

		private static void OrderListByPreference(List<InputActionMap> mapOrder, IList<string> preferredFirst, IList<string> preferredLast) {
			int index = 0;
			for (int order = 0; order < preferredFirst.Count; order++) {
				for (int i = 0; i < mapOrder.Count; i++) {
					InputActionMap m = mapOrder[i];
					if (m.name == preferredFirst[order]) {
						mapOrder.RemoveAt(i);
						mapOrder.Insert(index, m);
						index++;
						break;
					}
				}
			}
			for (int order = 0; order < preferredLast.Count; order++) {
				for (int i = 0; i < mapOrder.Count; i++) {
					InputActionMap m = mapOrder[i];
					if (m.name == preferredLast[order]) {
						mapOrder.RemoveAt(i);
						mapOrder.Insert(mapOrder.Count - index, m);
						index++;
						break;
					}
				}
			}
		}

		public static Dictionary<InputActionMap, List<InputAction>> AllEnabledActionsByMap(out List<InputAction> unmapped) {
			unmapped = null;
			List<InputAction> allEnabledActions = InputSystem.ListEnabledActions();
			Dictionary<InputActionMap, List<InputAction>> allEnabledActionsByMap = new Dictionary<InputActionMap, List<InputAction>>();
			for (int i = 0; i < allEnabledActions.Count; ++i) {
				InputAction ia = allEnabledActions[i];
				if (ia == null) {
					if (unmapped == null) { unmapped = new List<InputAction>(); }
					unmapped.Add(ia);
					continue;
				}
				if (!allEnabledActionsByMap.TryGetValue(ia.actionMap, out List<InputAction> enabledActions)) {
					enabledActions = new List<InputAction>();
					allEnabledActionsByMap[ia.actionMap] = enabledActions;
				}
				enabledActions.Add(ia);
			}
			return allEnabledActionsByMap;
		}

		public static void AppendInputActionMapInfo(StringBuilder sb, InputActionMap inputActionMap, 
			Dictionary<InputActionMap, List<InputAction>> allEnabledActionsByMap) {
			sb.Append(inputActionMap.name).Append("\n");
			List<InputAction> actions = allEnabledActionsByMap[inputActionMap];
			foreach (InputAction action in actions) {
				AppendInputActionInfo(sb, action);
			}
		}

		public static void AppendInputActionInfo(StringBuilder sb, InputAction action) {
			if (action == null) return;
			List<string> inputBindings = new List<string>(action.bindings.Select(binding => binding.path));
			InputControlBinding.Active.TryGetValue(action, out InputControlBinding binding);
			List<string> functionPayload = new List<string>();
			if (binding != null && binding.actionEventHandler != null) {
				functionPayload.AddRange(binding.GetPersistentEvents().Select(binding =>
					((UnityEngine.Object)binding.target).name + "." + binding.methodName));
			}
			string desc = binding != null ? " -- " + binding.description : "";
			sb.Append("  ").Append(action.name).Append(" ").Append(desc);
			if (functionPayload.Count > 0) {
				sb.Append(" // ").Append(string.Join(", ", functionPayload));
			}
			sb.Append("\n");
			if (inputBindings.Count > 0) {
				sb.Append("    input: ").Append(string.Join(", ", inputBindings)).Append("\n");
			}
		}

		public static bool IsMouseOverUIObject() {
			return IsPointerOverUIObject(Mouse.current.position.ReadValue());
		}

		public static bool IsPointerOverUIObject(Vector2 pointerPositon) {
			PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
			eventDataCurrentPosition.position = pointerPositon;
			List<RaycastResult> results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
			return results.Count > 0;
		}
	}
}
