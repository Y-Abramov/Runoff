using System;
using System.Collections.Generic;
using System.ComponentModel;
using AbrRunoff.Core;
using AbrRunoff.Core.Labels;
using AbrRunoff.Core.Network;
using AbrRunoff.Robur;
using AbrRunoff.Robur.Sources;
using Topomatic.Cad.Foundation;
using Topomatic.ComponentModel;
using Topomatic.Dwg;
using Topomatic.Dwg.Design;
using Topomatic.Dwg.Entities;
using Topomatic.Stg;

namespace AbrRunoff.Entity
{
    [DisplayName("Сеть водоотвода")]
    [EntityController(typeof(DrainageNetworkController))]
    [DesignAlias("Abr_Drainage_Network")]
    public class DwgDrainageNetwork : DwgEntity
    {
        // ЗАМОРОЖЕНО НАВСЕГДА: идентификатор сериализации в проектах юзеров.
        public const string ENTITY_NAME = "Abr.Runoff.DwgDrainageNetwork";

        private RunoffSettings m_Settings = new RunoffSettings();
        private double m_LinkTolerance = 15.0;

        /// <summary>Имена моделей-источников. Что именно читать - решает юзер.</summary>
        private readonly List<string> m_RoadNames = new List<string>();
        private readonly List<string> m_SiteNames = new List<string>();
        private readonly List<string> m_NetworkNames = new List<string>();

        // Ручные смещения подписей (грипы) - сериализуются вместе с объектом.
        private readonly LabelOffsets m_LabelOffsets = new LabelOffsets();

        private DrainageNetwork m_Net;
        private Dictionary<int, int> m_Basins;
        private Drawing m_Plan;
        private DwgBlock m_PlanBlock;
        private bool m_PlanDetail;

        // Подписи последнего ДЕТАЛЬНОГО построения - основа для грипов.
        private readonly List<PlacedLabel> m_Placed = new List<PlacedLabel>();

        // Трубы последнего построения - нужны ведомости, чтобы показать те из них,
        // что не примыкают к водоотводу или лежат выше дна кювета.
        private readonly List<PipeEdgeInfo> m_Pipes = new List<PipeEdgeInfo>();

        public override string EntityName { get { return ENTITY_NAME; } }
        public override string ObjectName { get { return "Сеть водоотвода"; } }

        // Подпись группы в комбо панели свойств = objects[0].ToString().
        public override string ToString() { return "Сеть водоотвода"; }

        public override bool IsProxyGraphics { get { return false; } }
        public override bool IsPurged { get { return m_RoadNames.Count == 0 && m_SiteNames.Count == 0; } }

        [Browsable(false)] public List<string> RoadNames { get { return m_RoadNames; } }
        [Browsable(false)] public List<string> SiteNames { get { return m_SiteNames; } }
        [Browsable(false)] public List<string> NetworkNames { get { return m_NetworkNames; } }
        [Browsable(false)] public RunoffSettings Settings { get { return m_Settings; } }

        [Browsable(false)]
        public double LinkTolerance
        {
            get { return m_LinkTolerance; }
            set { m_LinkTolerance = value; InvalidateScheme(); }
        }

        public void SetSettings(RunoffSettings settings)
        {
            BeginUpdate();
            try { m_Settings = settings ?? new RunoffSettings(); InvalidateScheme(); }
            finally { EndUpdate(); }
        }

        public void InvalidateScheme()
        {
            m_Net = null;
            m_Basins = null;
            m_Plan = null;
            m_PlanBlock = null;
            Invalidate();
        }

        /// <summary>
        /// Подписи последнего детального построения - контроллер вешает на них грипы.
        /// Пусто на дальнем зуме: подписей там нет, таскать нечего.
        ///
        /// Отдаётся КОПИЯ: перерисовка чистит рабочий список, а грипы могут
        /// обходить его в этот момент.
        /// </summary>
        [Browsable(false)]
        internal IList<PlacedLabel> PlacedLabels
        {
            get { return m_PlanDetail ? new List<PlacedLabel>(m_Placed) : new List<PlacedLabel>(); }
        }

        /// <summary>
        /// Строки ведомости про трубы с изъяном: не примыкает к водоотводу либо
        /// лоток выше дна кювета. Труба, тихо выпавшая из сети, - худшее поведение.
        /// </summary>
        internal List<AbrRunoff.Report.NetworkRow> PipeProblemRows()
        {
            var rows = new List<AbrRunoff.Report.NetworkRow>();
            if (m_Net == null) GetPlanBlock(true);

            foreach (var p in m_Pipes)
            {
                if (p.Detached)
                    rows.Add(AbrRunoff.Report.NetworkReport.PipeProblemRow(
                        p.SourceRef, "труба не примыкает к водоотводу", p.Start.X, p.Start.Y));
                else if (p.AboveDitch)
                    rows.Add(AbrRunoff.Report.NetworkReport.PipeProblemRow(
                        p.SourceRef,
                        string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                      "лоток выше дна кювета на {0:F2} м - вода не уйдёт", p.AboveDitchBy),
                        p.Start.X, p.Start.Y));
            }
            return rows;
        }

        /// <summary>Запомнить, куда проектировщик оттащил подпись грипом.</summary>
        internal void SetLabelOffset(string key, double dx, double dy)
        {
            m_LabelOffsets.Set(key, dx, dy);
            InvalidateScheme();
        }

        /// <summary>Вернуть одну подпись на автоматическое место.</summary>
        internal void ResetLabelOffset(string key)
        {
            m_LabelOffsets.Remove(key);
            InvalidateScheme();
        }

        /// <summary>Вернуть все подписи сети на автоматические места.</summary>
        public void ResetAllLabelOffsets()
        {
            m_LabelOffsets.Clear();
            InvalidateScheme();
        }

        /// <summary>Есть ли хоть одна подпись, оттащенная вручную.</summary>
        [Browsable(false)]
        public bool HasManualLabels { get { return m_LabelOffsets.Count > 0; } }

        [Browsable(false)]
        public DrainageNetwork Network
        {
            get { if (m_Net == null) GetPlanBlock(true); return m_Net ?? new DrainageNetwork(); }
        }

        [Browsable(false)]
        public Dictionary<int, int> Basins
        {
            get { if (m_Basins == null) GetPlanBlock(true); return m_Basins ?? new Dictionary<int, int>(); }
        }

        public DwgBlock GetPlanBlock(bool detail)
        {
            // Кэш помнит уровень детализации - грабли v1: иначе блок, собранный
            // на дальнем зуме, так и остаётся без стрелок и подписей.
            if (m_PlanBlock != null && m_PlanDetail == detail) return m_PlanBlock;
            m_PlanDetail = detail;

            m_Plan = new Drawing();
            var block = m_Plan.Blocks.Add("plan");
            m_Placed.Clear();

            try
            {
                var elements = new List<DrainageElement>();
                var pipes = new List<PipeEdgeInfo>();
                NetworkSourceCollector.Collect(this, elements, pipes);

                m_Net = NetworkBuilder.Build(elements, pipes, m_Settings, m_LinkTolerance);
                m_Basins = BasinAssigner.Assign(m_Net);

                // Флаги проблем ставит Build - забираем трубы уже размеченными.
                m_Pipes.Clear();
                m_Pipes.AddRange(pipes);

                NetworkGeometry.Build(m_Net, m_Basins,
                                      RunoffStyle.GlyphBase * m_Settings.GlyphScale,
                                      m_Settings.TextHeight, detail,
                                      delegate (DwgEntity e) { block.Add(e); },
                                      m_LabelOffsets, m_Placed, m_Settings);
            }
            catch (Exception ex)
            {
                // ДИАГНОСТИКА: сборка сети - новый код, не пройден живым гейтом Task 0.
                // Раньше исключение здесь падало необработанным до самого Execute()
                // и роняло Robur с голым стеком без единой нашей строки. Теперь -
                // тихо деградируем (пустая, но не null сеть) и пишем полный ToString()
                // с местом падения на диск, а не молча глотаем ошибку.
                try
                {
                    System.IO.File.WriteAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "abr_runoff_network_error.txt"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex);
                }
                catch { }
                m_Net = new AbrRunoff.Core.Network.DrainageNetwork();
                m_Basins = new Dictionary<int, int>();
            }

            m_PlanBlock = block;
            return m_PlanBlock;
        }

        protected override void OnRegen(EventArgs e)
        {
            var block = GetPlanBlock(true);
            if (block != null && block.Count > 0) Bounds = block.Bounds;
            else Invalidate();
        }

        protected override void OnTransform(Matrix matrix)
        {
            // Сеть не хранит собственных мировых координат - вся геометрия
            // берётся у источников при каждой перерисовке (см. GetPlanBlock).
        }

        protected override void OnAssign(DwgEntity source)
        {
            var other = source as DwgDrainageNetwork;
            if (other == null) return;
            m_Settings = other.m_Settings.Clone();
            m_LinkTolerance = other.m_LinkTolerance;
            Copy(other.m_RoadNames, m_RoadNames);
            Copy(other.m_SiteNames, m_SiteNames);
            Copy(other.m_NetworkNames, m_NetworkNames);
            m_LabelOffsets.CopyFrom(other.m_LabelOffsets);
            InvalidateScheme();
        }

        private static void Copy(List<string> from, List<string> to)
        {
            to.Clear();
            to.AddRange(from);
        }

        protected override void OnSaveToStg(StgNode node)
        {
            node.AddString("roads", string.Join("|", m_RoadNames.ToArray()));
            node.AddString("sites", string.Join("|", m_SiteNames.ToArray()));
            node.AddString("networks", string.Join("|", m_NetworkNames.ToArray()));
            node.AddDouble("linkTol", m_LinkTolerance);
            node.AddDouble("minGrade", m_Settings.MinGradePermille);
            node.AddDouble("plateauEps", m_Settings.PlateauEpsPermille);
            node.AddDouble("sampleStep", m_Settings.SampleStep);
            node.AddDouble("glyphScale", m_Settings.GlyphScale);
            node.AddDouble("textHeight", m_Settings.TextHeight);
            node.AddString("labelOffs", m_LabelOffsets.Serialize());
            node.AddInt32("basinZone", (int)m_Settings.BasinZone);
            node.AddDouble("basinOpacity", m_Settings.BasinZoneOpacity);
        }

        protected override void OnLoadFromStg(StgNode node)
        {
            Split(node.GetString("roads", ""), m_RoadNames);
            Split(node.GetString("sites", ""), m_SiteNames);
            Split(node.GetString("networks", ""), m_NetworkNames);
            m_LinkTolerance = node.GetDouble("linkTol", 15.0);
            m_Settings = new RunoffSettings
            {
                MinGradePermille   = node.GetDouble("minGrade", 5.0),
                PlateauEpsPermille = node.GetDouble("plateauEps", 0.2),
                SampleStep         = node.GetDouble("sampleStep", 5.0),
                GlyphScale         = node.GetDouble("glyphScale", 1.0),
                TextHeight         = node.GetDouble("textHeight", 1.5)
            };
            m_Settings.BasinZone = (BasinZoneStyle)node.GetInt32("basinZone", (int)BasinZoneStyle.Transparent);
            m_Settings.BasinZoneOpacity = node.GetDouble("basinOpacity", 18.0);
            LabelOffsets.Deserialize(node.GetString("labelOffs", ""), m_LabelOffsets);
            InvalidateScheme();
        }

        private static void Split(string raw, List<string> target)
        {
            target.Clear();
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var s in raw.Split('|'))
                if (s.Length > 0) target.Add(s);
        }
    }
}
