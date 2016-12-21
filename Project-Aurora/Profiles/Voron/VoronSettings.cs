using System.Collections.Generic;
using Aurora.Settings;

namespace Aurora.Profiles.Voron
{
	public class VoronSettings : ProfileSettings
	{
		//Effects
		//// Lighting Areas
		public List<ColorZone> lighting_areas { get; set; }
		public bool disable_cz_on_dark;

		public VoronSettings()
		{
			//General
			isEnabled = true;

			//Effects
			//// Lighting Areas
			lighting_areas = new List<ColorZone>() {
				new ColorZone(new Devices.DeviceKeys[] { Devices.DeviceKeys.Q, Devices.DeviceKeys.W, Devices.DeviceKeys.E, Devices.DeviceKeys.R }, System.Drawing.Color.Blue, "Abilities")
			};
			disable_cz_on_dark = true;
		}
	}
}
