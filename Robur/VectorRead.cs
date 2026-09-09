using System;
using System.Reflection;
using Topomatic.Cad.Foundation;

namespace AbrRunoff.Robur
{
    /// <summary>
    /// Чтение векторов Robur из объектов, полученных рефлексией.
    ///
    /// ⚠ ЛОВУШКА, СТОИВШАЯ ДВУХ БАГОВ (2026-08-09). У `Vector2D` и `Vector3D`
    /// координаты - это ПОЛЯ (`X`, `Y`, `Z`), а НЕ свойства. `GetProperty("X")`
    /// молча возвращает null, вектор выходит нулевым, и дальше:
    ///   - трубы теряли направление и пропадали из сети целиком;
    ///   - все структурные линии Площадки давали длину 0.0 м.
    /// Оба раза дамп выглядел здоровым, потому что печатался `ToString()`.
    ///
    /// Поэтому: сначала приведение ТИПОМ (векторы доступны напрямую), и только
    /// если пришёл чужой тип - рефлексия ПО ПОЛЯМ. Одно место на весь модуль,
    /// чтобы ловушка не сработала в третий раз.
    /// </summary>
    internal static class VectorRead
    {
        internal static bool TryVector2D(object v, out Vector2D result)
        {
            if (v is Vector2D) { result = (Vector2D)v; return true; }

            Vector3D v3;
            if (TryVector3D(v, out v3)) { result = new Vector2D(v3.X, v3.Y); return true; }

            result = new Vector2D(0.0, 0.0);
            return false;
        }

        internal static bool TryVector3D(object v, out Vector3D result)
        {
            if (v is Vector3D) { result = (Vector3D)v; return true; }

            if (v is Vector2D)
            {
                var v2 = (Vector2D)v;
                result = new Vector3D(v2.X, v2.Y, 0.0);
                return true;
            }

            // Чужой тип точки - читаем ПОЛЯ, не свойства.
            double x, y, z;
            if (v != null && TryField(v, "X", out x) && TryField(v, "Y", out y))
            {
                TryField(v, "Z", out z);
                result = new Vector3D(x, y, z);
                return true;
            }

            result = new Vector3D(0.0, 0.0, 0.0);
            return false;
        }

        /// <summary>
        /// Координаты Vector3D, полученного уже типизированным (не рефлексией) - например,
        /// SurfacePoint.Vertex. X/Y/Z у Vector3D - поля, читаем напрямую без похода в TryField.
        /// </summary>
        internal static double X(Vector3D v) { return v.X; }
        internal static double Y(Vector3D v) { return v.Y; }
        internal static double Z(Vector3D v) { return v.Z; }

        private static bool TryField(object obj, string name, out double value)
        {
            value = 0.0;
            try
            {
                var t = obj.GetType();
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (f != null) { value = Convert.ToDouble(f.GetValue(obj)); return true; }

                // На всякий случай и свойство - вдруг у чужого типа оно всё же есть.
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p != null) { value = Convert.ToDouble(p.GetValue(obj, null)); return true; }
            }
            catch { }
            return false;
        }
    }
}
