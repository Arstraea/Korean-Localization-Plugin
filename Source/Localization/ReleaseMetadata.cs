using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Arstraea.KoreanPatch.Localization
{
    // 배포 버전과 변경사항은 번역 문자열이 아닌 선택적 메타데이터다.
    // 없거나 읽을 수 없어도 실제 resx 설치를 막지 않는다. 뉴스 UI는 후속 작업이다.
    //
    // Optional release metadata is independent of translated strings. Absent/unreadable
    // metadata must not block RESX installation; news presentation is deferred.
    internal sealed class ReleaseMetadata
    {
        internal const string RelativePath = "Korean Patch Plugin/LocalizationVersion.xml";
        internal string Version, PublishedAt;
        internal string[] Changes;

        internal static ReleaseMetadata ReadOptional(string path, List<string> notes)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var root = ReadXml(path);
                if (root.Name != "KoreanPatchRelease" || (string)root.Attribute("schemaVersion") != "1")
                    throw new InvalidDataException("Unsupported KoreanPatch release metadata schema.");
                string version = ((string)root.Element("Version"))?.Trim();
                if (string.IsNullOrEmpty(version)) throw new InvalidDataException("Release Version is missing.");
                return new ReleaseMetadata { Version = version, PublishedAt = (string)root.Element("PublishedAt"),
                    Changes = root.Element("Changes")?.Elements("Change").Select(e => e.Value).ToArray() ?? new string[0] };
            }
            catch (Exception error) when (IsFileError(error))
            { notes.Add("Release metadata unavailable (translations still supported): " + error.Message); return null; }
        }

        internal static XElement ReadXml(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 }))
                return XElement.Load(reader, LoadOptions.PreserveWhitespace);
        }
        internal static bool IsFileError(Exception error) => error is IOException || error is XmlException
            || error is UnauthorizedAccessException || error is System.Security.SecurityException;
    }
}
