using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Reflection;

namespace NonStandard.Inputs {
	public class UserInput : MonoBehaviour {
		[Tooltip("The Character's Controls")]
		public InputActionAsset inputActionAsset;
		[SerializeField] private List<InputControlBinding> inputControlBindings;
		private bool _initialized = false;
		public List<string> actionMapToBindOnStart = new List<string>();
		public Callbacks callbacks;

		public bool IsInitialized => _initialized;

		[Serializable] public class UnityEvent_string : UnityEvent<string> { }
		[Serializable] public class Callbacks {
			public bool enable = true;
			public UnityEvent_string OnInputChange;
		}

		public void AddDefaultActionMapToBind(string mapName) {
			if (actionMapToBindOnStart.IndexOf(mapName) != -1) { return; }
			actionMapToBindOnStart.Add(mapName);
		}

		public bool RemoveDefaultActionMapToBind(string mapName) {
			if (_initialized) { throw new Exception("removing default action map after Start does nothing"); }
			int index = actionMapToBindOnStart.IndexOf(mapName);
			if (index < 0) { return false; }
			actionMapToBindOnStart.RemoveAt(index);
			return true;
		}

		void Start() {
			Bind(inputControlBindings, true);
			_initialized = true;
			actionMapToBindOnStart.ForEach(EnableActionMap);
		}

		public void EnableActionMap(string actionMapName, bool enable) {
			if (enable) {
				EnableActionMap(actionMapName);
				return;
			}
			DisableActionMap(actionMapName);
		}

		/// <summary>
		/// enables actions in the given action map. eg: "CmdLine" enables "CmdLine/UpArrow" and "CmdLine/DownArrow"
		/// </summary>
		public void EnableActionMap(string actionMapName) {
			Debug.Log("enabling action map: "+actionMapName);
			if (IsInitialized) {
				InputActionMap iam = inputActionAsset.FindActionMap(actionMapName);
				if (iam != null) {
					if (!iam.enabled) {
						iam.Enable();
					} else {
						Debug.LogWarning(actionMapName+" already enabled.");
					}
				}
				if (callbacks.enable && callbacks.OnInputChange.GetPersistentEventCount() > 0) {
					callbacks.OnInputChange.Invoke(GetInputDescription());
				}
				return;
			}
			AddDefaultActionMapToBind(actionMapName);
		}

		/// <summary>
		/// disables actions in the given action map. eg: "CmdLine" disables "CmdLine/UpArrow" and "CmdLine/DownArrow"
		/// </summary>
		public void DisableActionMap(string actionMapName) {
			Debug.Log("disabling action map: " + actionMapName);
			if (IsInitialized) {
				inputActionAsset.FindActionMap(actionMapName)?.Disable();
				if (callbacks.enable && callbacks.OnInputChange.GetPersistentEventCount() > 0) {
					callbacks.OnInputChange.Invoke(GetInputDescription());
				}
				return;
			}
			RemoveDefaultActionMapToBind(actionMapName);
		}

		public InputControlBinding GetBinding(string name) { return inputControlBindings.Find(b => b.actionName == name); }
		public void AddBindingIfMissing(InputControlBinding binding, bool enabled = true) {
			if (inputControlBindings != null && inputControlBindings.Find(b => b.description == binding.description) != null) {
				return;
			}
			AddBinding(binding, enabled);
		}
		public void AddBinding(InputControlBinding b, bool enabled = true) {
			if (inputControlBindings == null) { inputControlBindings = new List<InputControlBinding>(); }
			inputControlBindings.Add(b);
			b.Bind(inputActionAsset, enabled);
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
		private void OnEnable() {
			if (!_initialized) return;
			Bind(inputControlBindings, true);
		}
		private void OnDisable() {
			if (!_initialized) return;
			Bind(inputControlBindings, false);
		}
		public IList<string> GetAllActionNames() { return InputControlBinding.GetAllActionNames(inputActionAsset); }

		public static string GetInputDescription() {
			StringBuilder sb = new StringBuilder();
			// find all of the enabled actions, and group them by their ActionMap
			List<InputAction> allEnabledActions = InputSystem.ListEnabledActions();
			Dictionary<InputActionMap, List<InputAction>> allEnabledActionsByMap = new Dictionary<InputActionMap, List<InputAction>>();
			List<InputActionMap> mapOrder = new List<InputActionMap>();
			List<InputAction> unmapped = null;
			for (int i = 0; i < allEnabledActions.Count; ++i) {
				InputAction ia = allEnabledActions[i];
				if (ia == null) {
					if (unmapped == null) { unmapped = new List<InputAction>(); }
					unmapped.Add(ia);
					continue;
				}
				if(!allEnabledActionsByMap.TryGetValue(ia.actionMap, out List<InputAction> enabledActions)){
					enabledActions = new List<InputAction>();
					allEnabledActionsByMap[ia.actionMap] = enabledActions;
				}
				enabledActions.Add(ia);
			}
			// put the input maps in a good order
			foreach (var kvp in allEnabledActionsByMap) {
				InputActionMap m = kvp.Key;
				if (m.name == "Player") {
					mapOrder.Insert(0, m);
				} else {
					mapOrder.Add(m);
				}
			}
			// generate the text based on each InputAction in each ActionMap. show Binding.description if available.
			foreach (InputActionMap m in mapOrder) {
				sb.Append(m.name).Append("\n");
				List<InputAction> actions = allEnabledActionsByMap[m];
				foreach (var action in actions) {
					if (action == null) continue;
					List<string> inputBindings = new List<string>();
					for (int i = 0; i < action.bindings.Count; ++i) {
						string bPath = action.bindings[i].path;
						if (bPath.StartsWith("<Keyboard>") || bPath.StartsWith("<Mouse>")) {
							inputBindings.Add(bPath);
						}
					}
					InputControlBinding.Active.TryGetValue(action, out InputControlBinding binding);
					List<string> functionPayload = new List<string>();
					if (binding != null && binding.actionEventHandler != null) {
						for (int i = 0; i < binding.actionEventHandler.GetPersistentEventCount(); ++i) {
							UnityEngine.Object target = binding.actionEventHandler.GetPersistentTarget(i);
							string methodName = binding.actionEventHandler.GetPersistentMethodName(i);
							functionPayload.Add(target.name + "." + methodName);
						}
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
			}
			// if there were any input actions that were not part of an action map, show those too.
			if (unmapped != null && unmapped.Count > 0) {
				sb.AppendLine("---").Append("\n");
				foreach (var action in unmapped) {
					InputControlBinding binding = InputControlBinding.Active[action];
					sb.Append("  ").Append(action.name).Append(" ").Append(binding.description).
						Append("\n    ").Append(string.Join("\n    ", binding.bindingPaths)).Append("\n");
				}
			}
			return sb.ToString();
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
