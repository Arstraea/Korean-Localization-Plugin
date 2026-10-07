using System;
using System.Collections.Generic;
using System.IO;

namespace Arstraea.KoreanPatch.Localization
{
    // 실행 중인 배포 자료만 표시하고 원격 최신 조회는 하지 않는다. 확인 버튼만 저장한다.
    //
    // Show only the installed/running release. Persist acknowledgements only on explicit confirmation.
    internal sealed class NewsCatalog
    {
        internal readonly ReleaseMetadata Pack, Plugin;
        private readonly TranslationState state;
        private readonly string statePath;
        internal bool Available => Pack != null || Plugin != null;
        internal bool Pending => (Pack != null && state.HasPendingNews(Pack)) || (Plugin != null
            && !string.Equals(Plugin.Version, state.LastConfirmedPluginVersion, StringComparison.Ordinal));

        internal NewsCatalog(ReleaseMetadata pack, ReleaseMetadata plugin, TranslationState state, string statePath)
        {
            this.state = state;
            this.statePath = statePath;
            Pack = pack != null && string.Equals(pack.Version, state.InstalledVersion, StringComparison.Ordinal) ? PackWithPublishedDate(pack) : null;
            Plugin = plugin;
        }

        private static ReleaseMetadata PackWithPublishedDate(ReleaseMetadata pack)
        {
            // 날짜 필드 없이 게시된 첫 지원 팩만 확인된 Steam 게시 기록으로 보충한다.
            // 버전 문자열의 날짜를 추측하지 않으며 설치기나 원본 XML은 바꾸지 않는다.
            //
            // Supplement only the initial undated support pack with its verified Steam publication date.
            // Never infer dates from version strings or mutate installer metadata/the source XML.
            if (!string.IsNullOrWhiteSpace(pack.PublishedAt) || pack.Version != "2026-10-05-AddSupport-KoreanPatch") return pack;
            return new ReleaseMetadata { Version = pack.Version, PublishedAt = "2026-10-05",
                Introduction = pack.Introduction, Changes = pack.Changes };
        }

        internal static NewsCatalog Load(ReleaseMetadata pack, string content, List<string> notes)
        {
            string path = TranslationState.PathFor(content);
            // 로컬 XML의 유무로 동작을 바꾸지 않고 실행 DLL의 뉴스만 사용한다.
            //
            // Use the running DLL's news regardless of any local XML files.
            return new NewsCatalog(pack, ReleaseMetadata.ReadText(PluginNews.Xml), TranslationState.Load(path, notes), path);
        }

        internal void Confirm(List<string> notes)
        {
            // 표시한 두 스냅샷만 확인하고 설치기가 기록한 최신 상태는 보존한다.
            //
            // Confirm only displayed snapshots, preserving installation bookkeeping written elsewhere.
            var saved = TranslationState.Load(statePath, notes);
            if (Pack != null)
            {
                state.MarkNotified(Pack);
                saved.LastNotifiedVersion = Pack.Version;
            }
            if (Plugin != null) saved.LastConfirmedPluginVersion = state.LastConfirmedPluginVersion = Plugin.Version;
            saved.SaveIfChanged(statePath, notes);
        }
    }
}
