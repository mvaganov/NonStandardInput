using UnityEngine;
using UnityEngine.InputSystem.OnScreen;

namespace NonStandard.Inputs {
	public class OnScreenStickAnimated : OnScreenStick {
		RectTransform rt;
		public float stickAnimationSpeed = 1024;
		Vector2 targetPosition;
		private void Start() {
			rt = GetComponent<RectTransform>();
		}
		public void SetStickPositionFrominput(Vector2 input) {
			targetPosition = input * movementRange;
		}
		private void Update() {
			Vector2 d = targetPosition - rt.anchoredPosition;
			if (d.SqrMagnitude() > 1) {
				rt.anchoredPosition += d.normalized * stickAnimationSpeed * Time.deltaTime;
			} else {
				rt.anchoredPosition = targetPosition;
			}
		}
	}
}