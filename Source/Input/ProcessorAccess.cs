using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using VRage;

namespace Arstraea.KoreanPatch.Input
{
    // 비공개 엔진 계약은 설치 때 한 번 검사하고 기존 큐를 통해 입력창의 소유 스레드로 전달한다.
    //
    // Validate the private engine contract once; use its queue to reach the owning UI thread.
    internal sealed class ProcessorAccess
    {
        private readonly FieldInfo active, composing, previous, written, limit, activeScreen, enabled, guiControl;
        private readonly MethodInfo queue, activate, deactivate;
        private readonly ConstructorInfo queuedCall;
        private sealed class Ownership { public volatile bool Korean; public long Enqueued, Applied; }
        private readonly ConditionalWeakTable<object, Ownership> owners = new ConditionalWeakTable<object, Ownership>();
        private sealed class CaretAccessor { public PropertyInfo Property; }
        private static readonly ConditionalWeakTable<Type, CaretAccessor> carets = new ConditionalWeakTable<Type, CaretAccessor>();

        // 문자열 내용을 읽지 않고 실제 편집 위치만 UI 소유 스레드에서 관찰한다.
        //
        // Observe the edit position on its UI owner without reading text content.
        private static int CaretIndex(IMyImeActiveControl target)
        {
            PropertyInfo property = carets.GetValue(target.GetType(), type => new CaretAccessor
            {
                Property = type.GetProperty("CarriagePositionIndex", BindingFlags.Instance | BindingFlags.Public)
            }).Property;
            return property != null && property.PropertyType == typeof(int) ? (int)property.GetValue(target) : -1;
        }

        public ProcessorAccess(Type type)
        {
            active = RequireField(type, "m_activeTextElement", null);
            composing = RequireField(type, "m_isComposing", typeof(bool));
            previous = RequireField(type, "m_compositionString", typeof(string));
            written = RequireField(type, "m_charsWritten", typeof(int));
            limit = RequireField(type, "m_textLimit", typeof(int));
            activeScreen = RequireField(type, "m_activeScreen", typeof(IVRageGuiScreen));
            enabled = RequireField(type, "m_isEnabled", typeof(bool));
            guiControl = RequireField(type, "m_guiControlElement", null);
            activate = RequireMethod(type, "Activate", typeof(void), typeof(IMyImeActiveControl));
            deactivate = RequireMethod(type, "Deactivate", typeof(void));
            Type callType = type.GetNestedType("MyDel", BindingFlags.Public | BindingFlags.NonPublic);
            if (callType == null) throw new MissingMemberException(type.FullName, "MyDel");
            queuedCall = callType.GetConstructor(new[] { typeof(IMyImeActiveControl), typeof(Action<IMyImeActiveControl>) });
            if (queuedCall == null) throw new MissingMethodException(callType.FullName, ".ctor");
            queue = RequireMethod(type, "QueueInvoke", typeof(void), callType);
        }

        public bool IsConnected(object processor) { return active.GetValue(processor) != null; }
        internal InputContext Context(object processor, IMyImeActiveControl target)
        { return new InputContext(target, activeScreen.GetValue(processor) as IVRageGuiScreen); }
        internal bool PrepareActivation(object processor, IMyImeActiveControl target)
        {
            if (target == null || !(bool)enabled.GetValue(processor)) return true;
            InputContext context = Context(processor, target);
            if (context.Script && PluginSettings.Current.VanillaScriptEditor)
            {
                // 이전 조합은 기존 엔진의 종료 절차를 거친 뒤 코드 편집의 buffered 입력으로 돌린다.
                // Finish the old input through engine teardown before restoring buffered code input.
                deactivate.Invoke(processor, null);
                target.IsImeActive = false;
                ImeControlPolicy.EnterVanilla(guiControl.GetValue(processor) as System.Windows.Forms.Control, context);
                return false;
            }
            ImeControlPolicy.Prepare(context);
            return true;
        }

        // 화면 생성 중 검색칸이 먼저 선택된 뒤 RegisterActiveScreen이 이전 화면을
        // 해제하면 새 입력 대상도 지워진다. 등록이 끝난 같은 화면의 선택만 복원한다.
        // 활성 대상/조합이 있으면 건드리지 않아 매 등록마다 IME를 재초기화하지 않는다.
        //
        // Initial textbox focus can precede screen registration; unregistering the old
        // screen then clears that new input target. Restore only the registered screen's
        // focused editor, without reinitializing an already connected or composing target.
        internal bool RestoreScreenInput(object processor, IVRageGuiScreen screen)
        {
            if (screen == null || !screen.IsOpened || !(bool)enabled.GetValue(processor)
                || !ReferenceEquals(activeScreen.GetValue(processor), screen)
                || IsConnected(processor) || (bool)composing.GetValue(processor)) return false;
            var target = screen.FocusedControl as IMyImeActiveControl;
            if (target == null) return false;
            if (Context(processor, target).Script && PluginSettings.Current.VanillaScriptEditor) return false;
            activate.Invoke(processor, new object[] { target });
            if (ImeDiagnostics.WantsHealth)
                ImeDiagnostics.Record("screen-focus-restored", health: true, metadata: Snapshot(processor));
            return true;
        }
        internal string Snapshot(object processor)
        {
            return "target=" + ImeHealthProbe.Id(active.GetValue(processor))
                + " composing=" + composing.GetValue(processor) + " owned=" + OwnsComposition(processor)
                + " previous=" + (((string)previous.GetValue(processor)) ?? "").Length
                + " written=" + written.GetValue(processor) + " limit=" + limit.GetValue(processor);
        }
        public bool OwnsComposition(object processor)
        {
            Ownership owner;
            return owners.TryGetValue(processor, out owner) && owner.Korean;
        }

        public void Begin(object processor, long origin = 0)
        {
            Enqueue(processor, target =>
            {
                if (ImeDiagnostics.WantsDetail) ImeDiagnostics.Record("ui-explicit-start", origin: origin, metadata: Snapshot(processor));
                BeginOnUi(processor, target);
            }, origin, "start");
        }

        public void End(object processor, long origin = 0)
        {
            Enqueue(processor, target =>
            {
                string provisional = (string)previous.GetValue(processor) ?? "";
                bool trace = ImeDiagnostics.WantsDetail;
                int beforeCaret = trace ? CaretIndex(target) : -1;
                if (provisional.Length > 0) target.KeypressBackspaceMultiple(false, provisional.Length);
                previous.SetValue(processor, "");
                written.SetValue(processor, 0);
                limit.SetValue(processor, 0);
                composing.SetValue(processor, false);
                target.DeactivateIme();
                owners.GetOrCreateValue(processor).Korean = false;
                if (trace) ImeDiagnostics.Record("ui-end", removed: provisional.Length, origin: origin, metadata: Snapshot(processor)
                    + " caretBefore=" + beforeCaret + " caretAfter=" + CaretIndex(target));
            }, origin, "end");
        }

        public void Apply(object processor, bool hasResult, string result, bool hasComposition, string composition, long origin = 0)
        {
            Enqueue(processor, target =>
            {
                bool trace = ImeDiagnostics.WantsDetail;
                string oldComposition = trace ? (string)previous.GetValue(processor) ?? "" : "";
                bool wasComposing = trace && (bool)composing.GetValue(processor);
                BeginOnUi(processor, target);
                CompositionPlan plan = CompositionPlan.Create((string)previous.GetValue(processor),
                    (int)written.GetValue(processor), (int)limit.GetValue(processor),
                    hasResult, result, hasComposition, composition);
                int beforeLength = trace ? target.GetTextLength() : 0;
                int beforeCaret = trace ? CaretIndex(target) : -1;
                int selectionLength = trace ? target.GetSelectionLength() : 0;
                if (plan.RemoveCount > 0) target.KeypressBackspaceMultiple(false, plan.RemoveCount);
                if (plan.Commit.Length > 0) target.InsertCharMultiple(true, plan.Commit);
                if (plan.Composition.Length > 0) target.InsertCharMultiple(false, plan.Composition);
                previous.SetValue(processor, plan.Composition);
                written.SetValue(processor, plan.WrittenCount);
                if (trace) ImeDiagnostics.Record("ui-apply", resultLength: plan.Commit.Length, compositionLength: plan.Composition.Length,
                    removed: plan.RemoveCount, origin: origin, metadata: Snapshot(processor)
                    + " before=" + beforeLength + " after=" + target.GetTextLength() + " selection=" + selectionLength
                    + " caretBefore=" + beforeCaret + " caretAfter=" + CaretIndex(target)
                    + " hasResult=" + hasResult + " hasComposition=" + hasComposition + " wasComposing=" + wasComposing
                    + " previousShape=" + ImeDiagnostics.Shape(oldComposition)
                    + " resultMatchesPrevious=" + (hasResult && result == oldComposition)
                    + " resultShape=" + ImeDiagnostics.Shape(plan.Commit) + " compShape=" + ImeDiagnostics.Shape(plan.Composition));
            }, origin);
        }

        // 문자뿐 아니라 조합 상태도 같은 UI 큐에서 변경한다. 엔진의 원래 시작 함수는
        // Windows 스레드에서 후보 GUI를 건드리고, 길이가 찼을 때 IME를 껐다 켠다.
        // 재진입·빠른 음절 전환 때 이 경로가 다음 조합을 끊을 수 있어 호출하지 않는다.
        //
        // Serialize state and edits in the same UI queue. The native engine start path
        // touches candidate GUI off-thread and cycles IME when full; neither is safe
        // during reentrant or rapidly overlapping Korean composition messages.
        private void BeginOnUi(object processor, IMyImeActiveControl target)
        {
            owners.GetOrCreateValue(processor).Korean = true;
            if ((bool)composing.GetValue(processor)) return;
            previous.SetValue(processor, "");
            written.SetValue(processor, 0);
            limit.SetValue(processor, Math.Max(0, target.GetMaxLength() - target.GetTextLength() + target.GetSelectionLength()));
            composing.SetValue(processor, true);
            target.IsImeActive = true;
            ImeDiagnostics.Record("ui-start");
        }

        private void Enqueue(object processor, Action<IMyImeActiveControl> action, long origin = 0, string operation = "apply")
        {
            var target = active.GetValue(processor) as IMyImeActiveControl;
            if (target == null)
            {
                ImeDiagnostics.Record("drop-no-target", origin: origin);
                return;
            }
            Ownership owner = owners.GetOrCreateValue(processor);
            owner.Korean = true;
            long order = System.Threading.Interlocked.Increment(ref owner.Enqueued);
            if (ImeDiagnostics.WantsDetail) ImeDiagnostics.Record("ui-enqueue", origin: origin, metadata: "operation=" + operation
                + " order=" + order + " target=" + ImeHealthProbe.Id(target));
            long queuedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            Action<IMyImeActiveControl> guarded = captured =>
            {
                long delay = (System.Diagnostics.Stopwatch.GetTimestamp() - queuedAt) * 1000 / System.Diagnostics.Stopwatch.Frequency;
                if (delay >= 100) ImeDiagnostics.Record("ui-queue-delay", origin: origin, metadata: "delayMs=" + delay);
                // 입력창이 이미 바뀌었다면 오래된 이벤트로 새 조합 상태를 덮어쓰지 않는다.
                //
                // A stale event must not overwrite the composition state of a newly focused field.
                long last = owner.Applied;
                owner.Applied = order;
                if (ImeDiagnostics.WantsDetail) ImeDiagnostics.Record("ui-execute", origin: origin, metadata: "operation=" + operation
                    + " order=" + order + " previousOrder=" + last + " batch=" + ImeQueueTrace.CurrentBatch
                    + " delayMs=" + delay + " " + Snapshot(processor));
                if (ReferenceEquals(active.GetValue(processor), captured)) action(captured);
                else if (ImeDiagnostics.WantsDetail) ImeDiagnostics.Record("drop-stale-target", origin: origin, metadata: "captured="
                    + ImeHealthProbe.Id(captured) + " " + Snapshot(processor));
            };
            object call = queuedCall.Invoke(new object[] { target, guarded });
            queue.Invoke(processor, new[] { call });
        }

        private static FieldInfo RequireField(Type type, string name, Type fieldType)
        {
            FieldInfo field = AccessTools.Field(type, name);
            if (field == null || field.IsStatic || (fieldType != null && field.FieldType != fieldType))
                throw new MissingFieldException(type.FullName, name);
            return field;
        }

        internal static MethodInfo RequireMethod(Type type, string name, Type returnType, params Type[] parameters)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name, parameters);
            if (method == null || method.IsStatic || method.ReturnType != returnType)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }
    }
}
