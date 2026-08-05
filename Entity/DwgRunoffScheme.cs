using System;
using System.Collections.Generic;
using System.ComponentModel;
using AbrRunoff.Core;
using AbrRunoff.Robur;
using Topomatic.Alg.Road;
using Topomatic.Cad.Foundation;
using Topomatic.ComponentModel;
using Topomatic.Dwg;
using Topomatic.Dwg.Design;
using Topomatic.Dwg.Entities;
using Topomatic.Stg;

namespace AbrRunoff.Entity
{
    [DisplayName("Схема стока")]
    [EntityController(typeof(RunoffSchemeController))]
    [DesignAlias("Abr_Runoff_Scheme")]
    public class DwgRunoffScheme : DwgEntity
    {
        // ЗАМОРОЖЕНО НАВСЕГДА: идентификатор сериализации в проектах юзеров.
        // Менять нельзя - объекты в старых проектах молча выпадут при загрузке.
        public const string ENTITY_NAME = "Abr.Runoff.DwgRunoffScheme";

        private string m_RoadName = "";
        private RunoffSettings m_Settings = new RunoffSettings();

        // Снапшот: чтобы схема что-то рисовала, когда дорога не открыта.
        private readonly List<DitchSample> m_SnapLeft = new List<DitchSample>();
        private readonly List<DitchSample> m_SnapRight = new List<DitchSample>();

        // Кэши живого пересчёта (не сериализуются).
        private RunoffResult m_Result;
        private Drawing m_Plan;
        private DwgBlock m_PlanBlock;
        private bool m_PlanDetail;      // уровень детализации, на котором собран кэш
        private bool m_Stale;

        public override string EntityName { get { return ENTITY_NAME; } }
        public override string ObjectName { get { return "Схема стока"; } }

        // ИМЕННО ЭТО читает комбо панели свойств: подпись группы = objects[0].ToString(),
        // а DwgEntity.ToString() возвращает GetType().Name.
        public override string ToString() { return "Схема стока"; }

        public override bool IsProxyGraphics { get { return false; } }
        public override bool IsPurged { get { return string.IsNullOrEmpty(m_RoadName); } }

        [Browsable(false)]
        public string RoadName { get { return m_RoadName; } set { m_RoadName = value ?? ""; } }

        [Browsable(false)]
        public RunoffSettings Settings { get { return m_Settings; } }

        /// <summary>Дорога не открыта - схема нарисована по снапшоту.</summary>
        [Browsable(false)]
        public bool IsStale { get { return m_Stale; } }

        public void SetSettings(RunoffSettings settings)
        {
            BeginUpdate();
            try { m_Settings = settings ?? new RunoffSettings(); InvalidateScheme(); }
            finally { EndUpdate(); }
        }

        public void InvalidateScheme()
        {
            m_Result = null;
            m_Plan = null;
            m_PlanBlock = null;
            Invalidate();
        }

        /// <summary>Записать снапшот после успешного живого расчёта.</summary>
        private void StoreSnapshot(IList<DitchSample> left, IList<DitchSample> right)
        {
            m_SnapLeft.Clear(); m_SnapLeft.AddRange(left);
            m_SnapRight.Clear(); m_SnapRight.AddRange(right);
        }

        /// <summary>Находит свою дорогу среди открытых по имени модели.</summary>
        private RoadAlignment FindRoad()
        {
            foreach (var rr in RoadAccess.GetOpenRoads())
                if (string.Equals(rr.Name, m_RoadName, StringComparison.OrdinalIgnoreCase))
                    return rr.Road;
            return null;
        }

        public DwgBlock GetPlanBlock(bool detail)
        {
            // Кэш обязан помнить, НА КАКОМ уровне детализации он собран: иначе
            // блок, построенный при первом дальнем зуме, так и останется без
            // стрелок и подписей при последующем приближении.
            if (m_PlanBlock != null && m_PlanDetail == detail) return m_PlanBlock;
            m_PlanDetail = detail;

            m_Plan = new Drawing();
            var block = m_Plan.Blocks.Add("plan");

            var road = FindRoad();
            List<DitchSample> left, right;
            PlanProjector proj;

            if (road != null)
            {
                var stations = DitchReader.BuildStations(road, m_Settings.SampleStep);
                string wl, wr;
                left = DitchReader.Read(road, DitchSide.Left, stations, out wl);
                right = DitchReader.Read(road, DitchSide.Right, stations, out wr);
                proj = new PlanProjector(road);
                StoreSnapshot(left, right);
                m_Stale = false;

                m_Result = FlowDetector.Detect(new RunoffInput { Left = left, Right = right, Settings = m_Settings });
                RunoffChecks.Apply(m_Result, m_Settings, PipeMatcher.Read(road));
                if (wl != null) m_Result.Warnings.Add(wl);
                if (wr != null) m_Result.Warnings.Add(wr);
            }
            else
            {
                // Дорога закрыта: рисуем снапшот, проецировать нечем -> только то,
                // что уже посчитано. Без снапшота объект был бы пустотой.
                m_Stale = true;
                left = m_SnapLeft; right = m_SnapRight;
                m_Result = FlowDetector.Detect(new RunoffInput { Left = left, Right = right, Settings = m_Settings });
                m_PlanBlock = block;
                return m_PlanBlock;
            }

            RunoffGeometry.Build(m_Result, left, right, proj, m_Settings, detail,
                                 delegate (DwgEntity e) { block.Add(e); });

            m_PlanBlock = block;
            return m_PlanBlock;
        }

        [Browsable(false)]
        public RunoffResult Result
        {
            get
            {
                if (m_Result == null) GetPlanBlock(true);
                return m_Result ?? new RunoffResult();
            }
        }

        protected override void OnRegen(EventArgs e)
        {
            var block = GetPlanBlock(true);
            if (block != null && block.Count > 0) Bounds = block.Bounds;
            else Invalidate();
        }

        protected override void OnTransform(Matrix matrix)
        {
            // Схема не хранит собственных мировых координат - вся геометрия
            // берётся у дороги при каждой перерисовке (см. GetPlanBlock).
            // Переместить/повернуть примитив нечем - трогать RoadName некорректно.
        }

        protected override void OnAssign(DwgEntity source)
        {
            var other = source as DwgRunoffScheme;
            if (other == null) return;
            m_RoadName = other.m_RoadName;
            m_Settings = other.m_Settings.Clone();
            m_SnapLeft.Clear(); m_SnapLeft.AddRange(other.m_SnapLeft);
            m_SnapRight.Clear(); m_SnapRight.AddRange(other.m_SnapRight);
            InvalidateScheme();
        }

        protected override void OnSaveToStg(StgNode node)
        {
            node.AddString("road", m_RoadName);
            node.AddDouble("minGrade", m_Settings.MinGradePermille);
            node.AddDouble("plateauEps", m_Settings.PlateauEpsPermille);
            node.AddDouble("pipeTol", m_Settings.PipeTolerance);
            node.AddDouble("sampleStep", m_Settings.SampleStep);
            node.AddDouble("arrowStep", m_Settings.ArrowStep);
            node.AddDouble("glyphScale", m_Settings.GlyphScale);
            node.AddDouble("textHeight", m_Settings.TextHeight);
            // Флаги пишем числом: отдельного AddBool в StgNode нет.
            node.AddDouble("labelBg", m_Settings.LabelBackground ? 1.0 : 0.0);
            node.AddDouble("ptLabels", m_Settings.ShowPointLabels ? 1.0 : 0.0);
            node.AddDouble("grLabels", m_Settings.ShowGradeLabels ? 1.0 : 0.0);
            SaveSamples(node, "snapL", m_SnapLeft);
            SaveSamples(node, "snapR", m_SnapRight);
        }

        protected override void OnLoadFromStg(StgNode node)
        {
            m_RoadName = node.GetString("road", "");
            m_Settings = new RunoffSettings
            {
                MinGradePermille   = node.GetDouble("minGrade", 5.0),
                PlateauEpsPermille = node.GetDouble("plateauEps", 0.2),
                PipeTolerance      = node.GetDouble("pipeTol", 15.0),
                SampleStep         = node.GetDouble("sampleStep", 5.0),
                ArrowStep          = node.GetDouble("arrowStep", 25.0),
                GlyphScale         = node.GetDouble("glyphScale", 1.0),
                TextHeight         = node.GetDouble("textHeight", 1.5),
                LabelBackground    = node.GetDouble("labelBg", 1.0) > 0.5,
                ShowPointLabels    = node.GetDouble("ptLabels", 1.0) > 0.5,
                ShowGradeLabels    = node.GetDouble("grLabels", 1.0) > 0.5
            };
            LoadSamples(node, "snapL", m_SnapLeft);
            LoadSamples(node, "snapR", m_SnapRight);
            InvalidateScheme();
        }

        // Снапшот пишется строкой «пикет:отметка:смещение;...» - формат простой,
        // ломаться при добавлении полей нечему, а StgNode-массивы структур требуют
        // второго аргумента типа и лишней возни.
        private static void SaveSamples(StgNode node, string key, List<DitchSample> list)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (var s in list)
            {
                if (!s.IsDitch) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.AppendFormat(ci, "{0}:{1}:{2}", s.Station, s.BottomZ, s.Offset);
            }
            node.AddString(key, sb.ToString());
        }

        private static void LoadSamples(StgNode node, string key, List<DitchSample> list)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            list.Clear();
            string raw = node.GetString(key, "");
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var chunk in raw.Split(';'))
            {
                var parts = chunk.Split(':');
                if (parts.Length != 3) continue;
                double st, z, off;
                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, ci, out st)) continue;
                if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, ci, out z)) continue;
                if (!double.TryParse(parts[2], System.Globalization.NumberStyles.Float, ci, out off)) continue;
                list.Add(new DitchSample { Station = st, BottomZ = z, Offset = off, IsDitch = true });
            }
        }
    }
}
