using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PicShow
{
    public sealed class WallDisplayController : MonoBehaviour, IWallDisplay
    {
        [SerializeField] private MonoBehaviour wallSourceProvider;
        [SerializeField] private GalleryCatalog catalog;
        [SerializeField] private GameObject wallPanelPrefab;
        [SerializeField] private GameObject wallGalleryPrefab;
        [SerializeField] private Transform wallUiRoot;

        private IWallPlaneSource source;
        private readonly Dictionary<Guid, WallPanelView> panels =
            new Dictionary<Guid, WallPanelView>();
        private readonly List<Guid> order = new List<Guid>();
        private readonly WallSurfaceMerger surfaceMerger = new WallSurfaceMerger();
        private WallGalleryView gallery;
        private Guid selectedWall;
        private bool freezeSelected;
        private bool active;
        private bool initialized;
        private int clearCount;

        public Action<Guid> WallSelected;
        public Action WallSelectionClosed;
        public bool Active => active;
        public int ClearCount => clearCount;
        public IReadOnlyDictionary<Guid, WallPanelView> Panels => panels;
        public WallGalleryView Gallery => gallery;
        public Guid SelectedWall => selectedWall;

        public void Configure(MonoBehaviour sourceProvider, GalleryCatalog content,
            GameObject panelPrefab, GameObject galleryPrefab, Transform uiRoot)
        {
            wallSourceProvider = sourceProvider;
            catalog = content;
            wallPanelPrefab = panelPrefab;
            wallGalleryPrefab = galleryPrefab;
            wallUiRoot = uiRoot;
        }

        public void Initialize()
        {
            if (initialized)
                return;
            if (source == null && wallSourceProvider != null)
                source = wallSourceProvider as IWallPlaneSource;
            if (source == null)
                return;
            source.WallAdded += OnWallAdded;
            source.WallUpdated += OnWallUpdated;
            source.WallRemoved += OnWallRemoved;
            initialized = true;
        }

        public void SetWallSourceForTests(IWallPlaneSource newSource)
        {
            if (initialized && source != null)
            {
                source.WallAdded -= OnWallAdded;
                source.WallUpdated -= OnWallUpdated;
                source.WallRemoved -= OnWallRemoved;
            }
            source = newSource;
            initialized = false;
            ClearAll();
            Initialize();
        }

        private void OnDestroy()
        {
            if (!initialized || source == null)
                return;
            source.WallAdded -= OnWallAdded;
            source.WallUpdated -= OnWallUpdated;
            source.WallRemoved -= OnWallRemoved;
            initialized = false;
        }

        public void SetActive(bool value)
        {
            active = value;
            if (value)
                SyncPanels();
            else
                ClearAll();
        }

        public void ClearAll()
        {
            clearCount++;
            selectedWall = Guid.Empty;
            freezeSelected = false;
            surfaceMerger.Clear();
            if (gallery != null)
            {
                DestroyUnityObject(gallery.gameObject);
                gallery = null;
            }
            foreach (WallPanelView panel in panels.Values)
            {
                if (panel != null)
                    DestroyUnityObject(panel.gameObject);
            }
            panels.Clear();
            order.Clear();
        }

        public void AddWall(WallPlaneInfo wall)
        {
            if (wall.Id == Guid.Empty)
                return;
            surfaceMerger.AddOrUpdate(wall);
            SyncPanels();
        }

        public void UpdateWall(WallPlaneInfo wall)
        {
            if (wall.Id == Guid.Empty)
                return;
            surfaceMerger.AddOrUpdate(wall);
            SyncPanels();
        }

        public void RemoveWall(Guid id)
        {
            surfaceMerger.Remove(id);
            SyncPanels();
        }

        private void SyncPanels()
        {
            if (!active)
                return;
            var live = new HashSet<Guid>();
            foreach (WallSurface surface in surfaceMerger.Surfaces)
                live.Add(surface.Id);
            RemoveStalePanels(live);
            foreach (WallSurface surface in surfaceMerger.Surfaces)
            {
                if (freezeSelected && selectedWall == surface.Id)
                    continue;
                if (panels.TryGetValue(surface.Id, out WallPanelView panel))
                {
                    panel.ApplyInfo(surface.Wall);
                    if (gallery != null && selectedWall == surface.Id)
                        gallery.ApplyWallInfo(surface.Wall);
                    continue;
                }
                if (wallPanelPrefab == null)
                    continue;
                Transform parent = wallUiRoot != null ? wallUiRoot : transform;
                GameObject instance = Instantiate(wallPanelPrefab, parent, false);
                instance.name = "Wall " + (order.Count + 1);
                WallPanelView created = instance.GetComponent<WallPanelView>();
                if (created == null)
                {
                    DestroyUnityObject(instance);
                    continue;
                }
                order.Add(surface.Id);
                panels.Add(surface.Id, created);
                created.Configure(surface.Wall, order.Count, () => SelectWall(surface.Id));
                created.SetVisible(selectedWall == Guid.Empty);
            }
        }

        private void RemoveStalePanels(HashSet<Guid> live)
        {
            List<Guid> stale = panels.Keys.Where(id => !live.Contains(id)).ToList();
            bool selectedRemoved = false;
            foreach (Guid id in stale)
            {
                if (freezeSelected && selectedWall == id)
                    continue;
                if (!panels.TryGetValue(id, out WallPanelView panel))
                    continue;
                DestroyUnityObject(panel.gameObject);
                panels.Remove(id);
                order.Remove(id);
                if (selectedWall == id)
                {
                    selectedWall = Guid.Empty;
                    selectedRemoved = true;
                }
            }
            if (!selectedRemoved)
                return;
            if (gallery != null)
            {
                DestroyUnityObject(gallery.gameObject);
                gallery = null;
            }
            ShowSelectionPanels();
            WallSelectionClosed?.Invoke();
        }

        public void SelectWall(Guid id)
        {
            if (!active || selectedWall != Guid.Empty || !panels.ContainsKey(id))
                return;
            selectedWall = id;
            freezeSelected = true;
            if (gallery != null)
            {
                DestroyUnityObject(gallery.gameObject);
                gallery = null;
            }
            foreach (KeyValuePair<Guid, WallPanelView> entry in panels)
            {
                if (entry.Value != null)
                    entry.Value.SetVisible(false);
            }
            WallPanelView panel = panels[id];
            WallPlaneInfo wall = panel.Info;
            if (wallGalleryPrefab != null && wall.Id != Guid.Empty)
            {
                Transform parent = wallUiRoot != null ? wallUiRoot : transform;
                GameObject instance = Instantiate(wallGalleryPrefab, parent, false);
                instance.name = "Wall Gallery";
                gallery = instance.GetComponent<WallGalleryView>();
                if (gallery != null && catalog != null)
                    gallery.Configure(catalog, wall, BackToWallSelection);
            }
            WallSelected?.Invoke(id);
        }

        public void BackToWallSelection()
        {
            freezeSelected = false;
            selectedWall = Guid.Empty;
            if (gallery != null)
            {
                DestroyUnityObject(gallery.gameObject);
                gallery = null;
            }
            SyncPanels();
            ShowSelectionPanels();
            WallSelectionClosed?.Invoke();
        }

        public void SelectCategory(int index)
        {
            if (gallery != null)
                gallery.ShowCategory(index);
        }

        private void ShowSelectionPanels()
        {
            foreach (WallPanelView panel in panels.Values)
            {
                if (panel != null)
                    panel.SetVisible(true);
            }
        }

        private void OnWallAdded(WallPlaneInfo wall) => AddWall(wall);
        private void OnWallUpdated(WallPlaneInfo wall) => UpdateWall(wall);
        private void OnWallRemoved(Guid id) => RemoveWall(id);

        // The XR UI input module reports a hover exit on the frame after a
        // hovered canvas disappears. Destroying it immediately makes the PICO
        // interactor manager read a destroyed object inside
        // XRUIInputModule.DoProcess and abort that frame's input processing.
        // Deactivate first, then retire the object after a short grace period.
        private const float DestroyGraceSeconds = 0.1f;

        private static void DestroyUnityObject(GameObject target)
        {
            if (target == null)
                return;
            target.SetActive(false);
            if (Application.isPlaying)
                Destroy(target, DestroyGraceSeconds);
            else
                DestroyImmediate(target);
        }
    }
}
