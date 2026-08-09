using System.Collections.Generic;

namespace AbrRunoff.Core
{
    /// <summary>
    /// Отсекает кюветы, которых физически нет.
    ///
    /// ПОЧЕМУ ЭТО НУЖНО. Проектировщик СПЕЦИАЛЬНО задирает продольный профиль
    /// кювета выше рельефа на участках, где кювет строить не надо: при отметке
    /// дна выше земли конструкция не возводится. Для Robur это законный приём,
    /// а для анализа стока - ловушка: модуль видел горб отметок и объявлял его
    /// водоразделом, которого в природе не существует.
    ///
    /// Правило: кювет есть там, где дно ВРЕЗАНО в землю хотя бы на MinDepth.
    /// Всё остальное - разрыв, и `FlowDetector` уже умеет резать по нему ряд на
    /// отдельные канавы (сток через разрыв не идёт). Отдельной сущности не
    /// понадобилось - хватило честного значения флага `IsDitch`.
    ///
    /// Если отметка земли неизвестна (`HasGround = false`), НИЧЕГО не трогаем:
    /// на проектах без этих данных схема иначе опустела бы целиком.
    /// </summary>
    public static class DitchExistence
    {
        /// <param name="minDepth">
        /// Минимальная врезка дна в землю, м. Ниже неё канавы нет: нулевая и
        /// околонулевая глубина - это линия по земле, а не водоотвод.
        /// </param>
        /// <returns>Сколько отсчётов признано «не кюветом» - вызывающий может предупредить.</returns>
        public static int Apply(IList<DitchSample> samples, double minDepth)
        {
            if (samples == null) return 0;
            if (minDepth < 0.0) minDepth = 0.0;

            int dropped = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                if (!s.IsDitch || !s.HasGround) continue;

                double depth = s.GroundZ - s.BottomZ;
                if (depth >= minDepth) continue;

                s.IsDitch = false;          // DitchSample - структура, пишем обратно
                samples[i] = s;
                dropped++;
            }
            return dropped;
        }

        /// <summary>Сколько отсчётов остались кюветом - для проверки «не вычеркнули ли всё».</summary>
        public static int CountDitch(IList<DitchSample> samples)
        {
            if (samples == null) return 0;
            int n = 0;
            foreach (var s in samples) if (s.IsDitch) n++;
            return n;
        }
    }
}
