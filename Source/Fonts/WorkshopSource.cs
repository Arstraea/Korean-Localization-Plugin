using System;
using System.IO;
using System.Reflection;

namespace Arstraea.KoreanPatch.Fonts
{
    internal sealed class WorkshopSnapshot
    {
        internal uint Flags, Timestamp;
        internal string Folder;
        internal ulong Downloaded, Total;
        internal bool HasProgress, AwaitingResult;
        internal bool Subscribed => (Flags & 1) != 0;
        internal bool Installed => (Flags & 4) != 0;
        internal bool Downloading => (Flags & 16) != 0;
        internal bool Pending => (Flags & 32) != 0;
        internal bool Ready => Subscribed && LocalReady;
        internal bool LocalReady => Installed && (Flags & (8 | 16 | 32)) == 0 && !AwaitingResult
            && !string.IsNullOrWhiteSpace(Folder);
        internal bool SamePackage(WorkshopSnapshot other) => other != null && other.Ready
            && string.Equals(Folder, other.Folder, StringComparison.OrdinalIgnoreCase) && Timestamp == other.Timestamp;
    }

    internal interface IWorkshopSource : IDisposable
    {
        WorkshopSnapshot Read();
        void EnsureConnected();
        void RequestDownload();
        void Pump();
    }

    // Steam은 로더가 초기화한다. 기본 한글패치는 구독을 요구하고 선택적 HUD 폰트는
    // 구독 변경 없이 임시 다운로드할 수 있다. 다운로드를 요청한 경우
    // 해당 게임/항목의 완료 콜백까지 기다린 뒤에만 설치 폴더를 읽는다.
    //
    // Reuse loader-owned Steam. The main patch requires subscription; optional HUD
    // fonts may use temporary downloads without subscribing. Downloads we
    // request must deliver the matching app/item completion callback before disk access.
    internal sealed class SteamWorkshopSource : IWorkshopSource
    {
        private readonly object id;
        private readonly ulong workshopId;
        private readonly bool requireSubscription;
        private readonly MethodInfo state, install, downloadInfo, download, pump, loggedOn;
        private readonly Type callbackType;
        private IDisposable callback;
        private volatile bool awaiting;
        private volatile int downloadError;

        internal SteamWorkshopSource(Assembly assembly, ulong workshopId = FontStartup.WorkshopId, bool requireSubscription = true)
        {
            this.workshopId = workshopId;
            this.requireSubscription = requireSubscription;
            Type ugc = assembly.GetType("Steamworks.SteamUGC", true);
            Type idType = assembly.GetType("Steamworks.PublishedFileId_t", true);
            id = Activator.CreateInstance(idType, new object[] { workshopId });
            state = Required(ugc, "GetItemState", idType);
            install = Required(ugc, "GetItemInstallInfo", idType, typeof(ulong).MakeByRefType(),
                typeof(string).MakeByRefType(), typeof(uint), typeof(uint).MakeByRefType());
            downloadInfo = Required(ugc, "GetItemDownloadInfo", idType, typeof(ulong).MakeByRefType(), typeof(ulong).MakeByRefType());
            download = Required(ugc, "DownloadItem", idType, typeof(bool));
            pump = Required(assembly.GetType("Steamworks.SteamAPI", true), "RunCallbacks");
            loggedOn = Required(assembly.GetType("Steamworks.SteamUser", true), "BLoggedOn");
            callbackType = assembly.GetType("Steamworks.Callback`1", true)
                .MakeGenericType(assembly.GetType("Steamworks.DownloadItemResult_t", true));
        }

        private static MethodInfo Required(Type type, string name, params Type[] args)
            => type.GetMethod(name, args) ?? throw new MissingMethodException(type.FullName, name);

        public WorkshopSnapshot Read()
        {
            if (downloadError != 0)
            {
                EnsureConnected();
                throw new PatchPreparationException("Steam이 한글화 팩 다운로드를 완료하지 못했습니다. Steam 다운로드 상태와 인터넷 연결을 확인한 뒤 다시 실행해 주세요.\nSteam 결과 코드: " + downloadError);
            }
            var value = new WorkshopSnapshot { Flags = (uint)state.Invoke(null, new[] { id }), AwaitingResult = awaiting };
            if ((!requireSubscription || value.Subscribed) && value.Installed && (value.Flags & (8 | 16 | 32)) == 0 && !awaiting)
            {
                object[] args = { id, 0UL, null, 32768U, 0U };
                if (!(bool)install.Invoke(null, args) || string.IsNullOrWhiteSpace(args[2] as string))
                {
                    EnsureConnected();
                    throw new PatchPreparationException("Steam에서 한글화 팩의 다운로드 위치를 확인하지 못했습니다. 다운로드 완료를 확인한 뒤 다시 실행해 주세요.");
                }
                value.Folder = Path.GetFullPath((string)args[2]);
                value.Timestamp = (uint)args[4];
            }
            else if (!requireSubscription || value.Subscribed)
            {
                object[] args = { id, 0UL, 0UL };
                bool available = (bool)downloadInfo.Invoke(null, args);
                value.Downloaded = (ulong)args[1]; value.Total = (ulong)args[2];
                value.HasProgress = available && value.Downloading && value.Total > 0 && value.Downloaded <= value.Total;
            }
            return value;
        }

        // 사용 가능한 로컬 자료에는 온라인 연결을 요구하지 않는다. 준비가 막혔을 때만
        // 서버 연결을 확인하며, 연결 실패를 미구독이나 게임 미보유로 단정하지 않는다.
        //
        // Do not require connectivity for usable local content. Check only blocked
        // preparation; loss of connectivity does not prove non-subscription or ownership.
        public void EnsureConnected() => RequireConnection(() => (bool)loggedOn.Invoke(null, null));

        internal static void RequireConnection(Func<bool> check)
        {
            bool connected;
            try { connected = check(); }
            catch (Exception error)
            {
                while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
                throw new PatchPreparationException("Steam 연결 상태를 확인할 수 없어 한글화 팩 다운로드 대기를 계속할 수 없습니다.\n\nSteam 실행·로그인 상태를 확인하고 게임과 Pulsar를 다시 실행해 주세요.", error);
            }
            if (!connected)
                throw new PatchPreparationException("Steam 연결 상태를 확인할 수 없어 한글화 팩 다운로드 대기를 계속할 수 없습니다.\n\nSteam이 오프라인 모드라면 온라인으로 전환하고, 로그인과 인터넷 연결을 확인한 뒤 게임과 Pulsar를 다시 실행해 주세요.");
        }

        public void RequestDownload()
        {
            if (awaiting) return;
            // 구독 해제와 요청 사이의 경쟁도 확인한다. 높은 우선순위로 다른 다운로드를 멈추지 않는다.
            // Recheck subscription at the request boundary; never pause other downloads with high priority.
            if (requireSubscription && ((uint)state.Invoke(null, new[] { id }) & 1) == 0) return;
            if (callback == null)
            {
                MethodInfo create = callbackType.GetMethod("Create");
                Type resultType = callbackType.GetGenericArguments()[0];
                MethodInfo handler = GetType().GetMethod(nameof(OnDownload), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(resultType);
                Delegate dispatch = Delegate.CreateDelegate(create.GetParameters()[0].ParameterType, this, handler);
                callback = (IDisposable)create.Invoke(null, new object[] { dispatch });
            }
            awaiting = true;
            PatchStartupGate.WriteLog("Requesting Workshop download " + workshopId + " at normal priority; subscription required=" + requireSubscription + ".");
            if (!(bool)download.Invoke(null, new[] { id, (object)false }))
            {
                awaiting = false;
                EnsureConnected();
                throw new PatchPreparationException("Steam이 한글화 팩 다운로드 요청을 받지 못했습니다. Steam 로그인과 다운로드 상태를 확인한 뒤 다시 실행해 주세요.");
            }
        }

        private void OnDownload<T>(T result)
        {
            Type type = typeof(T);
            object app = type.GetField("m_unAppID").GetValue(result);
            object item = type.GetField("m_nPublishedFileId").GetValue(result);
            uint appId = (uint)app.GetType().GetField("m_AppId").GetValue(app);
            ulong itemId = (ulong)item.GetType().GetField("m_PublishedFileId").GetValue(item);
            if (appId != 244850 || itemId != workshopId || !awaiting) return;
            int code = Convert.ToInt32(type.GetField("m_eResult").GetValue(result));
            PatchStartupGate.WriteLog("Workshop download completed with Steam result " + code + ".");
            if (code != 1) downloadError = code;
            awaiting = false;
        }

        public void Pump() => pump.Invoke(null, null);
        public void Dispose() { callback?.Dispose(); callback = null; }
    }
}
