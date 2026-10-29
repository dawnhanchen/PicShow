using System;
using System.Linq;
using ByteDance.PICO.Interaction;
using ByteDance.PICO.XR;
using PicShow.PicoInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace PicShow.Editor
{
    public static class GalleryInteractionSceneMigrator
    {
        private const string SampleRigPath =
            "Assets/Samples/PICO XR/6.1.1/PICO Interaction Demo/Prefabs/XR Origin (VR).prefab";
        private const string LegacyRigName = "[PICO_MCP] XR Origin (XR Rig)";
        private const string RigName = "PICO Interaction XR Origin";
        private const float LegacyCameraYOffset = 1.36f;

        [MenuItem("PicShow/Migrate Scene To PICO Interaction Rig")]
        public static void MigrateActiveScene()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before migrating the XR rig.");

            Scene scene = SceneManager.GetActiveScene();
            GameObject legacyRig = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == LegacyRigName);
            XROrigin interactionOrigin = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<XROrigin>(true))
                .FirstOrDefault(origin =>
                    origin.GetComponentInChildren<PXR_InteractorManager>(true) != null);

            if (interactionOrigin == null)
            {
                GameObject sampleRig = AssetDatabase.LoadAssetAtPath<GameObject>(SampleRigPath);
                if (sampleRig == null)
                    throw new InvalidOperationException("PICO Interaction Demo rig is not imported.");

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(sampleRig, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Create PICO Interaction XR Origin");
                interactionOrigin = instance.GetComponent<XROrigin>();
            }

            GameObject targetRig = interactionOrigin.gameObject;
            Undo.RecordObject(targetRig, "Configure PICO Interaction XR Origin");
            targetRig.name = RigName;

            if (legacyRig != null)
            {
                targetRig.transform.SetPositionAndRotation(
                    legacyRig.transform.position,
                    legacyRig.transform.rotation);
                targetRig.transform.localScale = legacyRig.transform.localScale;
                MigratePicoRuntime(legacyRig, targetRig);
                MigrateSceneChildren(legacyRig, targetRig);
            }

            ConfigureInteractionRig(targetRig);
            ConfigureProjectCapabilities();

            Camera targetCamera = interactionOrigin.Camera;
            foreach (Canvas canvas in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
            {
                if (canvas.renderMode != RenderMode.WorldSpace)
                    continue;
                Undo.RecordObject(canvas, "Rebind world-space canvas camera");
                canvas.worldCamera = targetCamera;
            }

            ValidateBeforeLegacyRemoval(scene, interactionOrigin, legacyRig != null);
            if (legacyRig != null && legacyRig != targetRig)
                Undo.DestroyObjectImmediate(legacyRig);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void MigratePicoRuntime(GameObject sourceRig, GameObject targetRig)
        {
            var sourceManager = sourceRig.GetComponent<PXR_Manager>();
            var targetManager = targetRig.GetComponent<PXR_Manager>() ??
                Undo.AddComponent<PXR_Manager>(targetRig);
            if (sourceManager != null)
                EditorUtility.CopySerialized(sourceManager, targetManager);
            SetBoolean(targetManager, "eyeTracking", true);

            Camera sourceCamera = sourceRig.GetComponentInChildren<Camera>(true);
            Camera targetCamera = targetRig.GetComponentInChildren<Camera>(true);
            if (sourceCamera == null || targetCamera == null)
                throw new InvalidOperationException("Both XR rigs must contain a camera.");

            var sourceEffect = sourceCamera.GetComponent<PXR_CameraEffectBlock>();
            var targetEffect = targetCamera.GetComponent<PXR_CameraEffectBlock>() ??
                Undo.AddComponent<PXR_CameraEffectBlock>(targetCamera.gameObject);
            if (sourceEffect != null)
                EditorUtility.CopySerialized(sourceEffect, targetEffect);
        }

        private static void MigrateSceneChildren(GameObject sourceRig, GameObject targetRig)
        {
            MoveChild(sourceRig.transform, targetRig.transform, "[PICO_MCP] VST Marker");
            MoveChild(sourceRig.transform, targetRig.transform, "[PICO_MCP] Plane");

            Transform sourceOffset = sourceRig.transform.Find("Camera Offset");
            Transform targetOffset = targetRig.transform.Find("Camera Offset");
            if (sourceOffset == null || targetOffset == null)
                throw new InvalidOperationException("Both XR rigs must contain Camera Offset.");
            Undo.RecordObject(targetOffset, "Migrate XR camera offset");
            targetOffset.localPosition = sourceOffset.localPosition;
            targetOffset.localRotation = sourceOffset.localRotation;
            targetOffset.localScale = sourceOffset.localScale;

            MoveChild(
                sourceOffset,
                targetOffset.Find("Left Controller"),
                "Left Controller/[PICO_MCP] Left Controller Model");
            MoveChild(
                sourceOffset,
                targetOffset.Find("Right Controller"),
                "Right Controller/[PICO_MCP] Right Controller Model");

            SetGenericControllerVisual(targetOffset.Find("Left Controller/Left Controller Visual"), false);
            SetGenericControllerVisual(targetOffset.Find("Right Controller/Right Controller Visual"), false);
        }

        private static void ConfigureInteractionRig(GameObject targetRig)
        {
            Camera camera = targetRig.GetComponentInChildren<Camera>(true);
            if (camera == null)
                throw new InvalidOperationException("The PICO Interaction rig has no camera.");
            XROrigin origin = targetRig.GetComponent<XROrigin>();
            Transform cameraOffset = origin.CameraFloorOffsetObject.transform;
            if (Mathf.Approximately(cameraOffset.localPosition.y, 0f))
            {
                Undo.RecordObject(cameraOffset, "Restore gallery camera height");
                cameraOffset.localPosition = new Vector3(
                    cameraOffset.localPosition.x,
                    LegacyCameraYOffset,
                    cameraOffset.localPosition.z);
            }
            camera.tag = "MainCamera";
            camera.enabled = true;
            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = true;

            var manager = targetRig.GetComponentInChildren<PXR_InteractorManager>(true);
            if (manager == null)
                throw new InvalidOperationException("The Sample rig has no PXR_InteractorManager.");
            if (targetRig.GetComponentsInChildren<PXR_PinchInteractor>(true).Length != 2)
                throw new InvalidOperationException("The Sample rig must contain two pinch interactors.");
            var aimInteractor = targetRig.GetComponentInChildren<PXR_AimInteractor>(true);
            if (aimInteractor == null)
                throw new InvalidOperationException("The Sample rig has no eye-gaze aim interactor.");

            var eyeModeAdapter = targetRig.GetComponent<GalleryEyeInteractionModeAdapter>() ??
                Undo.AddComponent<GalleryEyeInteractionModeAdapter>(targetRig);
            eyeModeAdapter.Configure(manager, aimInteractor);
        }

        private static void ConfigureProjectCapabilities()
        {
            PXR_ProjectSetting settings = PXR_ProjectSetting.GetProjectConfig();
            settings.handTracking = true;
            settings.handTrackingSupportType = HandTrackingSupport.ControllersAndHands;
            settings.eyeTracking = true;
            settings.eyetrackingCalibration = true;
            PXR_ProjectSetting.SaveAssets();
        }

        private static void ValidateBeforeLegacyRemoval(
            Scene scene,
            XROrigin expectedOrigin,
            bool requirePicoCapabilities)
        {
            var origins = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<XROrigin>(true))
                .Where(origin => origin.gameObject.name != LegacyRigName)
                .ToArray();
            if (origins.Length != 1 || origins[0] != expectedOrigin)
                throw new InvalidOperationException("The migrated scene must contain one target XR Origin.");

            if (requirePicoCapabilities)
            {
                Camera camera = expectedOrigin.Camera;
                if (camera == null || camera.GetComponent<PXR_CameraEffectBlock>() == null)
                    throw new InvalidOperationException("The target camera did not preserve PICO VST support.");
                if (expectedOrigin.transform.Find("[PICO_MCP] VST Marker") == null)
                    throw new InvalidOperationException("The VST marker was not migrated.");
                if (expectedOrigin.transform.Find("[PICO_MCP] Plane") == null)
                    throw new InvalidOperationException("Plane detection was not migrated.");
            }
        }

        private static void MoveChild(Transform source, Transform target, string sourcePath)
        {
            if (source == null || target == null)
                return;
            Transform child = source.Find(sourcePath);
            if (child == null)
                return;

            Undo.SetTransformParent(child, target, "Migrate PICO XR child");
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
        }

        private static void SetGenericControllerVisual(Transform visual, bool active)
        {
            if (visual == null || visual.gameObject.activeSelf == active)
                return;
            Undo.RecordObject(visual.gameObject, "Set generic controller visual state");
            visual.gameObject.SetActive(active);
        }

        private static void SetBoolean(UnityEngine.Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Missing serialized property: {propertyName}");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
