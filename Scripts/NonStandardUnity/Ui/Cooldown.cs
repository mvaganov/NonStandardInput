using UnityEngine;
using UnityEngine.Events;

public class Cooldown : MonoBehaviour {
    public float cooldown = 3;
    public float timer = 0;
    public MonoBehaviour behavior;
    public UnityEvent_float onProgressChange;
    public UnityEvent OnStartCooldown, OnEndCooldown;
    public bool startCooldownOnStart = true;
    [System.Serializable] public class UnityEvent_float : UnityEvent<float> { }
    public void StartCooldown() {
        Debug.Log("Cooldown!");
        timer = 0;
        OnStartCooldown.Invoke();
    }
    private void Start() {
        if (startCooldownOnStart) { StartCooldown(); }
    }
    void Update() {
        if(timer >= cooldown) { return; }
        timer += Time.deltaTime;
        if (timer >= cooldown) {
            timer = cooldown;
            OnEndCooldown.Invoke();
        }
        onProgressChange.Invoke(timer / cooldown);
    }
}
