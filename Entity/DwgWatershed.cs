using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using AbrRunoff.Core.Watershed;
using Topomatic.Cad.Foundation;
using Topomatic.ComponentModel;
using Topomatic.Dwg;
using Topomatic.Dwg.Design;
using Topomatic.Dwg.Entities;
using Topomatic.Stg;

namespace AbrRunoff.Entity
{
    [DisplayName("Водосбор")]
    [EntityController(typeof(WatershedController))]
    [DesignAlias("Abr_Runoff_Watershed")]
    public class DwgWatershed : DwgEntity
    {
        // ЗАМОРОЖЕНО НАВСЕГДА: идентификатор сериализации в проектах юзеров.
        // Менять нельзя - объекты в старых проектах молча выпадут при загрузке.
        public const string ENTITY_NAME = "Abr.Runoff.DwgWatershed";

        private string m_SurfaceName = "";
        private string m_DesignSurfaceName = "";
        private string m_OutletName = "";
        private double m_OutletX;
        private double m_OutletY;
        private double m_Step = 5.0;
        private WatershedSettings m_Settings = new WatershedSettings();

        // Хеш исходных данных на момент последнего расчёта - сравнение со свежим
        // хешом решает, устарел ли снапшот (см. CheckStale).
        private string m_SourceHash = "";

        // Снапшот: единственное, что рисуется. Пересчёт - только по явной команде,
        // не в отрисовке - гидрология по сетке в сотни тысяч ячеек подвесила бы её.
        private WatershedResult m_Snapshot;

        private Drawing m_Plan;
        private DwgBlock m_PlanBlock;
        private bool m_PlanDetail;

        public override string EntityName { get { return ENTITY_NAME; } }
        public override string ObjectName { get { return "Водосбор"; } }
        public override string ToString() { return "Водосбор"; }
        public override bool IsProxyGraphics { get { return false; } }
        public override bool IsPurged { get { return m_Snapshot == null; } }

        [Browsable(false)] public string SurfaceName { get { return m_SurfaceName; } }
        [Browsable(false)] public string DesignSurfaceName { get { return m_DesignSurfaceName; } }
        [Browsable(false)] public string OutletName { get { return m_OutletName; } }
        [Browsable(false)] public double OutletX { get { return m_OutletX; } }
        [Browsable(false)] public double OutletY { get { return m_OutletY; } }
        [Browsable(false)] public double Step { get { return m_Step; } }
        [Browsable(false)] public WatershedSettings Settings { get { return m_Settings; } }
        [Browsable(false)] public WatershedResult Snapshot { get { return m_Snapshot; } }

        /// <summary>
        /// Расчёт устарел: рельеф, проектная поверхность, точка створа или шаг
        /// изменились после последнего построения. Примитив продолжает рисовать
        /// последний ЧЕСТНЫЙ результат - молча пересчитывать в отрисовке нельзя.
        /// </summary>
        [Browsable(false)]
        public bool IsStale { get; private set; }

        public void CheckStale(string currentHash)
        {
            IsStale = !string.IsNullOrEmpty(m_SourceHash) && m_SourceHash != currentHash;
        }

        /// <summary>Задаёт рецепт расчёта и его результат заново - вызывается командой построения/пересчёта.</summary>
        public void SetResult(string surfaceName, string designSurfaceName, string outletName,
                              double outletX, double outletY, double step, WatershedSettings settings,
                              string sourceHash, WatershedResult result)
        {
            BeginUpdate();
            try
            {
                m_SurfaceName = surfaceName ?? "";
                m_DesignSurfaceName = designSurfaceName ?? "";
                m_OutletName = outletName ?? "";
                m_OutletX = outletX;
                m_OutletY = outletY;
                m_Step = step;
                m_Settings = settings ?? new WatershedSettings();
                m_SourceHash = sourceHash ?? "";
                m_Snapshot = result;
                IsStale = false;
                InvalidateScheme();
            }
            finally { EndUpdate(); }
        }

        public void InvalidateScheme()
        {
            m_Plan = null;
            m_PlanBlock = null;
            Invalidate();
        }

        public DwgBlock GetPlanBlock(bool detail)
        {
            // Кэш помнит уровень детализации: иначе блок, собранный на дальнем
            // зуме, так и остаётся без стрелки и знаков при приближении.
            if (m_PlanBlock != null && m_PlanDetail == detail) return m_PlanBlock;
            m_PlanDetail = detail;

            m_Plan = new Drawing();
            var block = m_Plan.Blocks.Add("plan");

            if (m_Snapshot != null)
            {
                bool hasProblem = m_Snapshot.HasProblem || IsStale;
                WatershedGeometry.Build(m_Snapshot, RunoffStyle.GlyphBase * 1.0, 1.5, detail,
                                        hasProblem, delegate (DwgEntity e) { block.Add(e); });
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
            // Снапшот - мировые координаты, посчитанные один раз явной командой.
            // Переносить/поворачивать примитив нечем - пересчёт только по команде.
        }

        protected override void OnAssign(DwgEntity source)
        {
            var other = source as DwgWatershed;
            if (other == null) return;
            m_SurfaceName = other.m_SurfaceName;
            m_DesignSurfaceName = other.m_DesignSurfaceName;
            m_OutletName = other.m_OutletName;
            m_OutletX = other.m_OutletX;
            m_OutletY = other.m_OutletY;
            m_Step = other.m_Step;
            m_Settings = CloneSettings(other.m_Settings);
            m_SourceHash = other.m_SourceHash;
            m_Snapshot = other.m_Snapshot;
            IsStale = other.IsStale;
            InvalidateScheme();
        }

        private static WatershedSettings CloneSettings(WatershedSettings s)
        {
            return new WatershedSettings
            {
                MaxCells = s.MaxCells,
                SnapRadius = s.SnapRadius,
                MinChannelCells = s.MinChannelCells,
                FillEpsilon = s.FillEpsilon,
                HoleFillMaxCells = s.HoleFillMaxCells,
                DeepFillWarn = s.DeepFillWarn,
                AreaTolerancePercent = s.AreaTolerancePercent,
                SimplifyToleranceCells = s.SimplifyToleranceCells
            };
        }

        protected override void OnSaveToStg(StgNode node)
        {
            node.AddString("surface", m_SurfaceName);
            node.AddString("design", m_DesignSurfaceName);
            node.AddString("outletName", m_OutletName);
            node.AddDouble("outletX", m_OutletX);
            node.AddDouble("outletY", m_OutletY);
            node.AddDouble("step", m_Step);
            node.AddString("hash", m_SourceHash);

            node.AddInt32("maxCells", m_Settings.MaxCells);
            node.AddDouble("snapRadius", m_Settings.SnapRadius);
            node.AddInt32("minChannel", m_Settings.MinChannelCells);
            node.AddDouble("fillEps", m_Settings.FillEpsilon);
            node.AddInt32("holeFillMax", m_Settings.HoleFillMaxCells);
            node.AddDouble("deepFillWarn", m_Settings.DeepFillWarn);
            node.AddDouble("areaTolPct", m_Settings.AreaTolerancePercent);
            node.AddDouble("simplifyTolCells", m_Settings.SimplifyToleranceCells);

            if (m_Snapshot != null) SaveSnapshot(node, m_Snapshot);
        }

        protected override void OnLoadFromStg(StgNode node)
        {
            m_SurfaceName = node.GetString("surface", "");
            m_DesignSurfaceName = node.GetString("design", "");
            m_OutletName = node.GetString("outletName", "");
            m_OutletX = node.GetDouble("outletX", 0.0);
            m_OutletY = node.GetDouble("outletY", 0.0);
            m_Step = node.GetDouble("step", 5.0);
            m_SourceHash = node.GetString("hash", "");

            m_Settings = new WatershedSettings
            {
                MaxCells = node.GetInt32("maxCells", 4000000),
                SnapRadius = node.GetDouble("snapRadius", 10.0),
                MinChannelCells = node.GetInt32("minChannel", 50),
                FillEpsilon = node.GetDouble("fillEps", 0.001),
                HoleFillMaxCells = node.GetInt32("holeFillMax", 100),
                DeepFillWarn = node.GetDouble("deepFillWarn", 2.0),
                AreaTolerancePercent = node.GetDouble("areaTolPct", 1.0),
                SimplifyToleranceCells = node.GetDouble("simplifyTolCells", 1.0)
            };

            m_Snapshot = LoadSnapshot(node);
            IsStale = false;
            InvalidateScheme();
        }

        // Снапшот пишется строками "x:y" через ';' внутри кольца/лога, кольца - через
        // '|' - тот же приём, что SaveSamples в DwgRunoffScheme: простой формат
        // не ломается при добавлении полей и не тянет второй тип StgNode-массива.
        private static void SaveSnapshot(StgNode node, WatershedResult r)
        {
            node.AddString("outName", r.OutletName);
            node.AddDouble("reqX", r.RequestedPoint.X);
            node.AddDouble("reqY", r.RequestedPoint.Y);
            node.AddDouble("snapShift", r.SnapShiftM);
            node.AddDouble("area", r.AreaM2);
            node.AddDouble("length", r.LengthM);
            node.AddDouble("endSlope", r.EndSlope);
            node.AddDouble("weightedSlope", r.WeightedSlope);
            node.AddDouble("headZ", r.HeadZ);
            node.AddDouble("outletZ", r.OutletZ);
            node.AddDouble("meanSlope", r.MeanBasinSlope);
            node.AddString("statuses", string.Join("|", r.Statuses.ToArray()));

            var rings = new string[r.Contour.Count];
            for (int i = 0; i < r.Contour.Count; i++) rings[i] = SerializePoints(r.Contour[i]);
            node.AddString("contour", string.Join("~", rings));
            node.AddString("path", SerializePoints(r.Path));

            // Отметки вдоль лога от створа вверх - для профиля лога (LogProfile),
            // чтобы кнопка «Профиль в CSV» работала и после перезагрузки чертежа,
            // не только сразу после расчёта.
            var ci = CultureInfo.InvariantCulture;
            var elevParts = new string[r.PathElevations.Count];
            for (int i = 0; i < r.PathElevations.Count; i++)
                elevParts[i] = r.PathElevations[i].ToString("R", ci);
            node.AddString("pathElev", string.Join(";", elevParts));
        }

        private static WatershedResult LoadSnapshot(StgNode node)
        {
            string outName = node.GetString("outName", null);
            if (outName == null) return null;   // не было расчёта - IsPurged отработает сам

            var r = new WatershedResult
            {
                OutletName = outName,
                RequestedPoint = new Pt(node.GetDouble("reqX", 0.0), node.GetDouble("reqY", 0.0)),
                SnapShiftM = node.GetDouble("snapShift", 0.0),
                AreaM2 = node.GetDouble("area", 0.0),
                LengthM = node.GetDouble("length", 0.0),
                EndSlope = node.GetDouble("endSlope", 0.0),
                WeightedSlope = node.GetDouble("weightedSlope", 0.0),
                HeadZ = node.GetDouble("headZ", 0.0),
                OutletZ = node.GetDouble("outletZ", 0.0),
                MeanBasinSlope = node.GetDouble("meanSlope", 0.0)
            };

            string statuses = node.GetString("statuses", "");
            if (!string.IsNullOrEmpty(statuses))
                r.Statuses.AddRange(statuses.Split('|'));

            string contour = node.GetString("contour", "");
            if (!string.IsNullOrEmpty(contour))
                foreach (var ring in contour.Split('~'))
                    r.Contour.Add(DeserializePoints(ring));

            r.Path.AddRange(DeserializePoints(node.GetString("path", "")));

            string elevRaw = node.GetString("pathElev", "");
            if (!string.IsNullOrEmpty(elevRaw))
            {
                var ci = CultureInfo.InvariantCulture;
                foreach (var chunk in elevRaw.Split(';'))
                {
                    double z;
                    if (double.TryParse(chunk, NumberStyles.Float, ci, out z)) r.PathElevations.Add(z);
                }
            }
            return r;
        }

        private static string SerializePoints(IList<Pt> pts)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.AppendFormat(ci, "{0:R}:{1:R}", pts[i].X, pts[i].Y);
            }
            return sb.ToString();
        }

        private static List<Pt> DeserializePoints(string raw)
        {
            var list = new List<Pt>();
            if (string.IsNullOrEmpty(raw)) return list;
            var ci = CultureInfo.InvariantCulture;
            foreach (var chunk in raw.Split(';'))
            {
                var parts = chunk.Split(':');
                if (parts.Length != 2) continue;
                double x, y;
                if (!double.TryParse(parts[0], NumberStyles.Float, ci, out x)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, ci, out y)) continue;
                list.Add(new Pt(x, y));
            }
            return list;
        }
    }
}
