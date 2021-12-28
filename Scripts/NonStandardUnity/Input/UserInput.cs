using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Reflection;
using System.Text;

namespace NonStandard.Inputs {
    public class UserInput : MonoBehaviour {
        [Tooltip("The Character's Controls")]
        public InputActionAsset inputActionAsset;

        [SerializeField] private List<Binding> inputBindings;
        bool initialized = false;

        public string[] actionMapToBindOnStart = new string[0];

        void Start() {
            Bind(inputBindings, true);
            Array.ForEach(actionMapToBindOnStart, actionMapName => inputActionAsset.FindActionMap(actionMapName).Enable());
            initialized = true;
        }
        public Binding GetBinding(string name) { return inputBindings.Find(b => b.actionName == name); }
        public void AddBinding(Binding b, bool enable = true) {
            if (inputBindings == null) { inputBindings = new List<Binding>(); }
            inputBindings.Add(b);
            b.Bind(inputActionAsset, enable);
        }
        public void Bind(IList<Binding> inputs, bool enable) {
            if (inputActionAsset == null) {
                inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            }
            for (int i = 0; i < inputs.Count; ++i) {
                inputs[i].Bind(inputActionAsset, enable);
            }
        }
        private void OnEnable() {
            if (!initialized) return;
            Bind(inputBindings, true);
        }
        private void OnDisable() {
            if (!initialized) return;
            Bind(inputBindings, false);
        }
        public IList<string> GetAllActionNames() { return Binding.GetAllActionNames(inputActionAsset); }

        public static string GetInputDescription() {
            StringBuilder sb = new StringBuilder();
            List<InputAction> allEnabledActions = InputSystem.ListEnabledActions();
            Dictionary<InputActionMap, List<InputAction>> allEnabledActionsByMap = new Dictionary<InputActionMap, List<InputAction>>();
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
            foreach (var kvp in allEnabledActionsByMap) {
                sb.Append(kvp.Key.name).Append("\n");
                foreach(var action in kvp.Value) {
                    if (action == null) continue;
                    List<string> inputBindings = new List<string>();
                    for (int i = 0; i < action.bindings.Count; ++i) {
                        string bPath = action.bindings[i].path;
                        if (bPath.StartsWith("<Keyboard>") || bPath.StartsWith("<Mouse>")) {
                            inputBindings.Add(bPath);
                        }
                    }
                    if (inputBindings.Count == 0) continue;
                    Binding.Active.TryGetValue(action, out Binding binding);
                    string desc = binding != null ? " -- " + binding.description : "";
                    sb.Append("  ").Append(action.name).Append(" ").Append(desc).Append("\n    ");
                    sb.Append(string.Join("\n    ", inputBindings)).Append("\n");
                }
            }
            if (unmapped != null && unmapped.Count > 0) {
                sb.AppendLine("---").Append("\n");
                foreach (var action in unmapped) {
                    Binding binding = Binding.Active[action];
                    sb.Append("  ").Append(action.name).Append(" ").Append(binding.description).
                        Append("\n    ").Append(string.Join("\n    ", binding.bindingPaths)).Append("\n");
                }
            }
            return sb.ToString();
        }
    }

    public enum ControlType { Button, Vector2, Vector3, Analog, Axis, Bone, Digital, Double, Dpad, Eyes, Integer, Quaternion, Stick, Touch }

    [Serializable]
    public class Binding {
        public static Dictionary<InputAction, Binding> Active = new Dictionary<InputAction, Binding>();
        public static Action OnActiveChange;

        public string description, actionName;
        public ControlType controlType;
        public string[] bindingPaths = null;
        public EventBind evnt;
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
                return index >= 0 ? actionName.Substring(index+1) : actionName;
            }
        }
        public Binding(string d, string an, ControlType t, EventBind e, string[] c = null) {
            description = d; actionName = an; controlType = t; evnt = e; bindingPaths = c;
            e.Bind(actionEventHandler);
        }
        /// <summary>
        /// DEPRECATED. was used before PlayerInput was discovered to be buggy when dynamically allocated.
        /// </summary>
        /// <param name="playerInput"></param>
        /// <returns></returns>
        public bool BindAction(PlayerInput playerInput) {
            if (playerInput.actions != null) {
                //Debug.Log("!!!!! player assign " + name + " " + playerInput.actionEvents.Count);
                string n = Binding.separator + actionName;
                foreach (var e in playerInput.actionEvents) {
                    //Debug.Log("~~~~ " + e.actionName + " vs " + name);
                    if (e.actionName.Contains(n)) {
                        Debug.Log(actionName + " binding {" + evnt + "()} to " + e);
                        //evnt.Bind(e);
                        e.AddListener(actionEventHandler.Invoke);
                        return true;
                    }
                }
            }
            return false;
        }
        public void Bind(InputActionAsset inputActionAsset, bool enable) {
            InputAction ia = FindAction(inputActionAsset, actionName, controlType, bindingPaths);
            if (ia == null) {
                string allActions = string.Join(", ", string.Join(", ", Binding.GetAllActionNames(inputActionAsset)));
                Debug.LogWarning($"Missing {actionName} {controlType}). Did you mean one of these: [{allActions}]");
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

        public static InputAction FindAction(InputActionAsset actionAsset, string expectedActionName, ControlType actionInputType, string[] bindingPathToCreateWithIfMissing = null) {
            string controlType = actionInputType.ToString();
            foreach (var actionMap in actionAsset.actionMaps) {
                string n = separator + expectedActionName;
                foreach (var action in actionMap.actions) {
                    string actionName = actionMap.name + separator + action.name;
                    if (action.name == expectedActionName || actionName == expectedActionName || actionName.Contains(n)) {
                        if (action.expectedControlType != controlType) {
                            Debug.LogWarning("found " + expectedActionName + ", but Input type is " + action.expectedControlType + ", not " + actionInputType);
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
        private static InputAction CreateInputActionBinding(InputActionAsset asset, string name, string controlType, string[] bindPaths) {
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
                inputAct = actionMap.AddAction(actionName);
                inputAct.expectedControlType = controlType;
            }
            if (bindPaths.Length == 0) {
                //Debug.Log("no actual input for "+ name);
                return inputAct;
            }
            PopulateInputActionControlBinding(actionMap, inputAct, bindPaths);
            return inputAct;
        }
        public static string CompositePrefix = "^COMPOSITE^";
        private static void PopulateInputActionControlBinding(InputActionMap actionMap, InputAction action, string[] inputPathToCreateWithIfMissing) {
            for (int inputBindIndex = 0; inputBindIndex < inputPathToCreateWithIfMissing.Length; inputBindIndex++) {
                string bindingString = inputPathToCreateWithIfMissing[inputBindIndex];
                if (bindingString.StartsWith(CompositePrefix)) {
                    //Debug.Log("composite logic "+ bindingString);
                    string text = bindingString.Substring(CompositePrefix.Length);
                    int index = text.IndexOf(":");
                    string compositeName = text.Substring(0, index);
                    //Debug.Log("compositeName "+ compositeName);
                    text = text.Substring(compositeName.Length + 1);
                    //Debug.Log("text " + text);
                    string[] components = text.Split(InputBinding.Separator);
                    InputActionSetupExtensions.CompositeSyntax compSyntax = WsadBindingSyntax(actionMap, action, compositeName);
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
        /// uses voodoo magic to access <see cref="InputActionSetupExtensions.AddBindingInternal"/> and the internal
        /// <see cref="InputActionSetupExtensions.CompositeSyntax"/> constructor
        /// </summary>
        /// <param name="actionMap"></param>
        /// <param name="action"></param>
        /// <param name="compositeName"></param>
        /// <returns></returns>
        private static InputActionSetupExtensions.CompositeSyntax WsadBindingSyntax(InputActionMap actionMap, InputAction action, string compositeName) {
            var binding = new InputBinding {
                name = compositeName, path = "Dpad",
                interactions = null, processors = null,
                isComposite = true, action = action.name
            };
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
