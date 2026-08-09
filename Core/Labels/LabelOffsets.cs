using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AbrRunoff.Core.Labels
{
    /// <summary>
    /// Ручные смещения подписей, поставленные проектировщиком грипами.
    /// Ключ - устойчивый идентификатор подписи (см. LabelKeys), значение -
    /// смещение от автоматической точки привязки.
    ///
    /// Хранится в примитиве и уезжает в STG одной строкой: смещение обязано
    /// пережить сохранение и переоткрытие проекта, иначе ручная правка схемы
    /// теряется при каждом закрытии чертежа.
    /// </summary>
    public sealed class LabelOffsets
    {
        private struct Offset { public double Dx, Dy; }

        private readonly Dictionary<string, Offset> m_Map = new Dictionary<string, Offset>();

        public int Count { get { return m_Map.Count; } }

        public bool TryGet(string key, out double dx, out double dy)
        {
            Offset o;
            if (key != null && m_Map.TryGetValue(key, out o)) { dx = o.Dx; dy = o.Dy; return true; }
            dx = 0.0; dy = 0.0;
            return false;
        }

        public void Set(string key, double dx, double dy)
        {
            if (string.IsNullOrEmpty(key)) return;
            m_Map[key] = new Offset { Dx = dx, Dy = dy };
        }

        public void Remove(string key)
        {
            if (key != null) m_Map.Remove(key);
        }

        public void Clear() { m_Map.Clear(); }

        public void CopyFrom(LabelOffsets other)
        {
            m_Map.Clear();
            if (other == null) return;
            foreach (var kv in other.m_Map) m_Map[kv.Key] = kv.Value;
        }

        /// <summary>Одна строка вида «ключ=dx,dy;ключ=dx,dy» - формат STG.</summary>
        public string Serialize()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (var kv in m_Map)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.AppendFormat(ci, "{0}={1},{2}", kv.Key, kv.Value.Dx, kv.Value.Dy);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Разбор строки STG. Битые куски молча пропускаются: строка может прийти
        /// из чужого или более старого проекта, ронять загрузку чертежа нельзя.
        /// </summary>
        public static void Deserialize(string raw, LabelOffsets into)
        {
            if (into == null) return;
            into.Clear();
            if (string.IsNullOrEmpty(raw)) return;

            var ci = CultureInfo.InvariantCulture;
            foreach (var chunk in raw.Split(';'))
            {
                if (chunk.Length == 0) continue;

                int eq = chunk.IndexOf('=');
                if (eq <= 0 || eq == chunk.Length - 1) continue;

                string key = chunk.Substring(0, eq);
                var parts = chunk.Substring(eq + 1).Split(',');
                if (parts.Length != 2) continue;

                double dx, dy;
                if (!double.TryParse(parts[0], NumberStyles.Float, ci, out dx)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, ci, out dy)) continue;

                into.Set(key, dx, dy);
            }
        }
    }
}
