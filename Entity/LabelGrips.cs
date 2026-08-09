using System;
using System.Collections;
using System.Collections.Generic;
using Topomatic.Cad.Foundation;
using Topomatic.Cad.View;
using Topomatic.Dwg.Entities;
using Topomatic.Dwg.Layer;

namespace AbrRunoff.Entity
{
    /// <summary>
    /// Грипы подписей - общие для схемы стока и сети водоотвода: обе рисуют
    /// подписи одним кодом, значит и таскаются они одинаково.
    ///
    /// Сам примитив за грипы не тянется (его геометрия целиком определяется
    /// дорогой/источниками) - грипы двигают ТОЛЬКО подписи. Смещение считается
    /// от точки привязки, а не от текущего места: иначе повторный оттаск
    /// складывался бы сам с собой.
    /// </summary>
    internal static class LabelGrips
    {
        /// <param name="labels">Подписи последнего детального построения.</param>
        /// <param name="set">Запомнить смещение подписи (ключ, dx, dy).</param>
        /// <param name="reset">Вернуть подпись на автоматическое место (ключ).</param>
        internal static IEnumerable Build(DwgEntity entity, object cadview,
                                          IList<PlacedLabel> labels,
                                          Action<string, double, double> set,
                                          Action<string> reset)
        {
            var view = cadview as CadView;
            if (view == null || labels == null) yield break;

            foreach (var label in labels)
            {
                // Копия в локальную переменную обязательна: замыкание ниже иначе
                // захватило бы переменную цикла и все грипы двигали бы последнюю подпись.
                var item = label;
                if (string.IsNullOrEmpty(item.Key)) continue;

                var grip = new EntityGrip(view, entity);
                grip.GripType = GripType.Rectangular;
                grip.Location = new Vector3D(item.X, item.Y, 0.0);

                grip.Move += delegate (Vector3D target)
                {
                    set(item.Key, target.X - item.AnchorX, target.Y - item.AnchorY);
                };

                // Обратный ход: вернуть подпись туда, куда её ставит автомат.
                var back = new ClickGrip();
                back.Click += delegate { reset(item.Key); };
                grip.AddGrip("Вернуть подпись на место", "runoff_label_reset", back);

                yield return grip;
            }
        }
    }
}
