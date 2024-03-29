// code by michael vaganov, released to the public domain via the unlicense (https://unlicense.org/)
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using System.Linq;

namespace NonStandard.Inputs {
	public enum ControlType { Button, Vector2, Vector3, Analog, Axis, Bone, Digital, Double, Dpad, Eyes, Integer, Quaternion, Stick, Touch, Key }

	[Serializable]
	public class InputControlBinding {
		/// <summary>
		/// used by special generated composite key input
		/// </summary>
		public const string CompositePrefix = "^COMPOSITE^";
		public static Dictionary<InputAction, InputControlBinding> Active = new Dictionary<InputAction, InputControlBinding>();
		public static Action OnActiveChange;

		public string description, actionName;
		public ControlType controlType;
		[InputControl] public string[] bindingPaths = null;
		public UnityInputActionEvent actionEventHandler = new UnityInputActionEvent();
		internal const char separator = '/';
		[Serializable] public class UnityInputActionEvent : UnityEvent<InputAction.CallbackContext> { }

		public string ActionMapName {
			get {
				int index = actionName.IndexOf(separator);
				return index >= 0 ? actionName.Substring(0, index) : actionName;
			}
		}

		public string ActionInputName {
			get {
				int index = actionName.IndexOf(separator);
				return index >= 0 ? actionName.Substring(index + 1) : actionName;
			}
		}

		public InputControlBinding(string description, string actionName, ControlType t, EventBind e, string c = null) 
			: this(description, actionName, t, e, new string[] { c }) {}

		public InputControlBinding(string description, string actionName, ControlType t, EventBind e, IEnumerable<string> c = null) {
			this.description = description; this.actionName = actionName; controlType = t; 
			bindingPaths = c != null ? c.ToArray() : null;
			e.Bind(actionEventHandler);
		}

		public InputControlBinding(string description, string actionName, ControlType t, EventBind[] events, IEnumerable<string> c = null) {
			this.description = description; this.actionName = actionName; controlType = t;
			bindingPaths = c != null ? c.ToArray() : null;
			Array.ForEach(events, e=>e.Bind(actionEventHandler));
		}

		public void Bind(InputActionAsset inputActionAsset, bool enable) {
			InputAction ia = FindAction(inputActionAsset, actionName, controlType, bindingPaths);
			if (ia == null) {
				string allActions = string.Join(", ", string.Join(", ", InputControlBinding.GetAllActionNames(inputActionAsset)));
				Debug.LogWarning($"Missing {actionName} ({controlType}). Did you mean one of these: [{allActions}]");
				return;
			}
			if (enable) {
				BindAction(ia);
			} else {
				UnbindAction(ia);
			}
		}

		public void BindAction(InputAction ia) {
			if (actionEventHandler != null) {
				UnbindAction(ia);
				ia.started += actionEventHandler.Invoke;
				ia.performed += actionEventHandler.Invoke;
				ia.canceled += actionEventHandler.Invoke;
				Active[ia] = this;
				OnActiveChange?.Invoke();
			}
		}

		public void UnbindAction(InputAction ia) {
			if (actionEventHandler != null) {
				ia.started -= actionEventHandler.Invoke;
				ia.performed -= actionEventHandler.Invoke;
				ia.canceled -= actionEventHandler.Invoke;
				Active.Remove(ia);
				OnActiveChange?.Invoke();
			}
		}

		public EventBind[] GetPersistentEvents() {
			EventBind[] events = new EventBind[actionEventHandler.GetPersistentEventCount()];
			for (int i = 0; i < events.Length; i++) {
				events[i] = new EventBind(actionEventHandler.GetPersistentTarget(i),
					actionEventHandler.GetPersistentMethodName(i));
			}
			return events;
		}

		public static InputAction FindAction(InputActionAsset actionAsset, string expectedActionName, 
		ControlType actionInputType, IEnumerable<string> bindingPathToCreateWithIfMissing = null) {
			string controlType = actionInputType.ToString();
			foreach (InputActionMap actionMap in actionAsset.actionMaps) {
				string n = separator + expectedActionName;
				foreach (var action in actionMap.actions) {
					string actionName = actionMap.name + separator + action.name;
					if (action.name == expectedActionName || actionName == expectedActionName || actionName.Contains(n)) {
						if (action.expectedControlType != controlType) {
							Debug.LogWarning("found " + expectedActionName + " in " + actionAsset.name + ", but Input type is " + 
								action.expectedControlType + ", not " + actionInputType + ".");
						} else {
							return action;
						}
					}
				}
			}
			if (bindingPathToCreateWithIfMissing != null) {
				return CreateInputActionBinding(actionAsset, expectedActionName, controlType, bindingPathToCreateWithIfMissing);
			}
			return null;
		}

		public static List<InputAction> GetActiveActions(InputActionMap actionMap) {
			List<InputAction> activeActions = null;
			foreach (var ia in actionMap.actions) {
				if (ia.enabled) {
					if (activeActions == null) { activeActions = new List<InputAction>(); }
					activeActions.Add(ia);
				}
			}
			return activeActions;
		}

		private static InputAction CreateInputActionBinding(InputActionAsset asset, string name, string controlType,
		IEnumerable<string> bindPaths) {
			//Debug.Log("MAKE IT");
			int mapNameLimit = name.IndexOf("/");
			string actionMapName = name.Substring(0, mapNameLimit);
			string actionName = name.Substring(mapNameLimit + 1);
			InputActionMap actionMap = null;
			foreach (InputActionMap iam in asset.actionMaps) { if (iam.name == actionMapName) { actionMap = iam; break; } }
			if (actionMap == null) {
				//Debug.Log("creating action map "+ actionMapName);
				actionMap = asset.AddActionMap(actionMapName);
			}
			InputAction inputAct = null;
			foreach (InputAction ia in actionMap.actions) { if (ia.name == actionName) { inputAct = ia; } }
			if (inputAct == null) {
				bool isEnabled = actionMap.enabled;
				List<InputAction> activeActions = GetActiveActions(actionMap);
				if (isEnabled || activeActions != null) {
					//Debug.Log(actionMap.name + " was enabled, disabling");
					actionMap.Disable();
				}
				asset.Disable();
				inputAct = actionMap.AddAction(actionName);
				//inputAct = actionMap.AddAction(actionName, InputActionType.PassThrough);
				asset.Enable();
				//Debug.Log("added " + actionName);
				inputAct.expectedControlType = controlType;
				if (isEnabled) {
					if (activeActions == null) {
						//Debug.Log("reenabling " + actionMap.name);
						actionMap.Enable();
					} else {
						for(int i = 0; i < activeActions.Count; ++i) {
							activeActions[i].Enable();
						}
					}
				}
			}
			PopulateInputActionControlBinding(actionMap, inputAct, bindPaths);
			return inputAct;
		}

		private static void PopulateInputActionControlBinding(InputActionMap actionMap, InputAction action, IEnumerable<string> inputPathToCreateWithIfMissing) {
			foreach (string bindingString in inputPathToCreateWithIfMissing) {
				if (bindingString.StartsWith(CompositePrefix)) {
					//Debug.Log("composite logic "+ bindingString);
					string text = bindingString.Substring(CompositePrefix.Length);
					int index = text.IndexOf(":");
					string compositeName = text.Substring(0, index);
					//Debug.Log("compositeName "+ compositeName);
					text = text.Substring(compositeName.Length + 1);
					//Debug.Log("text " + text);
					string[] components = text.Split(InputBinding.Separator);
					InputActionSetupExtensions.CompositeSyntax compSyntax = CompositeBindingSyntax(actionMap, action, compositeName);
					for (int c = 0; c < components.Length; ++c) {
						text = components[c];
						//Debug.Log("text " + text);
						index = text.IndexOf(":");
						string subname = text.Substring(0, index);
						//Debug.Log("subname " + subname);
						text = text.Substring(index + 1);
						do {
							index = text.IndexOf(',');
							string subBindString;
							if (index >= 0) {
								subBindString = text.Substring(0, index);
								text = text.Substring(index + 1);
							} else {
								subBindString = text;
							}
							//Debug.Log("subBindString " + subBindString);
							compSyntax.With(subname, subBindString);
						} while (index >= 0);
					}
				} else {
					action.AddBinding(bindingString);
				}
			}
		}

		/// <summary>
		/// uses evil voodoo magic to access <see cref="InputActionSetupExtensions.AddBindingInternal"/> and the
		/// internal <see cref="InputActionSetupExtensions.CompositeSyntax"/> constructor. Can't seem to create
		/// composite inputs otherwise, at least in InputSystem v1.2.0 or before.
		/// </summary>
		private static InputActionSetupExtensions.CompositeSyntax CompositeBindingSyntax(InputActionMap actionMap, InputAction action, string compositeName) {
			var binding = new InputBinding {
				name = compositeName, path = "Dpad",
				interactions = null, processors = null,
				isComposite = true, action = action.name
			};
			// need a private method to add bindings to a composite input
			MethodInfo dynMethod = typeof(InputActionSetupExtensions).GetMethod("AddBindingInternal", BindingFlags.NonPublic | BindingFlags.Static);
			object result = null;
			try {
				// v1.0.2 uses 2 parameters
				result = dynMethod.Invoke(null, new object[] { actionMap, binding });
			} catch (Exception) { }
			if (result == null) {
				// v1.2.0 uses 3 parameters, the third one being ignored if it's -1
				result = dynMethod.Invoke(null, new object[] { actionMap, binding, -1 });
			}
			int bindingIndex = (int)result;
			InputActionSetupExtensions.CompositeSyntax compSyntax =
				(InputActionSetupExtensions.CompositeSyntax)typeof(InputActionSetupExtensions.CompositeSyntax).GetConstructor(
					  BindingFlags.NonPublic | BindingFlags.Instance,
					  null, new Type[] { typeof(InputActionMap), typeof(InputAction), typeof(int) }, null)
				.Invoke(new object[] { actionMap, action, bindingIndex });
			return compSyntax;
		}

		/// <summary>
		/// get names of actions in this <see cref="InputActionAsset"/>
		/// </summary>
		public static IList<string> GetAllActionNames(InputActionAsset actionAsset) {
			List<string> actionNames = new List<string>();
			foreach (var actionMap in actionAsset.actionMaps) {
				foreach (var action in actionMap.actions) {
					string actionName = actionMap.name + separator + action.name;
					actionNames.Add(actionName);
				}
			}
			return actionNames;
		}
	}
}
