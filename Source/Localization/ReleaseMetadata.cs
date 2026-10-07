using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Arstraea.KoreanPatch.Localization
{
    // 배포 버전과 변경사항은 번역 문자열이 아닌 선택적 메타데이터다.
    // 없거나 읽을 수 없어도 실제 resx 설치를 막지 않는다.
    //
    // Optional release metadata is independent of translated strings. Absent/unreadable
    // metadata must not block RESX installation.
    internal sealed class ReleaseMetadata
    {
        internal const string RelativePath = "Korean Patch Plugin/LocalizationVersion.xml";
        internal string Version, PublishedAt, Introduction;
        internal string[] Changes;
        internal DateTime? PublishedDate
        {
            get
            {
                DateTime date;
                return DateTime.TryParseExact(PublishedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date) ? date : (DateTime?)null;
            }
        }

        internal static ReleaseMetadata ReadOptional(string path, List<string> notes)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return Parse(ReadXml(path));
            }
            catch (Exception error) when (IsFileError(error))
            { notes.Add("Release metadata unavailable (translations still supported): " + error.Message); return null; }
        }

        internal static ReleaseMetadata ReadText(string text)
        {
            using (var input = new StringReader(text))
            using (var reader = XmlReader.Create(input, ReaderSettings()))
                return Parse(XElement.Load(reader, LoadOptions.PreserveWhitespace));
        }

        private static ReleaseMetadata Parse(XElement root)
        {
            if (root.Name != "KoreanPatchRelease" || (string)root.Attribute("schemaVersion") != "1")
                throw new InvalidDataException("Unsupported KoreanPatch release metadata schema.");
            string version = ((string)root.Element("Version"))?.Trim();
            if (string.IsNullOrEmpty(version)) throw new InvalidDataException("Release Version is missing.");
            return new ReleaseMetadata { Version = version, PublishedAt = ((string)root.Element("PublishedAt"))?.Trim(),
                Introduction = ((string)root.Element("Introduction"))?.Trim(),
                Changes = root.Element("Changes")?.Elements("Change").Select(e => e.Value).ToArray() ?? new string[0] };
        }

        private static XmlReaderSettings ReaderSettings() => new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 };

        internal static XElement ReadXml(string path)
        {
            using (var reader = XmlReader.Create(path, ReaderSettings()))
                return XElement.Load(reader, LoadOptions.PreserveWhitespace);
        }
        internal static bool IsFileError(Exception error) => error is IOException || error is XmlException
            || error is UnauthorizedAccessException || error is System.Security.SecurityException;
    }
}
