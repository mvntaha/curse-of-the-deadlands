using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadlands.Core.Input
{
    /// <summary>Persists rebinding overrides for an InputActionAsset so remapped keys survive restarts.</summary>
    public static class InputBindingStore
    {
        const string KeyPrefix = "input.bindings.";

        public static void Load(InputActionAsset asset)
        {
            string json = PlayerPrefs.GetString(KeyPrefix + asset.name, string.Empty);
            if (!string.IsNullOrEmpty(json))
                asset.LoadBindingOverridesFromJson(json);
        }

        public static void Save(InputActionAsset asset)
        {
            PlayerPrefs.SetString(KeyPrefix + asset.name, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetAll(InputActionAsset asset)
        {
            asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(KeyPrefix + asset.name);
        }
    }
}
