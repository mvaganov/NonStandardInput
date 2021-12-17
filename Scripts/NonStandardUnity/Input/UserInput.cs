using NonStandard.Utility.UnityEditor;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if !ENABLE_INPUT_SYSTEM
public class CharacterInputAutomate : MonoBehaviour { }
#else
using UnityEngine.InputSystem;
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

    public static InputAction FindAction(InputActionAsset actionAsset, string actionName, string actionInputType) {
        foreach (var actionMap in actionAsset.actionMaps) {
            string n = "/" + actionName;
            foreach (var action in actionMap.actions) {
                if (action.name == actionName || action.name.Contains(n)) {
                    if (action.expectedControlType != actionInputType) {
                        Debug.Log("found " + actionName + ", but Input type is " + action.expectedControlType + ", not " + actionInputType);
                    } else {
                        return action;
                    }
                }
            }
            //foreach (var action in actionMap.actions) { Debug.Log(actionName+"???\n"+ actionMap.actions.JoinToString("\n",a=>a.name)); }
        }
        return null;
    }

    [Serializable]
    public class UnityInputActionEvent : UnityEvent<InputAction.CallbackContext> { }

    [Serializable]
    public class Binding {
        public string name, type;
        public EventBind evnt;
        public UnityInputActionEvent startPerformCancel = new UnityInputActionEvent();
        public Binding(string n, string t, EventBind e) {
            name = n; type = t; evnt = e;
            e.Bind(startPerformCancel);
        }
        public bool BindAction(PlayerInput playerInput) {
            if (playerInput.actions != null) {
                //Debug.Log("!!!!! player assign " + name + " " + playerInput.actionEvents.Count);
                string n = "/" + name;
                foreach (var e in playerInput.actionEvents) {
                    //Debug.Log("~~~~ " + e.actionName + " vs " + name);
                    if (e.actionName.Contains(n)) {
                        Debug.Log(name+" binding {" + evnt + "()} to " + e);
                        //evnt.Bind(e);
                        e.AddListener(startPerformCancel.Invoke);
                        return true;
                    }
                }
            }
            return false;
        }
        public void Bind(InputActionAsset inputActionAsset, bool enable) {
            InputAction ia = FindAction(inputActionAsset, name, type);
            if (ia == null) {
                Debug.Log("Missing " + name + " (" + type + ")");
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
                ia.started  -= startPerformCancel.Invoke;
                ia.performed-= startPerformCancel.Invoke;
                ia.canceled -= startPerformCancel.Invoke;

                ia.started  += startPerformCancel.Invoke;
                ia.performed+= startPerformCancel.Invoke;
                ia.canceled += startPerformCancel.Invoke;
            }
        }
        public void UnbindAction(InputAction ia) {
            if (startPerformCancel != null) {
                ia.started  -= startPerformCancel.Invoke;
                ia.performed-= startPerformCancel.Invoke;
                ia.canceled -= startPerformCancel.Invoke;
            }
        }
    }
}
#endif
