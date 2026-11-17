using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Arstraea.KoreanPatch.Fonts;

namespace Arstraea.KoreanPatch.Localization
{
    // 서버가 키 대신 저장한 거래소 문장만 대조한다. 영문 RESX 전체나 한글 사전을
    // 상주시키지 않고, 9쌍의 원문 틀만 보관하며 번역은 게임의 현재 MyTexts에서 얻는다.
    //
    // Recognize server-expanded station notes. Retain only nine English template pairs,
    // never the full RESX or a second translation dictionary; resolve Korean through MyTexts.
    internal sealed class EconomyDatapadCatalog
    {
        private const string Station = "{EconomyStationName}", Gps = "{EconomyStationGPS}";
        internal static readonly EconomyDatapadCatalog Empty = new EconomyDatapadCatalog(new List<Entry>());
        private readonly List<Entry> entries;
        internal int Count => entries.Count;

        private sealed class Entry
        {
            internal string Title, Body, TitleKey, BodyKey;
            internal Regex Pattern;
        }

        private EconomyDatapadCatalog(List<Entry> entries) { this.entries = entries; }

        internal static EconomyDatapadCatalog Load(string path, List<string> notes)
        {
            try
            {
                FilePaths.Read(path);
                FontInstaller.RejectLinks(path);
                if (!File.Exists(path)) return Empty;
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var wanted = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 1; i <= 9; i++)
                { wanted.Add("StoryTitle_EconomyStation" + i); wanted.Add("StoryText_EconomyStation" + i); }
                using (var reader = XmlReader.Create(path, new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 }))
                {
                    reader.MoveToContent();
                    if (reader.Name != "root" || reader.NamespaceURI.Length != 0) throw new XmlException("RESX root is missing.");
                    while (reader.Read())
                    {
                        if (reader.NodeType != XmlNodeType.Element || reader.Depth != 1 || reader.Name != "data") continue;
                        string key = reader.GetAttribute("name");
                        if (!wanted.Contains(key)) continue;
                        if (values.ContainsKey(key)) throw new XmlException("Duplicate station template key: " + key);
                        using (var item = reader.ReadSubtree())
                        {
                            while (item.Read())
                                if (item.NodeType == XmlNodeType.Element && item.Depth == 1 && item.Name == "value")
                                { values.Add(key, item.ReadElementContentAsString()); break; }
                        }
                    }
                }
                var loaded = new List<Entry>();
                for (int i = 1; i <= 9; i++)
                {
                    string titleKey = "StoryTitle_EconomyStation" + i, bodyKey = "StoryText_EconomyStation" + i;
                    string title, body;
                    if (!values.TryGetValue(titleKey, out title) || string.IsNullOrEmpty(title) || title.Length > 250
                        || !values.TryGetValue(bodyKey, out body) || body.Length > 1000 || !HasSlots(body)) continue;
                    body = Lines(body);
                    loaded.Add(new Entry { Title = title, Body = body, TitleKey = titleKey, BodyKey = bodyKey,
                        Pattern = new Regex(@"\A" + Regex.Escape(body)
                            .Replace(Regex.Escape(Station), @"(?<station>[^\r\n]+?)")
                            .Replace(Regex.Escape(Gps), @"(?<gps>GPS:[^\r\n]+:)") + @"\z",
                            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)) });
                }
                notes.Add("Economy datapad templates: ready=" + loaded.Count + "/9.");
                return new EconomyDatapadCatalog(loaded);
            }
            catch (Exception error) when (ReleaseMetadata.IsFileError(error))
            { notes.Add("Economy datapad templates unavailable; stock text retained. " + error.Message); return Empty; }
        }

        internal bool TryTranslate(string title, string body, Func<string, string> lookup,
            out string translatedTitle, out string translatedBody)
        {
            translatedTitle = translatedBody = null;
            if (title == null || body == null || body.Length > 1000) return false;
            Entry found = null;
            string station = null, gps = null;
            try
            {
                foreach (var entry in entries)
                {
                    if (!string.Equals(title, entry.Title, StringComparison.Ordinal)) continue;
                    var match = entry.Pattern.Match(Lines(body));
                    if (!match.Success) continue;
                    string candidateStation = match.Groups["station"].Value, candidateGps = match.Groups["gps"].Value;
                    if (!ValidGps(candidateGps, candidateStation)) continue;
                    if (found != null) return false;
                    found = entry; station = candidateStation; gps = candidateGps;
                }
            }
            catch (RegexMatchTimeoutException) { return false; }
            if (found == null) return false;
            string nextTitle = lookup(found.TitleKey), nextBody = lookup(found.BodyKey);
            if (string.IsNullOrEmpty(nextTitle) || nextTitle == found.TitleKey || nextTitle.Length > 250
                || string.IsNullOrEmpty(nextBody) || !HasSlots(nextBody) || Lines(nextBody) == found.Body) return false;
            // 거래소 이름에 들어 있는 토큰을 다시 해석하지 않도록 원래 슬롯만 한 번에 채운다.
            //
            // Fill original slots in one pass; never reinterpret tokens embedded in a station name.
            string rendered = Regex.Replace(nextBody, @"\{EconomyStation(?:Name|GPS)\}",
                m => m.Value == Station ? station : gps);
            if (rendered.Length > 999) return false;
            translatedTitle = nextTitle; translatedBody = rendered;
            return true;
        }

        private static bool ValidGps(string gps, string station)
        {
            string prefix = "GPS:" + station + ":";
            if (!gps.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string[] fields = gps.Substring(prefix.Length).Split(':');
            if (fields.Length != 5 || fields[4].Length != 0 || !Regex.IsMatch(fields[3], @"\A#[0-9A-Fa-f]{8}\z")) return false;
            for (int i = 0; i < 3; i++)
            {
                double coordinate;
                if (!double.TryParse(fields[i], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out coordinate)
                    || double.IsNaN(coordinate) || double.IsInfinity(coordinate)) return false;
            }
            return true;
        }

        private static bool HasSlots(string value) => value != null
            && value.IndexOf(Station, StringComparison.Ordinal) >= 0 && value.IndexOf(Gps, StringComparison.Ordinal) >= 0
            && value.IndexOf(Station, value.IndexOf(Station, StringComparison.Ordinal) + Station.Length, StringComparison.Ordinal) < 0
            && value.IndexOf(Gps, value.IndexOf(Gps, StringComparison.Ordinal) + Gps.Length, StringComparison.Ordinal) < 0;
        private static string Lines(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}
