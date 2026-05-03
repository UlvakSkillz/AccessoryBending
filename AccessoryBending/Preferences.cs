using MelonLoader;

namespace AccessoryBending
{
	public class Preferences
	{
		private const string CONFIG_FILE = "config.cfg";
		private const string USER_DATA = "UserData/AccessoryBending/";
        internal static Dictionary<MelonPreferences_Entry, object> LastSavedValues = new();

        internal static MelonPreferences_Category AccessoryBendingCategory;
		internal static MelonPreferences_Entry<bool> PrefShowOthers;
		internal static MelonPreferences_Entry<bool> PrefDebugging;
        internal static MelonPreferences_Category AccessoriesCategory;
		internal static List<MelonPreferences_Entry<bool>> PrefAccessoriesEnabled;

        internal static void InitGlobalPrefs()
		{
			if (!Directory.Exists(USER_DATA)) { Directory.CreateDirectory(USER_DATA); }

			AccessoryBendingCategory = MelonPreferences.CreateCategory("AccessoryBending", "Toggles");
			AccessoryBendingCategory.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));

            PrefShowOthers = AccessoryBendingCategory.CreateEntry("ShowOthers", true, "Show Others Accessories", "Toggling ON will have others Accessories Shown that you have installed. (Will not Remove Accessories Already Loaded)");

            UIFramework.UI.CreateButtonEntry(
                category: AccessoryBendingCategory,
                buttonText: "Nuke",
                displayName: "Nuke Others Accessories",
                description: "Click to Remove all current Accessories from Players in the Scene. (Accessories will Come Back when a Player Enters the Room)",
                handler: () => Main.NukeOthersAccessories());

            PrefDebugging = AccessoryBendingCategory.CreateEntry("Debugging", false, "Debugging", "Toggling ON will print extensive diagnostic messages to the MelonLoader console.");
		}

		internal static void InitAccessoryPrefs()
        {
            AccessoriesCategory = MelonPreferences.CreateCategory("Accessories", "Accessories");
            AccessoriesCategory.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));

            if (PrefAccessoriesEnabled != null)
            {
                foreach (MelonPreferences_Entry<bool> entry in PrefAccessoriesEnabled)
                {
                    AccessoriesCategory.DeleteEntry(entry.Identifier);
                }
            }
            PrefAccessoriesEnabled = new List<MelonPreferences_Entry<bool>>();
            foreach (AssetInfo info in Main.assetInfos)
            {
                PrefAccessoriesEnabled.Add(AccessoriesCategory.CreateEntry(info.GetAssetToUse().name, false, info.GetAssetToUse().name, "Toggling ON will have this Accessory Shown."));
            }
        }

		internal static void StoreLastSavedPrefs()
		{
			List<MelonPreferences_Entry> prefs = new();
			prefs.AddRange(AccessoryBendingCategory.Entries);
			prefs.AddRange(AccessoriesCategory.Entries);
			foreach (MelonPreferences_Entry entry in  prefs) { LastSavedValues[entry] = entry.BoxedValue; }
		}

		public static bool AnyPrefsChanged()
		{
			foreach (KeyValuePair<MelonPreferences_Entry, object> pair in LastSavedValues)
			{
				if (!pair.Key.BoxedValue.Equals(pair.Value)) { return true; }
			}
			return false;
		}

        public static bool IsPrefChanged(MelonPreferences_Entry entry)
		{
			if (LastSavedValues.TryGetValue(entry, out object lastValue)) { return !entry.BoxedValue.Equals(lastValue); }
			return false;
		}
    }
}