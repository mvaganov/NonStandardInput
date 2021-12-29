using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class OnPressEvents : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    public bool keepPressingWhileHeld;
    public bool _enableOnPress = true, _enableOnRelease = true;
    bool pressed = false;
    public UnityEvent OnPress, OnRelease;
    public bool EnableOnPress { get { return _enableOnPress; } set { _enableOnPress = value; } }
    public bool EnableOnRelease { get { return _enableOnRelease; } set { _enableOnRelease = value; } }
    public void OnPointerUp(PointerEventData eventData) {
        pressed = false;
        if (!_enableOnPress) return;
        OnRelease.Invoke();
    }
    public void OnPointerDown(PointerEventData eventData) {
        pressed = true;
        if (!_enableOnRelease) return;
        OnPress.Invoke();
    }
    private void OnDestroy() {
        pressed = false;
    }
    private void Update() {
        if (_enableOnPress && keepPressingWhileHeld && pressed) {
            OnPress.Invoke();
        }
    }
}
