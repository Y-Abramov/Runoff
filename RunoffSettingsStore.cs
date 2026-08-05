using System;
using System.Globalization;
using System.IO;
using AbrRunoff.Core;

namespace AbrRunoff
{
    /// <summary>Глобальные дефолты. У каждой схемы своя копия - это только стартовые значения.</summary>
    internal static class RunoffSettingsStore
    {
        private static string Path
        {
            get
            {
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Topomatic", "Runoff");
                return System.IO.Path.Combine(dir, "settings.cfg");
            }
        }

        internal static RunoffSettings Load()
        {
            var s = new RunoffSettings();
            try
            {
                if (!File.Exists(Path)) return s;
                foreach (var line in File.ReadAllLines(Path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    double val;
                    if (!double.TryParse(line.Substring(eq + 1).Trim(),
                                         NumberStyles.Float, CultureInfo.InvariantCulture, out val)) continue;
                    switch (key)
                    {
                        case "minGrade":   s.MinGradePermille = val; break;
                        case "plateauEps": s.PlateauEpsPermille = val; break;
                        case "pipeTol":    s.PipeTolerance = val; break;
                        case "sampleStep": s.SampleStep = val; break;
                        case "arrowStep":  s.ArrowStep = val; break;
                    }
                }
            }
            catch { }
            return s;
        }

        internal static void Save(RunoffSettings s)
        {
            try
            {
                var ci = CultureInfo.InvariantCulture;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllLines(Path, new[]
                {
                    "minGrade="   + s.MinGradePermille.ToString(ci),
                    "plateauEps=" + s.PlateauEpsPermille.ToString(ci),
                    "pipeTol="    + s.PipeTolerance.ToString(ci),
                    "sampleStep=" + s.SampleStep.ToString(ci),
                    "arrowStep="  + s.ArrowStep.ToString(ci)
                });
            }
            catch { }
        }
    }
}
