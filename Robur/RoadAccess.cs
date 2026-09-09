using System;
using System.Collections.Generic;
using Topomatic.Alg;
using Topomatic.Alg.Model;
using Topomatic.Alg.Road;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Мост: открытые модели проекта -> Alignment (трасса).
    ///
    /// Берём БАЗОВЫЙ Alignment, а не RoadAlignment: железнодорожная трасса
    /// (RailAlignment) - такой же его наследник, а всё, что читает схема стока
    /// (Plan, Corridor, Parameters, Pipes, StartStation), объявлено на базовом
    /// классе. Ссылку на Topomatic.Alg.Rail СОЗНАТЕЛЬНО не добавляем: сборки
    /// нет на продуктах без ЖД, а недостающая ссылка роняет загрузку всего
    /// модуля (FileLoadException, см. корневой CLAUDE.md). Тип пути нужен
    /// только для подписи в списке - его определяем по имени типа.
    /// </summary>
    internal static class RoadAccess
    {
        internal sealed class RoadRef
        {
            public IProjectModel Model;

            /// <summary>Трасса: автодорога (RoadAlignment) либо ЖД путь (RailAlignment).</summary>
            public Alignment Road;

            public string Name;

            /// <summary>Ложь для ЖД пути и прочих трасс - у них нет дорожной специфики (лотки, Urb).</summary>
            public bool IsRoad { get { return Road is RoadAlignment; } }

            /// <summary>Подпись вида пути для списков выбора.</summary>
            public string KindTitle
            {
                get
                {
                    if (IsRoad) return "автодорога";
                    string type = Road == null ? "" : Road.GetType().Name;
                    return type.IndexOf("Rail", StringComparison.OrdinalIgnoreCase) >= 0 ? "ЖД путь" : "трасса";
                }
            }
        }

        internal static bool TryGetRoad(IProjectModel model, out Alignment road)
        {
            road = null;
            if (model == null) return false;
            try
            {
                object data = model.LockRead();     // парного UnlockRead в SDK нет
                var am = data as AlignmentModel;
                road = am == null ? null : am.Alignment;
            }
            catch { }
            return road != null;
        }

        /// <summary>Все открытые трассы проекта: автодороги и ЖД пути.</summary>
        internal static List<RoadRef> GetOpenRoads()
        {
            var found = new List<RoadRef>();
            try
            {
                PluginCoreOps.FilterOpenedModels((Predicate<IProjectModel>)delegate (IProjectModel pm)
                {
                    Alignment road;
                    if (TryGetRoad(pm, out road))
                    {
                        string name;
                        try { name = PluginCoreOps.GetFileName(pm); }
                        catch { name = "(без имени)"; }
                        found.Add(new RoadRef { Model = pm, Road = road, Name = name });
                    }
                    return false;
                });
            }
            catch { }
            return found;
        }
    }
}
