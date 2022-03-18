// code by michael vaganov, released to the public domain via the unlicense (https://unlicense.org/)
using NonStandard;
using NonStandard.Inputs;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;

namespace Nonstandard.Inputs {
	[RequireComponent(typeof(UserInput))]
	public class KeyboardInput : MonoBehaviour {
		public const string KeyboardInputPrefix = "<Keyboard>/";
		/// <summary>
		/// used to help the system determine which modifying keys are currently pressed
		/// </summary>
		private static bool s_ctrlIsDown = false, s_altIsDown = false, s_shiftIsDown = false;
		/// <summary>
		/// keeps track of which keys are currently pressed, and when they were pressed.
		/// </summary>
		protected Dictionary<KeyControl, int> KeyDownTime = new Dictionary<KeyControl, int>();
		/// <summary>
		/// very short-term key storage, processed and added to the <see cref="UnityConsole.Console"/> each update
		/// </summary>
		protected StringBuilder KeyBuffer = new StringBuilder();
		/// <summary>
		/// fast map for inserting characters based on key pressed
		/// </summary>
		private Dictionary<KeyControl, KeyboardInputMap> _keyInputMap = new Dictionary<KeyControl, KeyboardInputMap>();
		[SerializeField] protected KeyMapNames keyMapNames = new KeyMapNames();
		public List<KeyboardInputMap> KeyInput;

		public string KeyMapName => keyMapNames.normal;
		public bool KeyAvailable => KeyDownTime.Count > 0;
		public static bool IsShiftDown => s_shiftIsDown;
		public static bool IsControlDown => s_ctrlIsDown;
		public static bool IsAltDown => s_altIsDown;

		[Serializable] public class KeyMapNames {
			public string normal = "keyboard";
			public string shift = "shift";
			public string ctrl = "ctrl";
			public string alt = "alt";
		}
		
		/// <summary>
		/// data structure used to map characters that result from a key press
		/// </summary>
		[System.Serializable] public struct KeyboardInputMap {
			[InputControl] public string key;
			public char press;
			public char shift;
			public KeyboardInputMap(string key, char press, char shift) {
				this.key = key; this.press = press; this.shift = shift;
			}
			public static implicit operator KeyboardInputMap(string s) {
				int endOfName = s.Length - 2;
				bool hasExplicitName = endOfName > 0;
				string name = s.Substring(0, hasExplicitName ? endOfName : 1);
				char press = s[endOfName], shift = s[endOfName + 1];
				return new KeyboardInputMap(name, press, shift);
			}
		}
#if UNITY_EDITOR
		protected virtual void Reset() {
			GenerateImplicitKeyMap();
			UserInput uinput = GetComponent<UserInput>();
			// bind implicit keys
			List<string> keyboardInputs = KeyInput.ConvertAll(kp => kp.key);
			string normal = keyMapNames.normal;
			uinput.AddBindingIfMissing(new InputControlBinding("console key input", normal + "/KeyInput",
				ControlType.Button, new EventBind(this, nameof(KeyInputHandler)), keyboardInputs));
			// bind modified key states
			uinput.AddBindingIfMissing(new InputControlBinding("console ctrl", normal + "/" + keyMapNames.ctrl,
				ControlType.Button, new EventBind(this, nameof(ModifierCtrlHandler)),
				KeyboardInput.Path(new string[] { "ctrl", "leftCtrl", "rightCtrl" })));
			uinput.AddBindingIfMissing(new InputControlBinding("console alt", normal + "/" + keyMapNames.alt,
				ControlType.Button, new EventBind(this, nameof(ModifierAltHandler)),
				KeyboardInput.Path(new string[] { "alt", "leftAlt", "rightAlt" })));
			uinput.AddBindingIfMissing(new InputControlBinding("console shift", normal + "/" + keyMapNames.shift,
				ControlType.Button, new EventBind(this, nameof(ModifierShiftHandler)),
				KeyboardInput.Path(new string[] { "shift", "leftShift", "rightShift" })));
			// make sure the standard 'CmdLine' keys are bound to start with
			uinput.AddDefaultActionMapToBind(normal);
		}
		private void GenerateImplicitKeyMap() {
			const string B = "Bracket", l = "left", r = "right";
			KeyInput = new List<KeyboardInputMap>() {
"backquote`~", "1!", "2@", "3#", "4$", "5%", "6^", "7&", "8*", "9(", "0)", "minus-_", "equals=+", "backspace\b\b",
"tab\t\t", "qQ", "wW", "eE", "rR", "tT", "yY", "uU", "iI", "oO", "pP", l + B + "[{", r + B + "]}", "backslash\\|",
"aA", "sS", "dD", "fF", "gG", "hH", "jJ", "kK", "lL", "semicolon;:", "quote\'\"", "enter\n\n",
"zZ", "xX", "cC", "vV", "bB", "nN", "mM", "comma,<", "period.>", "slash/?", "space  ",
			};
			for (int i = 0; i < KeyInput.Count; ++i) {
				KeyboardInputMap kmap = KeyInput[i];
				if (!kmap.key.StartsWith(KeyboardInputPrefix)) {
					kmap.key = KeyboardInputPrefix + kmap.key;
					KeyInput[i] = kmap;
				}
			}
		}
#endif
		protected virtual void Awake() {
			RefreshAllKeyMaps();
		}

		private void RefreshAllKeyMaps() {
			KeyInput.ForEach(SetKeyMap);
		}

		private void SetKeyMap(KeyboardInputMap kmap) {
			InputControl ic = InputSystem.FindControl(kmap.key);
			if (ic is KeyControl kc) {
				_keyInputMap[kc] = kmap;
			}
		}

		protected virtual void OnEnable() {
			EnableKeyMapProcessing(KeyMapName, true);
		}

		protected virtual void OnDisable() {
			EnableKeyMapProcessing(KeyMapName, false);
		}

		public void EnableKeyMapProcessing(string keyMapName, bool enable) {
			GetComponent<UserInput>().EnableActionMap(keyMapName, enable);
		}

		public KeyboardInputMap GetKeyMapByPress(char lowercase) {
			KeyboardInputMap result = new KeyboardInputMap();
			int index = KeyInput.FindIndex(map => map.press == lowercase);
			if (index >= 0) {
				result = KeyInput[index];
			}
			return result;
		}

		public void DisableKeyMapByPress(char lowercase) {
			KeyboardInputMap result = new KeyboardInputMap();
			int index = KeyInput.FindIndex(map => map.press == lowercase);
			if (index >= 0) {
				result = KeyInput[index];
				result.press = '\0';
				KeyInput[index] = result;
				SetKeyMap(result);
			}
		}

		public string Flush() {
			string txt = KeyBuffer.ToString();
			KeyBuffer.Clear();
			return txt;
		}

		public void ModifierCtrlHandler(InputAction.CallbackContext ctx) {
			SpecialModifierHandler(ctx, keyMapNames.ctrl, ref s_ctrlIsDown);
		}

		public void ModifierAltHandler(InputAction.CallbackContext ctx) {
			SpecialModifierHandler(ctx, keyMapNames.alt, ref s_altIsDown);
		}

		public void ModifierShiftHandler(InputAction.CallbackContext ctx) {
			SpecialModifierHandler(ctx, keyMapNames.shift, ref s_shiftIsDown);
		}

		private void SpecialModifierHandler(InputAction.CallbackContext ctx, string mapName, ref bool state) {
			UserInput uinput = GetComponent<UserInput>();
			bool oldState = state;
			switch (ctx.phase) {
				case InputActionPhase.Performed: state = true; break;
				case InputActionPhase.Canceled: state = false; break;
			}
			if (oldState != state) {
				if (state) {
					uinput.EnableActionMap(mapName);
				} else {
					uinput.DisableActionMap(mapName);
				}
				//Debug.Log(mapName+" " + state + " via " + ctx.control.path + " " + ctx.phase);
			}
		}

		public void KeyInputHandler(InputAction.CallbackContext context) {
			switch (context.phase) {
				// performed happens for each key, started only happens when the first keypress in a sequence happens
				case InputActionPhase.Performed: KeyDown(context.control as KeyControl); return;
				case InputActionPhase.Canceled: KeyUp(context.control as KeyControl); return;
			}
		}

		protected void KeyDown(KeyControl kc) {
			if (!enabled) {
				Debug.Log("ignoring " + kc.name + ", ConsoleInput is disabled. " +
					"Should not be seen because InputActions should be disabled when ConsoleInput is disabled.");
				return;
			}
			KeyDownTime[kc] = Environment.TickCount;
			bool isShift = IsShiftDown, isCtrl = IsControlDown, isNormal = !isShift && !isCtrl;
			if ((isShift || isNormal) && _keyInputMap.TryGetValue(kc, out KeyboardInputMap normalKeyboardKey)) {
				char value = isNormal ? normalKeyboardKey.press : normalKeyboardKey.shift;
				if (value != '\0') {
					KeyBuffer.Append(value);
				}
			}
		}

		protected void KeyUp(KeyControl kc) {
			if (!enabled) { return; }
			KeyDownTime.Remove(kc);
		}

		/// <summary>
		/// turn a simple key name into a fully qualified input path
		/// </summary>
		public static string Path(string key) {
			if (!key.StartsWith(KeyboardInputPrefix)) { return KeyboardInputPrefix + key; }
			return key;
		}

		/// <summary>
		/// turn a list of simple key names into a list of fully qualified input paths
		/// </summary>
		public static string[] Path(string[] keys) {
			string[] p = new string[keys.Length];
			for (int i = 0; i < keys.Length; ++i) {
				p[i] = (!keys[i].StartsWith(KeyboardInputPrefix)) ? KeyboardInputPrefix + keys[i] : keys[i];
			}
			return p;
		}
	}
}
