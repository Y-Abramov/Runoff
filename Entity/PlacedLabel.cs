namespace AbrRunoff.Entity
{
    /// <summary>
    /// Подпись, реально нарисованная в последнем построении плана: где стоит текст
    /// и от какой точки он отмерян. Контроллер строит по этому списку грипы, а
    /// обработчик грипа считает смещение как (новая точка - Anchor).
    ///
    /// Заполняется только на детальном уровне (detail = true): на дальнем зуме
    /// подписей нет, и грипов быть не должно.
    /// </summary>
    internal struct PlacedLabel
    {
        /// <summary>Устойчивый ключ подписи, см. Core.Labels.LabelKeys.</summary>
        public string Key;

        /// <summary>Точка привязки - куда подпись встала бы без смещений.</summary>
        public double AnchorX, AnchorY;

        /// <summary>Где текст стоит сейчас - сюда садится грип.</summary>
        public double X, Y;
    }
}
