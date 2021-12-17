using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class InputForwarding_ButtonPressed : MonoBehaviour
{
    [System.Serializable] public class UnityEvent_bool : UnityEvent<bool> { }
    public UnityEvent_bool boolEvent;
    public void ForwardPressed(InputAction.CallbackContext context) {
        switch (context.phase) {
            case InputActionPhase.Started: boolEvent.Invoke(true); break;
            case InputActionPhase.Canceled: boolEvent.Invoke(false); break;
        }
    }
}
