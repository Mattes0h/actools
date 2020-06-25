﻿using AcManager.Tools.Objects;
using AcTools.DataFile;
using JetBrains.Annotations;

namespace AcManager.ContentRepair.Repairs {
    [UsedImplicitly]
    public class CarIniVersionRepair : CarSimpleRepairBase {
        protected override void Fix(CarObject car, DataWrapper data) {
            var ini = data.GetIniFile("car.ini");
            var section = ini["HEADER"];
            section.Set("VERSION", 2);
            ini.Save();
        }

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            var ini = data.GetIniFile("car.ini");
            var section = ini["HEADER"];
            var versionString = section.GetNonEmpty("VERSION");

            if (int.TryParse(versionString, out var version) && version > 0 && version < 10
                    || versionString?.StartsWith("extended-") == true) return null;

            return new ContentObsoleteSuggestion("Invalid [mono]HEADER/VERSION[/mono] parameter",
                    "A bit misleading, but this parameter is not to set mod’s version, but to specify used set of params.",
                    (p, c) => FixAsync(car, p, c)) {
                        AffectsData = true
                    };
        }
    }
}