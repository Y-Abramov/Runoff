using System;
using Topomatic.ApplicationPlatform.Plugins;

namespace AbrRunoff
{
    public class PluginHost : PluginHostInitializator
    {
        protected override Type[] GetTypes()
        {
            return new[] { typeof(RunoffPlugin) };
        }

        public override void Initialize(PluginFactory factory)
        {
            base.Initialize(factory);

            // Без активатора объект молча выпадет при загрузке проекта.
            // Регистрируем по замороженному ENTITY_NAME, а не по имени типа -
            // тогда ренейм класса ничего не сломает.
            try
            {
                Topomatic.Dwg.Drawing.RegisterActivator(
                    Entity.DwgRunoffScheme.ENTITY_NAME,
                    delegate { return new Entity.DwgRunoffScheme(); });

                Topomatic.Dwg.Drawing.RegisterActivator(
                    Entity.DwgDrainageNetwork.ENTITY_NAME,
                    delegate { return new Entity.DwgDrainageNetwork(); });

                Topomatic.Dwg.Drawing.RegisterActivator(
                    Entity.DwgWatershed.ENTITY_NAME,
                    delegate { return new Entity.DwgWatershed(); });
            }
            catch { }
        }
    }
}
