using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Разведка доступа к чужим моделям (Инженерные сети, Площадка).
    /// Только рефлексия: типы моделей не подтверждены живьём, а жёсткая ссылка
    /// на отсутствующую сборку уронила бы загрузку плагина целиком.
    /// </summary>
    internal static class ModelProbe
    {
        internal static void DumpModels(StringBuilder sb)
        {
            sb.AppendLine("=== ОТКРЫТЫЕ МОДЕЛИ ===");
            try
            {
                PluginCoreOps.FilterOpenedModels((Predicate<IProjectModel>)delegate (IProjectModel pm)
                {
                    string name;
                    try { name = PluginCoreOps.GetFileName(pm); } catch { name = "(без имени)"; }

                    object data = null;
                    try { data = pm.LockRead(); } catch (Exception ex) { sb.AppendLine("  " + name + ": LockRead упал -> " + ex.Message); return false; }

                    sb.AppendLine("  " + name + " -> " + (data == null ? "null" : data.GetType().FullName));
                    if (data == null) return false;

                    DumpMember(sb, data, "PipeNetwork");
                    DumpMember(sb, data, "Network");
                    DumpMember(sb, data, "Surface");
                    DumpMember(sb, data, "Terrain");
                    DumpPublicProps(sb, data);
                    ProbePipeNetwork(sb, data);
                    ProbeSurface(sb, data);
                    return false;
                });
            }
            catch (Exception ex) { sb.AppendLine("  !! FilterOpenedModels: " + ex); }
        }

        private static void DumpPublicProps(StringBuilder sb, object data)
        {
            sb.AppendLine("    свойства:");
            foreach (var p in data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                sb.AppendLine("      " + p.PropertyType.Name + " " + p.Name);
        }

        private static void DumpMember(StringBuilder sb, object data, string name)
        {
            object v = Get(data, name);
            if (v != null) sb.AppendLine("    " + name + " = " + v.GetType().FullName);
        }

        /// <summary>Ищем PipeNetwork где угодно в объекте модели и считаем кюветы.</summary>
        private static void ProbePipeNetwork(StringBuilder sb, object data)
        {
            object pn = FindByTypeName(data, "PipeNetwork");
            if (pn == null) return;
            sb.AppendLine("    >>> PipeNetwork найден: " + pn.GetType().FullName);

            object ditches = Get(pn, "Ditches");
            if (ditches == null) { sb.AppendLine("    Ditches = null"); return; }

            int n = 0;
            var en = ditches as IEnumerable;
            if (en != null) foreach (var d in en) n++;
            sb.AppendLine("    Ditches.Count = " + n);

            if (en == null) return;
            int shown = 0;
            foreach (var d in en)
            {
                if (shown >= 3) break;
                object pos = Get(d, "Positions");
                int pc = 0;
                var pe = pos as IEnumerable;
                if (pe != null) foreach (var q in pe) pc++;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "      [{0}] {1}  Name={2} Positions={3} ConnectedNode={4}",
                    shown, d.GetType().Name, Get(d, "Name"), pc,
                    Get(d, "ConnectedNode") == null ? "нет" : "ЕСТЬ"));
                shown++;
            }
        }

        private static void ProbeSurface(StringBuilder sb, object data)
        {
            object sf = FindByTypeName(data, "Surface");
            if (sf == null) return;
            sb.AppendLine("    >>> Surface найден: " + sf.GetType().FullName);

            object sl = Get(sf, "StructureLines");
            if (sl == null) { sb.AppendLine("    StructureLines = null"); return; }

            int n = 0;
            var en = sl as IEnumerable;
            if (en != null) foreach (var l in en) n++;
            sb.AppendLine("    StructureLines.Count = " + n);

            if (en == null) return;
            int shown = 0;
            foreach (var line in en)
            {
                if (shown >= 3) break;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "      [{0}] Count={1} Description=\"{2}\" AreaCode={3} IsClosed={4}",
                    shown, Get(line, "Count"), Get(line, "Description"),
                    Get(line, "AreaCode"), Get(line, "IsClosed")));
                shown++;
            }
        }

        /// <summary>Обходит свойства объекта на один уровень в поисках типа с нужным именем.</summary>
        private static object FindByTypeName(object root, string typeName)
        {
            if (root == null) return null;
            if (root.GetType().Name == typeName) return root;

            foreach (var p in root.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object v;
                try { v = p.GetValue(root, null); } catch { continue; }
                if (v == null) continue;
                if (v.GetType().Name == typeName) return v;
            }
            return null;
        }

        private static object Get(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                return p == null ? null : p.GetValue(obj, null);
            }
            catch { return null; }
        }

        /// <summary>
        /// Концы трубы. ElevationStart/End семантически не подтверждены - какой конец
        /// слева от оси, какой справа. Считаем координаты и смотрим знак векторного
        /// произведения с касательной: положительный = слева по ходу пикетажа.
        /// </summary>
        internal static void DumpPipes(StringBuilder sb, Topomatic.Alg.Road.RoadAlignment road, string roadName)
        {
            sb.AppendLine("=== ТРУБЫ: " + roadName + " ===");
            var proj = new PlanProjector(road);
            var pipes = road.Pipes;
            if (pipes == null || pipes.Count == 0) { sb.AppendLine("  труб нет"); return; }

            for (int i = 0; i < pipes.Count; i++)
            {
                var p = pipes[i];
                if (p == null) continue;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  [{0}] ПК{1:F2} Angle={2:F4} LenToStart={3:F2} LenToEnd={4:F2} ElevStart={5:F3} ElevEnd={6:F3} Mode={7} Type={8} d={9:F2}",
                    i, p.Station, p.Angle, p.LengthToStart, p.LengthToEnd,
                    p.ElevationStart, p.ElevationEnd, p.Mode, p.Type, p.Diameter));

                Topomatic.Cad.Foundation.Vector2D axis, tangent;
                if (!proj.TryAxis(p.Station, out axis)) { sb.AppendLine("      ось не спроецировалась"); continue; }
                if (!proj.TryTangent(p.Station, out tangent)) { sb.AppendLine("      касательная не найдена"); continue; }

                // Труба идёт под углом Angle к оси. Направление её оси в плане -
                // касательная, повёрнутая на Angle.
                double ca = Math.Cos(p.Angle), sa = Math.Sin(p.Angle);
                var dir = new Topomatic.Cad.Foundation.Vector2D(
                    tangent.X * ca - tangent.Y * sa,
                    tangent.X * sa + tangent.Y * ca);

                var startPt = new Topomatic.Cad.Foundation.Vector2D(
                    axis.X - dir.X * p.LengthToStart, axis.Y - dir.Y * p.LengthToStart);
                var endPt = new Topomatic.Cad.Foundation.Vector2D(
                    axis.X + dir.X * p.LengthToEnd, axis.Y + dir.Y * p.LengthToEnd);

                double crossStart = tangent.X * (startPt.Y - axis.Y) - tangent.Y * (startPt.X - axis.X);
                double crossEnd   = tangent.X * (endPt.Y - axis.Y)   - tangent.Y * (endPt.X - axis.X);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "      START xy=({0:F2},{1:F2}) cross={2:F2} -> {3}",
                    startPt.X, startPt.Y, crossStart, crossStart > 0 ? "СЛЕВА" : "СПРАВА"));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "      END   xy=({0:F2},{1:F2}) cross={2:F2} -> {3}",
                    endPt.X, endPt.Y, crossEnd, crossEnd > 0 ? "СЛЕВА" : "СПРАВА"));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "      вода потечёт от {0} (выше) к {1}",
                    p.ElevationStart > p.ElevationEnd ? "START" : "END",
                    p.ElevationStart > p.ElevationEnd ? "END" : "START"));
            }
        }
    }
}
