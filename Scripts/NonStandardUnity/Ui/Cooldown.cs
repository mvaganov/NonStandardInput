using UnityEngine;
using UnityEngine.Events;

namespace NonStandard.Ui {
	public class Cooldown : MonoBehaviour {
		public float cooldown = 3;
		private float timer = 0;
		public bool startCooldownOnStart = true;
		public UnityEvent_float onProgressChange;
		public UnityEvent OnStartCooldown, OnEndCooldown;
		[System.Serializable] public class UnityEvent_float : UnityEvent<float> { }
		public void StartCooldown() {
			timer = 0;
			OnStartCooldown.Invoke();
		}
		private void Start() {
			if (startCooldownOnStart) { StartCooldown(); }
		}
		void Update() {
			if (timer >= cooldown) { return; }
			timer += Time.deltaTime;
			if (timer >= cooldown) {
				timer = cooldown;
				OnEndCooldown.Invoke();
			}
			onProgressChange.Invoke(timer / cooldown);
		}
	}
}
