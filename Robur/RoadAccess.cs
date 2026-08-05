using System;
using System.Collections.Generic;
using Topomatic.Alg;
using Topomatic.Alg.Model;
using Topomatic.Alg.Road;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;

namespace AbrRunoff.Robur
{
    /// <summary>Мост: открытые модели проекта -> RoadAlignment.</summary>
    internal static class RoadAccess
    {
        internal sealed class RoadRef
        {
            public IProjectModel Model;
            public RoadAlignment Road;
            public string Name;
        }

        internal static bool TryGetRoad(IProjectModel model, out RoadAlignment road)
        {
            road = null;
            if (model == null) return false;
            try
            {
                object data = model.LockRead();     // парного UnlockRead в SDK нет
                var am = data as AlignmentModel;
                Alignment al = am == null ? null : am.Alignment;
                road = al as RoadAlignment;
            }
            catch { }
            return road != null;
        }

        /// <summary>Все открытые дорожные модели проекта.</summary>
        internal static List<RoadRef> GetOpenRoads()
        {
            var found = new List<RoadRef>();
            try
            {
                PluginCoreOps.FilterOpenedModels((Predicate<IProjectModel>)delegate (IProjectModel pm)
                {
                    RoadAlignment road;
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
