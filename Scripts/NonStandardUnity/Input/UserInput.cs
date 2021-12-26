using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Reflection;

namespace NonStandard.Inputs {
    public class UserInput : MonoBehaviour {
        [Tooltip("The Character's Controls")]
        public InputActionAsset inputActionAsset;

        [SerializeField] private List<Binding> inputBindings;
        bool initialized = false;

        void Start() {
            Bind(inputBindings, true);
            inputActionAsset.FindActionMap("Player").Enable();
            initialized = true;
        }
        public Binding GetBinding(string name) { return inputBindings.Find(b => b.name == name); }
        public void AddBinding(Binding b, bool enable = true) {
            if (inputBindings == null) { inputBindings = new List<Binding>(); }
            inputBindings.Add(b);
            b.Bind(inputActionAsset, enable);
        }
        public void Bind(IList<Binding> inputs, bool enable) {
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

        public static InputActionAsset CreateAsset(string name = "FPS Character Controller", string path = "") {
#if UNITY_EDITOR
            string filename = path + "/" + name + "." + InputActionAsset.Extension;
            //ProjectWindowUtil.CreateAssetWithContent(filename, "{}");
            //InputActionAsset asset = Resources.Load(filename, typeof(InputActionAsset)) as InputActionAsset;
            InputActionAsset asset = ScriptableObjectUtility.CreateAsset(typeof(InputActionAsset), filename, "{}") as InputActionAsset;
            return asset;
#else
            return null;
#endif
        }
    }

    public enum ControlType { Button, Vector2, Vector3, Analog, Axis, Bone, Digital, Double, Dpad, Eyes, Integer, Quaternion, Stick, Touch }

    [Serializable]
    public class Binding {
        public string name;
        public ControlType type;
        public string[] controls = null;
        public EventBind evnt;
        public UnityInputActionEvent startPerformCancel = new UnityInputActionEvent();
        internal const char separator = '/';

        [Serializable] public class UnityInputActionEvent : UnityEvent<InputAction.CallbackContext> { }
        public Binding(string n, ControlType t, EventBind e, string[] c = null) {
            name = n; type = t; evnt = e; controls = c;
            e.Bind(startPerformCancel);
        }
        /// <summary>
        /// DEPRECATED. was used before PlayerInput was discovered to be buggy when dynamically allocated.
        /// </summary>
        /// <param name="playerInput"></param>
        /// <returns></returns>
        public bool BindAction(PlayerInput playerInput) {
            if (playerInput.actions != null) {
                //Debug.Log("!!!!! player assign " + name + " " + playerInput.actionEvents.Count);
                string n = Binding.separator + name;
                foreach (var e in playerInput.actionEvents) {
                    //Debug.Log("~~~~ " + e.actionName + " vs " + name);
                    if (e.actionName.Contains(n)) {
                        Debug.Log(name + " binding {" + evnt + "()} to " + e);
                        //evnt.Bind(e);
                        e.AddListener(startPerformCancel.Invoke);
                        return true;
                    }
                }
            }
            return false;
        }
        public void Bind(InputActionAsset inputActionAsset, bool enable) {
            InputAction ia = FindAction(inputActionAsset, name, type, controls);
            if (ia == null) {
                string allActions = string.Join(", ", string.Join(", ", Binding.GetAllActionNames(inputActionAsset)));
                Debug.LogWarning($"Missing {name} {type}). Did you mean one of these: [{allActions}]");
                return;
            }
            if (enable) {
                BindAction(ia);
            } else {
                UnbindAction(ia);
            }
        }
        public void BindAction(InputAction ia) {
            if (startPerformCancel != null) {
                ia.started -= startPerformCancel.Invoke;
                ia.performed -= startPerformCancel.Invoke;
                ia.canceled -= startPerformCancel.Invoke;

                ia.started += startPerformCancel.Invoke;
                ia.performed += startPerformCancel.Invoke;
                ia.canceled += startPerformCancel.Invoke;
            }
        }
        public void UnbindAction(InputAction ia) {
            if (startPerformCancel != null) {
                ia.started -= startPerformCancel.Invoke;
                ia.performed -= startPerformCancel.Invoke;
                ia.canceled -= startPerformCancel.Invoke;
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
            object result = dynMethod.Invoke(null, new object[] { actionMap, binding });
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
