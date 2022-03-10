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
		public const string KbPrefix = "<Keyboard>/";

		/// <summary>
		/// used to help the system determine which modifying keys are currently pressed
		/// </summary>
		private static bool s_ctrlIsDown = false, s_altIsDown = false, s_shiftIsDown = false;
		/// <summary>
		/// keeps track of which keys are currently pressed, and when they were pressed.
		/// </summary>
		protected Dictionary<KeyControl, int> _keysDown = new Dictionary<KeyControl, int>();
		/// <summary>
		/// very short-term key storage, processed and added to the <see cref="UnityConsole.Console"/> each update
		/// </summary>
		protected StringBuilder _keyBuffer = new StringBuilder();
		/// <summary>
		/// fast map for inserting characters based on key pressed
		/// </summary>
		private Dictionary<KeyControl, KMap> _normalKeyMap = new Dictionary<KeyControl, KMap>();
		[Tooltip("each of these will be added as a key bind for " + nameof(KeyInput))]
		public KMap[] _implicitKeymap;
		[SerializeField] protected string _keyMapNameNormal = "keyboard";
		[SerializeField] protected string _keyMapNameShift = "shift";
		[SerializeField] protected string _keyMapNameCtrl = "ctrl";
		[SerializeField] protected string _keyMapNameAlt = "alt";

		public bool KeyAvailable => _keysDown.Count > 0;

		/// <summary>
		/// data structure used to map characters that result from a key press
		/// </summary>
		[System.Serializable]
		public struct KMap {
			[InputControl] public string key;
			public char press;
			public char shift;
			public KMap(string key, char press, char shift) {
				this.key = key; this.press = press; this.shift = shift;
			}
			public static implicit operator KMap(string s) {
				int i = s.Length - 2;
				return new KMap(s.Substring(0, i), s[i], s[i + 1]);
			}
		}
#if UNITY_EDITOR
		private void GenerateImplicitKeyMap() {
			_implicitKeymap = new KMap[] {
				"backquote`~",
				"11!", "22@", "33#", "44$", "55%", "66^", "77&", "88*", "99(", "00)",
				"minus-_", "equals=+", "backspace\b\b", "tab\t\t",
				"qqQ", "wwW", "eeE", "rrR", "ttT", "yyY", "uuU", "iiI", "ooO", "ppP",
				"leftBracket[{", "rightBracket]}", "backslash\\|",
				"aaA", "ssS", "ddD", "ffF", "ggG", "hhH", "jjJ", "kkK", "llL",
				"semicolon;:", "quote\'\"", "enter\n\n",
				"zzZ", "xxX", "ccC", "vvV", "bbB", "nnN", "mmM",
				"comma,<", "period.>", "slash/?", "space  ",
			};
			for (int i = 0; i < _implicitKeymap.Length; ++i) {
				KMap kbp = _implicitKeymap[i];
				if (!kbp.key.StartsWith(KbPrefix)) {
					kbp.key = KbPrefix + kbp.key;
					_implicitKeymap[i] = kbp;
				}
			}
		}
#endif
		public static bool IsShiftDown() { return s_shiftIsDown; }
		public static bool IsControlDown() { return s_ctrlIsDown; }
		public static bool IsAltDown() { return s_altIsDown; }
		public void ModifierAltHandler(InputAction.CallbackContext ctx) {
			SpecialModifierHandler(ctx, _keyMapNameCtrl, ref s_ctrlIsDown);
		}
		public void SpecialAltHandler(InputAction.CallbackContext ctx) {
			SpecialModifierHandler(ctx, _keyMapNameAlt, ref s_altIsDown);
		}
		public void ModifierShiftHandler(InputAction.CallbackContext ctx) {
			SpecialModifierHandler(ctx, _keyMapNameShift, ref s_shiftIsDown);
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

		protected virtual void Reset() {
			GenerateImplicitKeyMap();
			UserInput uinput = GetComponent<UserInput>();
			// bind implicit keys
			string[] keyboardInputs = Array.ConvertAll(_implicitKeymap, kp => kp.key);
			uinput.AddBindingIfMissing(new InputControlBinding("command line standard key input", _keyMapNameNormal + "/KeyInput",
				ControlType.Button, new EventBind(this, nameof(KeyInput)), keyboardInputs));
			// bind modified key states
			uinput.AddBindingIfMissing(new InputControlBinding("command line ctrl", _keyMapNameNormal + "/" + _keyMapNameCtrl,
				ControlType.Button, new EventBind(this, nameof(ModifierAltHandler)), KeyboardInput.Path(
					new string[] { "ctrl", "leftCtrl", "rightCtrl" })));
			uinput.AddBindingIfMissing(new InputControlBinding("command line alt", _keyMapNameNormal + "/" + _keyMapNameAlt,
				ControlType.Button, new EventBind(this, nameof(ModifierAltHandler)), KeyboardInput.Path(
					new string[] { "alt", "leftAlt", "rightAlt" })));
			uinput.AddBindingIfMissing(new InputControlBinding("command line shift", _keyMapNameNormal + "/" + _keyMapNameShift,
				ControlType.Button, new EventBind(this, nameof(ModifierShiftHandler)), KeyboardInput.Path(
					new string[] { "shift", "leftShift", "rightShift" })));
			// make sure the standard 'CmdLine' keys are bound to start with
			uinput.AddActionMapToBind(_keyMapNameNormal);
		}

		protected virtual void Awake() {
			// populate the fast standard keypress
			for (int i = 0; i < _implicitKeymap.Length; ++i) {
				InputControl ic = InputSystem.FindControl(_implicitKeymap[i].key);
				if (ic is KeyControl kc) {
					_normalKeyMap[kc] = _implicitKeymap[i];
				}
			}
		}

		public string Flush() {
			string txt = _keyBuffer.ToString();
			_keyBuffer.Clear();
			return txt;
		}

		public void KeyInput(InputAction.CallbackContext context) {
			//if (!_keyInputNormalAvailable) return;
			switch (context.phase) {
				// performed happens for each key, started only happens when the first keypress in a sequence happens
				case InputActionPhase.Performed: KeyDown(context.control as KeyControl); return;
				case InputActionPhase.Canceled: KeyUp(context.control as KeyControl); return;
			}
		}

		protected void KeyDown(KeyControl kc) {
			if (!enabled) {
				Debug.Log("ignoring " + kc.name + ", ConsoleInput is disabled.");
				return;
			}
			_keysDown[kc] = Environment.TickCount;
			bool isShift = IsShiftDown(), isCtrl = IsControlDown(), isNormal = !isShift && !isCtrl;
			if ((isShift || isNormal) && _normalKeyMap.TryGetValue(kc, out KMap normalKeyboardKey)) {
				_keyBuffer.Append(isNormal ? normalKeyboardKey.press : normalKeyboardKey.shift);
			}
		}

		protected void KeyUp(KeyControl kc) {
			if (!enabled) { return; }
			_keysDown.Remove(kc);
		}
		/// <summary>
		/// turn a simple key name into a fully qualified input path
		/// </summary>
		public static string Path(string key) {
			if (!key.StartsWith(KbPrefix)) { return KbPrefix + key; }
			return key;
		}
		/// <summary>
		/// turn a list of simple key names into a list of fully qualified input paths
		/// </summary>
		public static string[] Path(string[] keys) {
			string[] p = new string[keys.Length];
			for (int i = 0; i < keys.Length; ++i) {
				p[i] = (!keys[i].StartsWith(KbPrefix)) ? KbPrefix + keys[i] : keys[i];
			}
			return p;
		}
	}
}
