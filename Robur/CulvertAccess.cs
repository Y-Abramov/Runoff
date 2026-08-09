using System;
using System.Collections.Generic;
using System.Reflection;
using Topomatic.ApplicationPlatform.Core;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Водопропускная труба, приведённая к плану: два конца в мировых координатах
    /// и отметки лотков на них.
    /// </summary>
    internal sealed class CulvertRef
    {
        public string Name = "";
        public Vector2D EndA, EndB;
        public double ElevationA, ElevationB;
        public double Diameter;
        public int HoleCount = 1;

        /// <summary>Верхний по отметке конец - оттуда вода входит в трубу.</summary>
        public Vector2D Upper { get { return ElevationA >= ElevationB ? EndA : EndB; } }
        public Vector2D Lower { get { return ElevationA >= ElevationB ? EndB : EndA; } }
        public double UpperZ { get { return Math.Max(ElevationA, ElevationB); } }
        public double LowerZ { get { return Math.Min(ElevationA, ElevationB); } }

        public double Length
        {
            get
            {
                double dx = EndB.X - EndA.X, dy = EndB.Y - EndA.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }
    }

    /// <summary>
    /// Мост: открытые модели `.clv` -> геометрия трубы.
    ///
    /// ПОЧЕМУ ЦЕЛИКОМ НА РЕФЛЕКСИИ. Труба - самостоятельная модель проекта, а не
    /// часть дороги (`RoadAlignment.Pipes` на реальном проекте пуст, проверено
    /// живым прогоном 2026-08-09). Имена типов модели в `Topomatic.Culverts.Core`
    /// ОБФУСЦИРОВАНЫ - на класс не сослаться даже по имени, опознаём модель по
    /// наличию свойства `Culvert`.
    ///
    /// СИСТЕМЫ КООРДИНАТ (разобрано по дампу 2026-08-09, легко ошибиться):
    ///   `Place.StaticCenter`    - мировая точка центра трубы;
    ///   `Place.StaticDirection` - НЕ вектор, а ТОЧКА на единичном расстоянии от
    ///                             центра; направление = StaticDirection - StaticCenter;
    ///   `Prism.*CulvertPosition`- координаты СЕЧЕНИЯ, а не плана:
    ///                             X = смещение вдоль трубы от центра (со знаком),
    ///                             Y = ОТМЕТКА ЛОТКА на этом конце.
    /// Отсюда: мировой конец = StaticCenter + dir * X, отметка = Y.
    /// </summary>
    internal static class CulvertAccess
    {
        internal static List<CulvertRef> GetOpenCulverts()
        {
            var found = new List<CulvertRef>();
            try
            {
                PluginCoreOps.FilterOpenedModels((Predicate<IProjectModel>)delegate (IProjectModel pm)
                {
                    try
                    {
                        object data = pm.LockRead();      // парного UnlockRead в SDK нет
                        object culvert = Get(data, "Culvert");
                        if (culvert == null) return false;

                        string name;
                        try { name = PluginCoreOps.GetFileName(pm); } catch { name = "труба"; }

                        var one = Read(culvert, name);
                        if (one != null) found.Add(one);
                    }
                    catch { }
                    return false;
                });
            }
            catch { }
            return found;
        }

        private static CulvertRef Read(object culvert, string name)
        {
            object place = Get(culvert, "Place");
            object prism = Get(culvert, "Prism");
            if (place == null || prism == null) return null;

            Vector2D center, dirPoint;
            if (!TryVector(Get(place, "StaticCenter"), out center)) return null;
            if (!TryVector(Get(place, "StaticDirection"), out dirPoint)) return null;

            double dx = dirPoint.X - center.X, dy = dirPoint.Y - center.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return null;      // вырожденное направление - трубу не построить
            dx /= len; dy /= len;

            Vector2D left, right;
            if (!TryVector(Get(prism, "LeftCulvertPosition"), out left)) return null;
            if (!TryVector(Get(prism, "RightCulvertPosition"), out right)) return null;

            return new CulvertRef
            {
                Name = name,
                EndA = new Vector2D(center.X + dx * left.X, center.Y + dy * left.X),
                EndB = new Vector2D(center.X + dx * right.X, center.Y + dy * right.X),
                ElevationA = left.Y,          // Y в координатах сечения = отметка лотка
                ElevationB = right.Y,
                Diameter = ToDouble(Get(culvert, "Diameter")),
                HoleCount = Math.Max(1, ToInt(Get(culvert, "HoleCount")))
            };
        }

        /// <summary>Единая точка чтения векторов - см. ловушку в VectorRead.</summary>
        private static bool TryVector(object v, out Vector2D result)
        {
            return VectorRead.TryVector2D(v, out result);
        }

        private static object Get(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                var p = obj.GetType().GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                return p == null ? null : p.GetValue(obj, null);
            }
            catch { return null; }
        }

        private static double ToDouble(object v)
        {
            try { return v == null ? 0.0 : Convert.ToDouble(v); } catch { return 0.0; }
        }

        private static int ToInt(object v)
        {
            try { return v == null ? 0 : Convert.ToInt32(v); } catch { return 0; }
        }
    }
}
