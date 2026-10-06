using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Arstraea.KoreanPatch
{
    internal sealed class PatchRollbackException : AggregateException
    {
        internal PatchRollbackException(IEnumerable<Exception> errors)
            : base("Startup patch rollback failed; restart before using game input.", errors) { }
    }
    // 시작 패치는 전부 사전 확인한 뒤 설치하고 실패하면 이번 묶음만 해제한다.
    // 같은 대상의 다른 플러그인과 이미 설치한 우리 묶음은 건드리지 않는다.
    //
    // Validate the whole startup group before installing. On failure remove only
    // this group, preserving foreign patches and previously installed groups.
    internal sealed class PatchTransaction
    {
        private sealed class Entry
        {
            internal MethodBase Target;
            internal HarmonyMethod Prefix, Postfix, Transpiler, Finalizer;
        }
        private readonly Harmony harmony;
        private readonly List<Entry> entries = new List<Entry>();
        internal PatchTransaction(string id) { harmony = new Harmony(id); }
        internal void Add(MethodBase target, HarmonyMethod prefix = null, HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
        {
            if (target == null) throw new MissingMethodException("A startup patch target is missing.");
            entries.Add(new Entry { Target = target, Prefix = prefix, Postfix = postfix,
                Transpiler = transpiler, Finalizer = finalizer });
        }
        internal Harmony Apply(Action<int> afterPatch = null)
        {
            // 적용 중인 같은 ID를 재사용하면 실패 복구가 기존 패치까지 제거할 수 있다.
            //
            // Reject an existing owner on these targets before touching any patch.
            foreach (var entry in entries)
                if (Harmony.GetPatchInfo(entry.Target)?.Owners.Contains(harmony.Id) == true)
                    throw new InvalidOperationException("Startup patch group is already installed: " + harmony.Id);
            var touched = new List<MethodBase>();
            try
            {
                foreach (var entry in entries)
                {
                    // Harmony 자체가 중간 실패해도 해당 대상의 등록 정보를 복구한다.
                    //
                    // Include the attempted target even when Harmony itself throws halfway through.
                    if (!touched.Contains(entry.Target)) touched.Add(entry.Target);
                    harmony.Patch(entry.Target, entry.Prefix, entry.Postfix, entry.Transpiler, entry.Finalizer);
                    afterPatch?.Invoke(touched.Count);
                }
                return harmony;
            }
            catch (Exception error)
            {
                var failures = new List<Exception> { error };
                for (int i = touched.Count - 1; i >= 0; i--)
                    try { harmony.Unpatch(touched[i], HarmonyPatchType.All, harmony.Id); }
                    catch (Exception rollback) { failures.Add(rollback); }
                if (failures.Count > 1) throw new PatchRollbackException(failures);
                throw;
            }
        }
    }
}
