using System;
using System.Collections.Generic;
using System.IO;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Cad.Foundation;
using Topomatic.Dtm;
using Topomatic.Sfc;

namespace AbrRunoff.Robur
{
    internal sealed class SurfaceRef
    {
        public string Name = "";
        public bool IsDesigned;
        public BoundingBox2D Bounds;
        public int TriangleCount;
        public Surface Surface;
    }

    internal static class SurfaceAccess
    {
        /// <summary>
        /// Поверхности проекта. Обход дерева файлов, а не FilterModels: последний
        /// видит только открытые модели сессии, а холодная модель после перезапуска
        /// Robur ему неизвестна. GetChilds() может кинуть NullReferenceException
        /// вместо null - та же ловушка, что документирована в DemLoader/TerrainWriter.
        /// </summary>
        internal static List<SurfaceRef> GetSurfaces()
        {
            var list = new List<SurfaceRef>();
            var root = FindProjectRoot();
            if (root == null) return list;

            Walk(root, list);
            return list;
        }

        /// <summary>Корень проекта: любая открытая модель знает свой ModelProject, а тот - корневой узел.</summary>
        private static IProjectModel FindProjectRoot()
        {
            IProjectModel root = null;
            PluginCoreOps.FilterModels((Predicate<IProjectModel>)delegate (IProjectModel pm)
            {
                if (root == null && pm != null && pm.Project != null) root = pm.Project.Model;
                return false;
            });
            return root;
        }

        private static void Walk(IProjectModel node, List<SurfaceRef> acc)
        {
            IProjectModel[] childs;
            try { childs = node.GetChilds(); }
            catch (NullReferenceException) { return; }
            if (childs == null) return;

            foreach (var child in childs)
            {
                if (child == null) continue;
                TryAdd(child, acc);
                Walk(child, acc);
            }
        }

        private static void TryAdd(IProjectModel node, List<SurfaceRef> acc)
        {
            try
            {
                node.LockRead();
                var terrain = node.Model as TerrainModel;
                if (terrain == null) return;

                var surface = terrain.Surface;
                if (surface == null) return;

                acc.Add(new SurfaceRef
                {
                    Name = DisplayName(node),
                    IsDesigned = surface.Designed,
                    Bounds = surface.Bounds2d,
                    TriangleCount = surface.Triangles.Count,
                    Surface = surface
                });
            }
            catch (Exception)
            {
                // Модель нечитаема (битый файл, чужой формат) - не наша забота
                // и не повод валить перечисление остальных.
            }
        }

        /// <summary>
        /// IProjectModel не отдаёт имя напрямую - только путь файла. Тот же приём,
        /// что в DemLoader/ModelPickDialog.
        /// </summary>
        private static string DisplayName(IProjectModel node)
        {
            string fileName = PluginCoreOps.GetFileName(node);
            return string.IsNullOrEmpty(fileName) ? "(без имени)" : Path.GetFileNameWithoutExtension(fileName);
        }
    }
}
