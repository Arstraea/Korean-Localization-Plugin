using System;
using System.Reflection;

namespace Arstraea.KoreanPatch
{
    // 전용 서버에서는 언어 메타데이터·파일 설치·입력/UI 패치를 모두 생략한다.
    // 싱글과 호스트도 서버 역할을 가지므로 IsServer는 판별에 사용하지 않는다.
    // 사전 패치 단계에서는 진입 어셈블리의 참조 정보로 클라이언트 로더를 확인한다.
    // 이미 로드된 Game 타입만 조회해 패치 전에 게임 DLL을 먼저 로드하지 않게 한다.
    //
    // Skip all work on a dedicated server, while allowing single-player and listen hosts.
    // Never use IsServer or load a game assembly just to identify the host during preloading.
    internal static class ClientHost
    {
        private static readonly bool clientLoader = IsClientLoader(Assembly.GetEntryAssembly());
        private static bool dedicated;

        internal static bool CanRunClient => clientLoader && !IsDedicated;

        // Pulsar Legacy/Interim은 클라이언트 SpaceEngineers와 Pulsar.Shared를 참조한다.
        // Magnetar는 SpaceEngineersDedicated를 참조한다. 경로·실행 파일 이름은 쓰지 않는다.
        // 역할을 확인하지 못한 호스트에서는 초기 파일 작업도 시작하지 않는다.
        //
        // Identify the supported client loader from reference metadata, without loading dependencies.
        // Reject server references and unknown hosts before any patches or file preparation.
        private static bool IsClientLoader(Assembly entry)
        {
            if (entry == null) return false;
            bool client = false, pulsar = false;
            foreach (AssemblyName reference in entry.GetReferencedAssemblies())
            {
                if (reference.Name == "SpaceEngineersDedicated" || reference.Name == "VRage.Dedicated") return false;
                if (reference.Name == "SpaceEngineers") client = true;
                if (reference.Name == "Pulsar.Shared") pulsar = true;
            }
            return client && pulsar;
        }

        internal static bool IsDedicated
        {
            get
            {
                if (dedicated) return true;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != "Sandbox.Game") continue;
                    var type = assembly.GetType("Sandbox.Engine.Platform.Game", false);
                    var dedicatedField = type?.GetField("IsDedicated", BindingFlags.Public | BindingFlags.Static);
                    if (dedicatedField != null && dedicatedField.FieldType == typeof(bool) && (bool)dedicatedField.GetValue(null))
                        return dedicated = true;
                }
                return false;
            }
        }

    }
}
