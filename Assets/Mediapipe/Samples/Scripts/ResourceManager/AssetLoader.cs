using Mediapipe;
using UnityEngine;

namespace Mediapipe.Unity {
  public static class AssetLoader {
    static ResourceManager resourceManager;

    public static void Provide(ResourceManager manager) {
      resourceManager = manager;
    }

    public static void PrepareAsset(string name, string uniqueKey, bool overwrite = false) {
      if (resourceManager == null) {
        Debug.LogWarning("ResourceManager is not provided, so default LocalAssetManager will be used");
        resourceManager = new LocalAssetManager();
      }
      resourceManager.PrepareAsset(name, uniqueKey, overwrite);
    }

    public static void PrepareAsset(string name, bool overwrite = false) {
      PrepareAsset(name, name, overwrite);
    }
  }
}
