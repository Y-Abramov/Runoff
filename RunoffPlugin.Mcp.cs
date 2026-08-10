using System.Collections.Generic;
using AbrRunoff.Mcp;
using Topomatic.ApplicationPlatform.Plugins;
using Topomatic.ToolBridge;

namespace AbrRunoff
{
    public partial class RunoffPlugin
    {
        /// <summary>Хендлер broadcast "tool_request" от Topomatic.ToolBridge.
        /// args[0] - List&lt;ToolProvider&gt;, куда плагины складывают свои провайдеры.
        /// Имя "generate_tools" занято собственным broadcast-хендлером вендора внутри
        /// Topomatic.ToolBridge.dll - дубль ломает инициализацию моста целиком
        /// (Log.log: "Dublicated function 'generate_tools'", инцидент 2026-07-27 на PavePlan).
        /// Мост читает целевое имя из broadcasts каждого .plugin отдельно, совпадение
        /// с вендорским именем не требуется.</summary>
        [cmd("runoff_generate_tools")]
        private void GenerateTools(object[] args)
        {
            if (args == null || args.Length == 0) return;
            var providers = args[0] as List<ToolProvider>;
            if (providers == null) return;
            providers.Add(new RunoffTools());
        }
    }
}
