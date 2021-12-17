using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class InputForwarding_Vector2 : MonoBehaviour {
    [System.Serializable] public class UnityEvent_Vector2 : UnityEvent<Vector2> { }
    public UnityEvent_Vector2 vector2Event;
    public void ForwardVector2(InputAction.CallbackContext context) {
        vector2Event.Invoke(context.ReadValue<Vector2>());
    }
}