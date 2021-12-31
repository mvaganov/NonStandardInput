using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NonStandard.Ui {
	public class PressEvents : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
		public bool keepPressingWhileHeld = true;
		public bool releaseIfDestroyed = true;
		public bool _enableOnPress = true, _enableOnRelease = true;
		bool pressed = false;
		public UnityEvent OnPress, OnRelease;
		public bool EnableOnPress { get { return _enableOnPress; } set { _enableOnPress = value; } }
		public bool EnableOnRelease { get { return _enableOnRelease; } set { _enableOnRelease = value; } }
		public void InputAction(InputAction.CallbackContext context) {
			switch (context.phase) {
				case InputActionPhase.Started: Press(); break;
				case InputActionPhase.Canceled: Release(); break;
			}
		}
		public void Release() => OnPointerUp(null);
		public void OnPointerUp(PointerEventData eventData) {
			//Debug.Log("release");
			pressed = false;
			if (!_enableOnRelease) return;
			OnRelease.Invoke();
		}
		public void Press() => OnPointerDown(null);
		public void OnPointerDown(PointerEventData eventData) {
			//Debug.Log("press");
			pressed = true;
			if (!_enableOnPress) return;
			OnPress.Invoke();
		}
		public void Click() { Click(0); }
		public void Click(float secondsDelayBeforeRelease) {
			StartCoroutine(PressThenRelease(0, secondsDelayBeforeRelease));
		}
		IEnumerator PressThenRelease(float delayBeforePress = 0, float delayBeforeRelease = 0) {
			if (delayBeforePress > 0) { yield return new WaitForSeconds(delayBeforePress); }
			Press();
			if (delayBeforeRelease > 0) { yield return new WaitForSeconds(delayBeforeRelease); }
			Release();
		}
		private void OnDestroy() {
			if (releaseIfDestroyed) { Release(); }
		}
		private void Update() {
			if (_enableOnPress && keepPressingWhileHeld && pressed) {
				OnPress.Invoke();
			}
		}
	}
}
