using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;
using UnityEngine.EventSystems;

namespace NonStandard.Inputs {
	public class UserInput : MonoBehaviour {
		[Tooltip("The Character's Controls")]
		public InputActionAsset inputActionAsset;

		[SerializeField] private List<InputControlBinding> inputControlBindings;
		bool initialized = false;

		public string[] actionMapToBindOnStart = new string[0];

		void Start() {
			Bind(inputControlBindings, true);
			Array.ForEach(actionMapToBindOnStart, actionMapName => inputActionAsset.FindActionMap(actionMapName).Enable());
			initialized = true;
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
				inputs[i].Bind(inputActionAsset, enable);
			}
		}
		private void OnEnable() {
			if (!initialized) return;
			Bind(inputControlBindings, true);
		}
		private void OnDisable() {
			if (!initialized) return;
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
					if (inputBindings.Count == 0) continue;
					InputControlBinding.Active.TryGetValue(action, out InputControlBinding binding);
					string desc = binding != null ? " -- " + binding.description : "";
					sb.Append("  ").Append(action.name).Append(" ").Append(desc).Append("\n    ");
					sb.Append(string.Join("\n    ", inputBindings)).Append("\n");
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
