using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Arstraea.KoreanPatch.Input
{
    internal enum InputLanguagePolicy { Previous, PerContext, EnglishExceptChat, AlwaysEnglish }

    // 네이티브 창과 게임 UI가 다른 스레드에 있으므로 작은 상태 변경만 잠금으로 보호한다.
    // 현재 화면/입력칸만 약한 참조로 유지하고 기억한 값은 문자열 키와 한/영 값뿐이다.
    //
    // Native and game UI threads share only a small locked state. Retain weak references
    // to the current screen/target; remembered contexts contain keys/modes, never GUI objects.
    internal sealed class InputLanguageMemory
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, ImeMode> remembered = new Dictionary<string, ImeMode>(StringComparer.Ordinal);
        private WeakReference lastTarget;
        private WeakReference lastScreen;
        private string lastKey;
        private bool lastChat;
        private ImeMode? shared, lastMode;
        private InputLanguagePolicy policy;
        internal volatile bool MemoryEnabled = true;

        internal void Configure(InputLanguagePolicy value)
        {
            lock (gate)
            {
                if (policy == value) return;
                policy = value;
                ResetCore();
            }
        }

        internal ImeMode Activate(object target, string key, bool chat, ImeMode current, object screen = null)
        {
            lock (gate)
            {
                // 화면 등록 보완이나 포커스 복구로 같은 입력칸을 영어로 되돌리지 않는다.
                // Screen registration repair/repeated activation must not reset a field.
                bool same = target != null && ReferenceEquals(lastTarget?.Target, target);
                same |= screen != null && ReferenceEquals(lastScreen?.Target, screen) && lastKey == key;
                ImeMode chosen;
                if (same) chosen = lastMode ?? current;
                else if (!MemoryEnabled) chosen = ImeMode.Alpha;
                // 기억이 없는 최초 진입은 영어다. 이전 Windows/월드의 IME 상태를 가져오지 않는다.
                // Start in English before any session memory; do not inherit Windows/previous-world mode.
                else if (policy == InputLanguagePolicy.Previous) chosen = shared ?? ImeMode.Alpha;
                else if (policy == InputLanguagePolicy.PerContext
                    || (policy == InputLanguagePolicy.EnglishExceptChat && chat))
                    chosen = remembered.TryGetValue(key, out var saved) ? saved : ImeMode.Alpha;
                else chosen = ImeMode.Alpha;
                lastTarget = target == null ? null : new WeakReference(target);
                lastScreen = screen == null ? null : new WeakReference(screen);
                lastKey = key;
                lastChat = chat;
                lastMode = chosen;
                return chosen;
            }
        }

        internal void Remember(ImeMode mode)
        {
            lock (gate)
            {
                shared = lastMode = mode;
                if (!MemoryEnabled) return;
                if (lastKey != null && (policy == InputLanguagePolicy.PerContext
                    || (policy == InputLanguagePolicy.EnglishExceptChat && lastChat)))
                {
                    // 커스텀 UI가 무한한 이름을 공급해도 세션 메모리가 계속 늘지 않게 한다.
                    // Bound session memory even if a custom UI generates arbitrary names.
                    if (remembered.Count >= 128 && !remembered.ContainsKey(lastKey)) remembered.Clear();
                    remembered[lastKey] = mode;
                }
            }
        }

        internal void EndTarget(object target)
        {
            lock (gate)
                if (ReferenceEquals(lastTarget?.Target, target)) { lastTarget = lastScreen = null; lastKey = null; lastMode = null; }
        }

        internal void Reset() { lock (gate) ResetCore(); }
        private void ResetCore()
        { remembered.Clear(); lastTarget = lastScreen = null; lastKey = null; shared = lastMode = null; lastChat = false; }
    }
}
