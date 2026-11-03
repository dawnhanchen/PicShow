/*******************************************************************************
Copyright © 2015-2022 PICO Technology Co., Ltd.All rights reserved.

NOTICE：All information contained herein is, and remains the property of 
PICO Technology Co., Ltd. The intellectual and technical concepts 
contained herein are proprietary to PICO Technology Co., Ltd. and may be 
covered by patents, patents in process, and are protected by trade secret or 
copyright law. Dissemination of this information or reproduction of this 
material is strictly forbidden unless prior written permission is obtained from
PICO Technology Co., Ltd. 
*******************************************************************************/
// PlaneDetectionManager is the Plane-Detection sibling of SpatialMeshManager.
// It reuses SpatialMeshManager's render pipeline when mesh rendering is enabled,
// while the default PicShow mode uses it as a wall geometry source for the
// immersive wall selection UI.
//
// PLANE DETECTION IS PICO-NATIVE ONLY. PICO ships NO plane-detection OpenXR
// feature; the provider is created only inside the native PXR_Loader. Therefore
// this driver is guarded by ENABLE_PICO_XR_SDK only and refuses to run under a
// non-PICO runtime.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PicShow;
using UnityEngine;
using UnityEngine.XR;
using Debug = UnityEngine.Debug;

#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;

public class PlaneDetectionManager : MonoBehaviour, IWallPlaneSource
{
    public static PlaneDetectionManager Instance { get; private set; }

    [Header("通用配置")]
    [SerializeField] private int maxRenderPerFrame = 100;
    [SerializeField] private int meshAmount = 200;
    [Header("平面过滤")]
    [Tooltip("只渲染语义标签为 Wall 的平面；关闭后显示所有平面。")]
    [SerializeField] private bool renderOnlyWallPlanes = true;
    [Header("材质与容器")]
    [SerializeField] private Transform meshContainer;
    [SerializeField] private GameObject meshPrefab;
    [SerializeField] private Material wireframeMaterial;
    [SerializeField] private Material transparentMaterial;
    [Tooltip("墙面选择模式不需要重复的线框，默认交由墙面 UI 表达检测结果。")]
    [SerializeField] private bool renderPlaneMeshes = false;

    private readonly Dictionary<Guid, GameObject> planeList = new();
    private readonly Dictionary<Guid, WallPlaneInfo> wallList = new();
    private readonly Dictionary<Guid, PxrPlaneData> needUpdatePlaneList = new();
    private readonly Queue<GameObject> pool = new();
    private Mesh mesh;
    private Transform _camera;
    private readonly object listLock = new();
    private bool isStopUpdatePlane = true;
    private bool subscribed;
    private bool poolCreated;
    private bool detectionRequested;
    private bool providerStarting;
    private bool providerRunning;
    private static readonly SemaphoreSlim providerStartGate = new(1, 1);

    public bool DetectionEnabled { get; private set; }
    public IReadOnlyDictionary<Guid, WallPlaneInfo> Walls => wallList;

    public event Action<WallPlaneInfo> WallAdded;
    public event Action<WallPlaneInfo> WallUpdated;
    public event Action<Guid> WallRemoved;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            Debug.LogError("该单例存在多个实例！");
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        ResolveCamera();
    }

    public void SetEnabled(bool enabled) => SetDetectionEnabled(enabled);

    public void SetDetectionEnabled(bool enabled)
    {
        if (enabled)
            StartDetection();
        else
            StopDetection();
    }

    private async void StartDetection()
    {
        detectionRequested = true;
        if (providerStarting)
            return;
        if (providerRunning)
        {
            EnableDelivery();
            return;
        }
        ResolveCamera();
        EnsurePool();
        providerStarting = true;
        // The native provider handle is shared across component instances.
        await providerStartGate.WaitAsync();
        try
        {
            if (!detectionRequested || this == null)
                return;
            PxrResult result = await StartProviderAsync();
            if (result != PxrResult.SUCCESS)
            {
                Debug.LogWarning("Start plane detection provider failed: " + result);
                return;
            }
            providerRunning = true;
            // Do not release the shared gate until late-start cleanup has finished.
            if (!detectionRequested || this == null)
                StopRunningProvider();
            else
                EnableDelivery();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Start plane detection provider failed: " + exception.Message);
        }
        finally
        {
            providerStarting = false;
            providerStartGate.Release();
        }
    }

    private void EnableDelivery()
    {
        if (!subscribed)
        {
            PXR_Manager.PlaneDetectionDataUpdated += PXR_OnPlaneDetectionDataUpdated;
            subscribed = true;
        }
        isStopUpdatePlane = false;
        DetectionEnabled = true;
    }

    private void StopDetection()
    {
        detectionRequested = false;
        DetectionEnabled = false;
        isStopUpdatePlane = true;
        if (subscribed)
        {
            PXR_Manager.PlaneDetectionDataUpdated -= PXR_OnPlaneDetectionDataUpdated;
            subscribed = false;
        }
        // Never issue stop concurrently with the SDK's outstanding start task.
        if (!providerStarting)
            StopRunningProvider();
        ClearWalls();
    }

    private void StopRunningProvider()
    {
        if (!providerRunning)
            return;
        try
        {
            var result = StopProvider();
            if (result != PxrResult.SUCCESS)
            {
                Debug.LogWarning("Stop plane detection provider failed: " + result);
                return;
            }
            providerRunning = false;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Stop plane detection provider failed: " + exception.Message);
        }
    }

    public void ClearWalls()
    {
        lock (listLock)
        {
            foreach (Guid id in wallList.Keys.ToList())
                WallRemoved?.Invoke(id);
            wallList.Clear();
            needUpdatePlaneList.Clear();
            foreach (GameObject plane in planeList.Values)
                SetPlaneToPool(plane);
            planeList.Clear();
        }
    }

    protected virtual Task<PxrResult> StartProviderAsync() =>
        PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.PlaneDetection);

    protected virtual PxrResult StopProvider() =>
        PXR_MixedReality.StopSenseDataProvider(PxrSenseDataProviderType.PlaneDetection);

    void Update()
    {
        if (!DetectionEnabled || isStopUpdatePlane) return;
        if (_camera != null)
            Shader.SetGlobalVector("_TargetPosition", _camera.position);
        if (needUpdatePlaneList.Count == 0)
            return;
        lock (listLock)
        {
            var keysToRemove = needUpdatePlaneList.Keys.Take(maxRenderPerFrame).ToList();
            foreach (Guid key in keysToRemove)
                PlaneUpdateQueue(needUpdatePlaneList[key]);
            foreach (Guid key in keysToRemove)
                needUpdatePlaneList.Remove(key);
        }
    }

    private void PlaneUpdateQueue(PxrPlaneData data)
    {
        if (data.state == MeshChangeState.Removed)
        {
            RemoveWall(data.uuid);
            SetPlaneToPool(planeList.TryGetValue(data.uuid, out GameObject removed) ? removed : null);
            planeList.Remove(data.uuid);
            return;
        }

        if (!TryBuildWall(data, _camera != null ? _camera.position : Vector3.zero,
                TrackingToWorld, false, out WallPlaneInfo wall))
        {
            RemoveWall(data.uuid);
            if (planeList.TryGetValue(data.uuid, out GameObject stale))
            {
                SetPlaneToPool(stale);
                planeList.Remove(data.uuid);
            }
            return;
        }

        bool existed = wallList.ContainsKey(data.uuid);
        wallList[data.uuid] = wall;
        if (existed)
            WallUpdated?.Invoke(wall);
        else
            WallAdded?.Invoke(wall);

        if (!renderPlaneMeshes)
            return;
        if (planeList.TryGetValue(data.uuid, out GameObject existing))
            CreateMesh(data, existing);
        else
        {
            GameObject plane = GetPlaneFromPool();
            if (plane == null)
                return;
            planeList.Add(data.uuid, plane);
            CreateMesh(data, plane);
        }
    }

    public static bool TryBuildWall(PxrPlaneData data, Vector3 cameraPosition,
        out WallPlaneInfo wall) => TryBuildWall(data, cameraPosition, Matrix4x4.identity,
        false, out wall);

    public static bool TryBuildWall(PxrPlaneData data, Vector3 cameraPosition,
        Matrix4x4 trackingToWorld, out WallPlaneInfo wall) => TryBuildWall(data,
        cameraPosition, trackingToWorld, false, out wall);

    public static bool TryBuildWall(PxrPlaneData data, Vector3 cameraPosition,
        Matrix4x4 trackingToWorld, bool allowGeometricFallback, out WallPlaneInfo wall)
    {
        int[] indices = data.indices != null && data.indices.Length > 0
            ? data.indices.Select(index => (int)index).ToArray()
            : null;
        return WallGeometry.TryBuild(data.uuid, WorldVertices(data, trackingToWorld), indices,
            data.label == PxrSemanticLabel.Wall, cameraPosition, allowGeometricFallback,
            out wall, out _);
    }

    private Matrix4x4 TrackingToWorld => _camera != null && _camera.parent != null
        ? _camera.parent.localToWorldMatrix : Matrix4x4.identity;

    private static Vector3[] WorldVertices(PxrPlaneData data, Matrix4x4 trackingToWorld)
    {
        if (data.vertices == null)
            return null;
        var vertices = new Vector3[data.vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = trackingToWorld.MultiplyPoint3x4(
                data.rotation * data.vertices[i] + data.position);
        return vertices;
    }

    private void RemoveWall(Guid id)
    {
        if (!wallList.Remove(id))
            return;
        WallRemoved?.Invoke(id);
    }

    private void EnsurePool()
    {
        if (poolCreated || !renderPlaneMeshes || meshPrefab == null)
            return;
        for (var i = 0; i < meshAmount; i++)
        {
            var plane = Instantiate(meshPrefab, meshContainer);
            pool.Enqueue(plane);
            plane.SetActive(false);
        }
        poolCreated = true;
    }

    private GameObject GetPlaneFromPool()
    {
        if (!renderPlaneMeshes || meshPrefab == null)
            return null;
        EnsurePool();
        GameObject plane;
        if (pool.Count > 0)
        {
            plane = pool.Dequeue();
            plane.SetActive(true);
        }
        else
        {
            plane = Instantiate(meshPrefab, meshContainer);
            plane.SetActive(true);
        }
        var renderer = plane.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            MaterialPropertyBlock props = new();
            props.SetFloat("_StartTime", Time.time);
            renderer.SetPropertyBlock(props);
        }
        return plane;
    }

    private void SetPlaneToPool(GameObject plane)
    {
        if (plane == null)
            return;
        plane.SetActive(false);
        if (pool.Count > meshAmount)
            Destroy(plane);
        else
            pool.Enqueue(plane);
    }

    private void PXR_OnPlaneDetectionDataUpdated(List<PxrPlaneData> list)
    {
        lock (listLock)
        {
            if (!DetectionEnabled)
                return;
            foreach (PxrPlaneData item in list)
            {
                if (needUpdatePlaneList.ContainsKey(item.uuid))
                {
                    switch (item.state)
                    {
                        case MeshChangeState.Added:
                        case MeshChangeState.Updated:
                            needUpdatePlaneList[item.uuid] = item;
                            break;
                        case MeshChangeState.Removed:
                        case MeshChangeState.Unchanged:
                            needUpdatePlaneList.Remove(item.uuid);
                            break;
                    }
                }
                else if (item.state != MeshChangeState.Unchanged)
                {
                    needUpdatePlaneList.Add(item.uuid, item);
                }
            }
        }
    }

    private void CreateMesh(PxrPlaneData block, GameObject planeGameObject)
    {
        if (planeGameObject == null)
            return;
        var meshFilter = planeGameObject.GetComponentInChildren<MeshFilter>();
        var meshCollider = planeGameObject.GetComponentInChildren<MeshCollider>();
        if (meshFilter == null)
            return;
        if (meshFilter.mesh == null)
        {
            mesh = new Mesh();
            mesh.Clear();
            mesh.MarkDynamic();
        }
        else
        {
            mesh = meshFilter.mesh;
            mesh.Clear();
        }
        var vertices = WorldVertices(block, TrackingToWorld);
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = meshFilter.transform.InverseTransformPoint(vertices[i]);
        mesh.SetVertices(vertices);
        mesh.SetTriangles(block.indices, 0);
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
        if (meshCollider != null)
            meshCollider.sharedMesh = mesh;
    }

    private void ResolveCamera()
    {
        if (_camera == null && Camera.main != null)
            _camera = Camera.main.transform;
    }

    private void OnDisable()
    {
        StopDetection();
    }

    private void OnDestroy()
    {
        StopDetection();
        if (Instance == this)
            Instance = null;
    }
}
#endif
