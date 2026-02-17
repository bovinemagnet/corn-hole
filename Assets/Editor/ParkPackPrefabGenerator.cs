using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Fusion;
using CornHole;

namespace CornHole.Editor
{
    /// <summary>
    /// Generates network-spawnable prefabs from Park Pack GLB models.
    /// Reads metadata JSON for component values and creates one prefab per variant.
    /// Run via menu: Tools > Corn Hole > Generate Park Pack Prefabs.
    /// </summary>
    public static class ParkPackPrefabGenerator
    {
        private const string ModelsPath = "Assets/Models/ParkPack";
        private const string PrefabsPath = "Assets/Prefabs/ParkPack";
        private const string MetadataFile = "park_props_metadata.json";

        [MenuItem("Tools/Corn Hole/Generate Park Pack Prefabs")]
        public static void GeneratePrefabs()
        {
            string metadataPath = Path.Combine(ModelsPath, MetadataFile);
            var metadataAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(metadataPath);
            if (metadataAsset == null)
            {
                Debug.LogError(
                    $"Park Pack metadata not found at {metadataPath}. " +
                    "Ensure GLB files and JSON are in Assets/Models/ParkPack/.");
                return;
            }

            var metadata = JsonUtility.FromJson<ParkPackMetadata>(metadataAsset.text);
            if (metadata.props == null || metadata.props.Length == 0)
            {
                Debug.LogError("No props found in metadata JSON.");
                return;
            }

            // Ensure output folder exists
            EnsureFolder(PrefabsPath);

            int created = 0;
            int skipped = 0;
            int failed = 0;
            var createdPrefabs = new List<GameObject>();

            foreach (var prop in metadata.props)
            {
                foreach (var variant in prop.variants)
                {
                    string fileName = Path.GetFileNameWithoutExtension(variant.file);
                    string prefabPath = $"{PrefabsPath}/{fileName}.prefab";

                    // Skip if prefab already exists
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                    {
                        skipped++;
                        continue;
                    }

                    // Find the imported model
                    string modelPath = $"{ModelsPath}/{variant.file}";
                    var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    if (modelAsset == null)
                    {
                        Debug.LogWarning($"Model not found at {modelPath} — skipping.");
                        failed++;
                        continue;
                    }

                    // Instantiate the model
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                    instance.name = fileName;

                    // Add NetworkObject
                    if (instance.GetComponent<NetworkObject>() == null)
                        instance.AddComponent<NetworkObject>();

                    // Add Rigidbody
                    var rb = instance.GetComponent<Rigidbody>();
                    if (rb == null)
                        rb = instance.AddComponent<Rigidbody>();
                    rb.useGravity = true;
                    rb.isKinematic = false;

                    // Add BoxCollider sized from renderer bounds
                    if (instance.GetComponent<Collider>() == null)
                    {
                        var renderers = instance.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            var bounds = renderers[0].bounds;
                            for (int i = 1; i < renderers.Length; i++)
                                bounds.Encapsulate(renderers[i].bounds);

                            var boxCollider = instance.AddComponent<BoxCollider>();
                            // Convert world-space bounds to local space
                            boxCollider.center = instance.transform.InverseTransformPoint(bounds.center);
                            boxCollider.size = instance.transform.InverseTransformVector(bounds.size);
                            // Ensure positive size values
                            boxCollider.size = new Vector3(
                                Mathf.Abs(boxCollider.size.x),
                                Mathf.Abs(boxCollider.size.y),
                                Mathf.Abs(boxCollider.size.z));
                        }
                        else
                        {
                            // Fallback: unit-size collider
                            instance.AddComponent<BoxCollider>();
                        }
                    }

                    // Add ConsumableObject and set values via SerializedObject
                    var consumable = instance.GetComponent<ConsumableObject>();
                    if (consumable == null)
                        consumable = instance.AddComponent<ConsumableObject>();

                    var so = new SerializedObject(consumable);
                    so.FindProperty("objectSize").floatValue = prop.requiredRadius;
                    so.FindProperty("pointValue").intValue = prop.scoreValue;
                    so.FindProperty("sizeValue").floatValue = prop.areaValue;
                    so.FindProperty("rb").objectReferenceValue = rb;
                    so.ApplyModifiedProperties();

                    // Save as prefab
                    var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    UnityEngine.Object.DestroyImmediate(instance);

                    if (prefab != null)
                    {
                        createdPrefabs.Add(prefab);
                        created++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== Park Pack Prefab Generation Complete ===");
            Debug.Log($"  Created: {created}  |  Skipped (existing): {skipped}  |  Failed: {failed}");
            Debug.Log($"  Output: {PrefabsPath}/");

            if (created > 0)
            {
                Debug.Log("Next step: assign generated prefabs to ObjectSpawner.consumablePrefabs in the Inspector.");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            // Build folder tree from root
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        // ──────────────────────────────────────────────
        //  JSON data classes (matches park_props_metadata.json)
        // ──────────────────────────────────────────────

        [Serializable]
        private class ParkPackMetadata
        {
            public PropData[] props;
        }

        [Serializable]
        private class PropData
        {
            public string name;
            public string tier;
            public float requiredRadius;
            public float areaValue;
            public int scoreValue;
            public VariantData[] variants;
        }

        [Serializable]
        private class VariantData
        {
            public int variantIndex;
            public long seed;
            public string file;
        }
    }
}
