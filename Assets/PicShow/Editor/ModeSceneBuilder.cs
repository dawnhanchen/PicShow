using System;
using System.IO;
using System.Linq;
using ByteDance.PICO.Interaction;
using PicShow.PicoInteraction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace PicShow.Editor
{
    public static class ModeSceneBuilder
    {
        private const string Root = "Assets/PicShow";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string UiFolder = Root + "/UI";
        private const string CatalogPath = Root + "/Data/GalleryCatalog.asset";
        private const string FontPath = Root + "/Fonts/GalleryChinese SDF.asset";
        private const string ModeFontPath = Root + "/Fonts/ModeChinese SDF.asset";
        private const string SourceFontPath = Root + "/Fonts/NotoSansSC-Regular.ttf";
        private const string RoundedPath = Root + "/UI/RoundedPanel.png";
        private const string PanelPrefabPath = PrefabFolder + "/ModeSelection.prefab";
        private const string WallPanelPrefabPath = PrefabFolder + "/WallPanel.prefab";
        private const string WallGalleryPrefabPath = PrefabFolder + "/WallGallery.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SkyboxFolder = Root + "/Art/Skybox";
        private const string SkyTexturePath = SkyboxFolder + "/SkyGradientSeamless.png";
        private const string SkyMaterialPath = SkyboxFolder + "/SkyGradient.mat";
        private const string SkyShaderName = "Skybox/Panoramic";
        private const string SkyMaterialName = "Sky Gradient";
        private const int SkyTextureWidth = 2048;
        private const int SkyTextureHeight = 1024;
        private const float Tau = 6.2831853f;
        private static readonly Color SkyZenith = Hex("#5E9FBF");
        private static readonly Color SkyMid = Hex("#A9D2DC");
        private static readonly Color SkyHorizon = Hex("#FBDFC0");
        private static readonly Color SkyGround = Hex("#8C7362");
        private const string AltSkyTexturePath = SkyboxFolder + "/GalleryAtmosphereSeamless.png";
        private const string AltSkyMaterialPath = SkyboxFolder + "/GalleryAtmosphere.mat";
        private const string AltSkyMaterialName = "Gallery Atmosphere";
        private static readonly Color GalleryFloor = Hex("#3C3631");
        private static readonly Color GalleryFloorBase = Hex("#5A534C");
        private static readonly Color GalleryWallBase = Hex("#736A61");
        private static readonly Color GalleryWallTop = Hex("#6A6259");
        private static readonly Color GalleryCeilingBase = Hex("#5B544D");
        private static readonly Color GalleryCeilingTop = Hex("#413B36");
        private const string ModeRootName = "PicShow Mode Root";
        private const string WallUiRootName = "Wall UI Root";
        private const string MenuText = "MR 模式";
        private const string NonMenuText = "非 MR 模式";
        private const int IconTextureSize = 512;
        // Matches GallerySceneBuilder: the 512 px rounded sprite with a 160 px border keeps
        // the same on-screen corner radius as the previous 64 px sprite.
        private const float RoundedPixelsPerUnitMultiplier = 4.4f;
        private static readonly Color Ink = Hex("#EDF1E9");
        private static readonly Color Muted = Hex("#ACB9B4");
        private static readonly Color Accent = Hex("#C1DECE");
        private static readonly Color Panel = Hex("#24332E");
        // Only used when the scene has no return entry yet; manual tweaks in the scene win.
        private static readonly Vector3 ReturnButtonDefaultPosition = new Vector3(0f, -0.235f, 0.66f);
        private static TMP_FontAsset font;
        private static Sprite rounded;
        private static float iconScale = 1f;
        private static Sprite mrIcon;
        private static Sprite nonMrIcon;

        [MenuItem("PicShow/Build All UI")]
        public static void BuildAll()
        {
            // Regenerate the rounded sprite first so the rebuilt prefabs pick up the sharp version.
            GallerySceneBuilder.UpgradeSharpness();
            Build();
        }

        [MenuItem("PicShow/Build Mode Selection UI")]
        public static void Build()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before building the mode UI.");
            EnsureFolder(PrefabFolder);
            EnsureFolder(UiFolder);
            font = BuildModeFont();
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            if (rounded == null)
                throw new InvalidOperationException("Rounded panel sprite is missing: " + RoundedPath);
            mrIcon = BuildIcon("ModeMr.png", true);
            nonMrIcon = BuildIcon("ModeNonMr.png", false);
            BuildModePanelPrefab();
            BuildWallPanelPrefab();
            BuildWallGalleryPrefab();
            WireScene();
            Debug.Log("PicShow mode selection prefabs and scene wiring saved.");
        }

        private static TMP_FontAsset BuildModeFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ModeFontPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(ModeFontPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
                throw new InvalidOperationException("Source font is missing: " + SourceFontPath);
            var asset = TMP_FontAsset.CreateFontAsset(source, 120, 12,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, false);
            string glyphs = MenuText + NonMenuText +
                "选择该墙面显示墙面模式换墙面返回上一页沉浸展示检测轮播浏览选择体验模式墙面展示三图作品分类或·" +
                "水彩治愈现代都市赛博朋克超现实奇幻";
            string ascii = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
            if (!asset.TryAddCharacters(glyphs + ascii, out string missing))
                throw new InvalidOperationException("Missing mode UI font glyphs: " + missing);
            asset.name = "ModeChinese SDF";
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, ModeFontPath);
            asset.material.name = "ModeChinese Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (Texture2D texture in asset.atlasTextures)
            {
                texture.name = "ModeChinese Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject CreateCanvasRoot(string name, Vector2 size)
        {
            var root = Rect(name, null, Vector2.zero, size);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;
            root.gameObject.AddComponent<GraphicRaycaster>();
            root.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.localScale = Vector3.one * 0.001f;
            return root.gameObject;
        }

        private static void BuildModePanelPrefab()
        {
            GameObject root = CreateCanvasRoot("Mode Selection", new Vector2(1200, 700));
            try
            {
                Image("Backdrop", root.transform, Vector2.zero, new Vector2(1200, 700),
                    Hex("#17231E"), true);
                Text("Title", root.transform, "选择体验模式", new Vector2(0, 288),
                    new Vector2(1000, 70), 48, Ink);
                Text("Subtitle", root.transform, "选择 MR 墙面展示或非 MR 轮播浏览", new Vector2(0, 228),
                    new Vector2(1050, 44), 24, Muted);

                Button mr = BuildModeCard(root.transform, -235f, MenuText, "墙面检测 · 沉浸展示",
                    mrIcon, out Image mrImage);
                Button nonMr = BuildModeCard(root.transform, 235f, NonMenuText, "三图轮播 · 作品浏览",
                    nonMrIcon, out Image nonMrImage);

                var view = root.AddComponent<ModePanelView>();
                SetReference(view, "mrButton", mr);
                SetReference(view, "nonMrButton", nonMr);
                SetReference(view, "mrLabel", mr.transform.Find("Label").GetComponent<TMP_Text>());
                SetReference(view, "nonMrLabel", nonMr.transform.Find("Label").GetComponent<TMP_Text>());
                SetReference(view, "mrIcon", mrImage);
                SetReference(view, "nonMrIcon", nonMrImage);

                AddEnhanced(mr);
                AddEnhanced(nonMr);
                var bridge = root.AddComponent<PicoButtonClickBridge>();
                bridge.Configure(new[] { mr, nonMr });

                var follow = root.AddComponent<LazyFollow>();
                follow.targetOffset = new Vector3(0f, 0f, 1.6f);
                follow.movementSpeed = 8f;
                follow.snapOnEnable = true;
                follow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
                follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAt;
                var timed = root.AddComponent<TimedLazyFollow>();
                timed.Configure(follow, 3f);

                PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Button BuildModeCard(Transform parent, float x, string label, string caption,
            Sprite icon, out Image iconImage)
        {
            var card = Rect("Mode " + label, parent, new Vector2(x, -55), new Vector2(420, 470));
            var background = card.gameObject.AddComponent<Image>();
            background.sprite = rounded;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = RoundedPixelsPerUnitMultiplier;
            background.color = Panel;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var hover = card.gameObject.AddComponent<ModeCardHover>();
            hover.Configure(background, Color.Lerp(Panel, Color.white, 0.16f), 1.05f);

            Image ring = Image("Icon ring", card.transform, new Vector2(0, 82), new Vector2(190, 190),
                Hex("#16231E"), true);
            iconImage = Image("Icon", ring.transform, Vector2.zero, new Vector2(160, 160), Color.white);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            Text("Label", card.transform, label, new Vector2(0, -55),
                new Vector2(380, 60), 34, Ink);
            Text("Caption", card.transform, caption, new Vector2(0, -105),
                new Vector2(380, 42), 22, Muted);
            return button;
        }

        private static void BuildWallPanelPrefab()
        {
            GameObject root = CreateCanvasRoot("Wall Panel", new Vector2(1000, 1000));
            try
            {
                Image fill = Image("Fill", root.transform, Vector2.zero, Vector2.zero,
                    new Color(0.76f, 0.87f, 0.81f, 0.18f));
                RectTransform fillRect = (RectTransform)fill.transform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;

                RectTransform border = Rect("Border", root.transform, Vector2.zero, Vector2.zero);
                border.anchorMin = Vector2.zero;
                border.anchorMax = Vector2.one;
                border.offsetMin = Vector2.zero;
                border.offsetMax = Vector2.zero;
                BuildBorderBar("Border Top", border, new Vector2(0f, 1f),
                    new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 6f));
                BuildBorderBar("Border Bottom", border, new Vector2(0f, 0f),
                    new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f));
                BuildBorderBar("Border Left", border, new Vector2(0f, 0f),
                    new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(6f, 0f));
                BuildBorderBar("Border Right", border, new Vector2(1f, 0f),
                    new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(6f, 0f));

                var surfaceObject = new GameObject("Surface", typeof(RectTransform), typeof(Image),
                    typeof(Button));
                surfaceObject.transform.SetParent(root.transform, false);
                RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
                surfaceRect.anchorMin = new Vector2(0.5f, 0.5f);
                surfaceRect.anchorMax = new Vector2(0.5f, 0.5f);
                surfaceRect.sizeDelta = new Vector2(560f, 180f);
                surfaceRect.anchoredPosition = Vector2.zero;
                var surface = surfaceObject.GetComponent<Image>();
                surface.color = new Color(0.05f, 0.09f, 0.075f, 0.58f);
                surface.raycastTarget = true;
                var button = surfaceObject.GetComponent<Button>();
                button.targetGraphic = surface;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };

                TMP_Text prompt = StretchText("Prompt", surfaceObject.transform, "选择该墙面显示",
                    0.03f, 0.25f, 0.97f, 0.78f, 54, Ink);
                TMP_Text index = StretchText("Wall index", surfaceObject.transform, "墙面 1",
                    0.08f, 0.02f, 0.92f, 0.23f, 26, Muted);

                var view = root.AddComponent<WallPanelView>();
                SetReference(view, "root", root.GetComponent<RectTransform>());
                SetReference(view, "selectButton", button);
                SetReference(view, "promptLabel", prompt);
                SetReference(view, "indexLabel", index);
                AddEnhanced(button);
                var bridge = root.AddComponent<PicoButtonClickBridge>();
                bridge.Configure(new[] { button });

                PrefabUtility.SaveAsPrefabAsset(root, WallPanelPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildBorderBar(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, size);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.76f, 0.87f, 0.81f, 0.9f);
            image.raycastTarget = false;
        }

        private static void BuildWallGalleryPrefab()
        {
            GameObject root = CreateCanvasRoot("Wall Gallery", new Vector2(1000, 1000));
            try
            {
                var view = root.AddComponent<WallGalleryView>();
                Button[] categories = new Button[4];
                TMP_Text[] labels = new TMP_Text[4];
                for (int i = 0; i < 4; i++)
                {
                    categories[i] = BuildWallButton(root.transform, "Category " + (i + 1),
                        new Vector2((i - 1.5f) * 230f, 430f), new Vector2(210, 70), Panel,
                        out labels[i]);
                }

                Button back = BuildWallButton(root.transform, "Change wall",
                    new Vector2(-430f, 430f), new Vector2(150, 64), Hex("#33443C"),
                    out TMP_Text backLabel);
                backLabel.text = "换墙面";

                RawImage[] slots = new RawImage[5];
                for (int i = 0; i < 5; i++)
                {
                    var slotObject = new GameObject("Artwork " + (i + 1), typeof(RectTransform));
                    slotObject.transform.SetParent(root.transform, false);
                    RectTransform rect = slotObject.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(220, 150);
                    rect.anchoredPosition = new Vector2((i - 2) * 240f, -40f);
                    var image = slotObject.AddComponent<RawImage>();
                    image.raycastTarget = false;
                    image.color = Color.white;
                    slots[i] = image;
                }

                var backButton = back;
                var categoryButtons = categories;
                var categoryLabels = labels;
                var imageSlots = slots;
                SetReference(view, "root", root.GetComponent<RectTransform>());
                SetReference(view, "backButton", backButton);
                SetArray(view, "categoryButtons", categoryButtons);
                SetArray(view, "categoryLabels", categoryLabels);
                SetArray(view, "imageSlots", imageSlots);

                foreach (Button category in categories) AddEnhanced(category);
                AddEnhanced(back);
                var bridge = root.AddComponent<PicoButtonClickBridge>();
                bridge.Configure(new[] { categories[0], categories[1], categories[2], categories[3], back });

                PrefabUtility.SaveAsPrefabAsset(root, WallGalleryPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Button BuildWallButton(Transform parent, string name, Vector2 position,
            Vector2 size, Color color, out TMP_Text label)
        {
            Image background = Image(name, parent, position, size, color, true);
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            label = Text("Label", background.transform, "", Vector2.zero,
                new Vector2(size.x - 16, size.y - 12), 26, Ink);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 6f);
            labelRect.offsetMax = new Vector2(-8f, -6f);
            return button;
        }

        private static void WireScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject gallery = Object.FindAnyObjectByType<GalleryCarousel>(
                FindObjectsInactive.Include)?.gameObject;
            if (gallery == null)
                throw new InvalidOperationException("SampleScene is missing the gallery carousel.");
            PlaneDetectionManager plane = Object.FindAnyObjectByType<PlaneDetectionManager>();
            if (plane == null)
                throw new InvalidOperationException("SampleScene is missing PlaneDetectionManager.");
            Camera camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("SampleScene is missing the main camera.");

            GameObject existing = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == ModeRootName);
            if (existing != null)
                Object.DestroyImmediate(existing);
            Transform previousReturn = camera.transform.Find("Mode Return");
            Vector3 returnPosition = previousReturn != null
                ? previousReturn.localPosition
                : ReturnButtonDefaultPosition;
            while (camera.transform.Find("Mode Return") != null)
                Object.DestroyImmediate(camera.transform.Find("Mode Return").gameObject);

            var modeRoot = new GameObject(ModeRootName);
            SceneManager.MoveGameObjectToScene(modeRoot, scene);
            var runtime = modeRoot.AddComponent<PicoModeRuntime>();
            var controller = modeRoot.AddComponent<AppModeController>();
            AttachSkyboxBackdrop(modeRoot, controller, camera);

            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            GameObject wallPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPanelPrefabPath);
            GameObject wallGalleryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallGalleryPrefabPath);
            GalleryCatalog catalog = AssetDatabase.LoadAssetAtPath<GalleryCatalog>(CatalogPath);
            if (catalog == null)
                throw new InvalidOperationException("Gallery catalog is missing: " + CatalogPath);

            var wallRoot = new GameObject(WallUiRootName);
            wallRoot.transform.SetParent(modeRoot.transform, false);
            var wallDisplay = wallRoot.AddComponent<WallDisplayController>();
            SetReference(wallDisplay, "wallSourceProvider", plane);
            SetReference(wallDisplay, "catalog", catalog);
            SetReference(wallDisplay, "wallPanelPrefab", wallPanelPrefab);
            SetReference(wallDisplay, "wallGalleryPrefab", wallGalleryPrefab);
            SetReference(wallDisplay, "wallUiRoot", wallRoot.transform);

            GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, modeRoot.transform);
            panel.name = "Mode Selection";
            panel.transform.position = camera.transform.position + camera.transform.forward * 1.6f
                - camera.transform.up * 0.05f;
            panel.transform.rotation = camera.transform.rotation;
            var canvas = panel.GetComponent<Canvas>();
            if (canvas != null)
                canvas.worldCamera = camera;
            ModePanelView panelView = panel.GetComponent<ModePanelView>();

            GameObject returnButton = BuildReturnButton(camera.transform, returnPosition);
            SetReference(controller, "modePanel", panelView);
            SetReference(controller, "galleryRoot", gallery);
            SetReference(controller, "returnButton", returnButton);
            SetReference(controller, "wallDisplay", wallDisplay);
            SetReference(controller, "picoRuntimeProvider", runtime);
            SetReference(controller, "wallSourceProvider", plane);

            gallery.SetActive(false);
            panel.SetActive(true);
            returnButton.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Mode UI wired into " + ScenePath);
        }

        /// <summary>
        /// Adds the gallery skybox to the existing scene without rebuilding prefabs or UI.
        /// </summary>
        [MenuItem("PicShow/Apply Skybox Backdrop")]
        public static void EnsureSkyboxBackdrop()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before wiring the skybox backdrop.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject modeRoot = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == ModeRootName);
            if (modeRoot == null)
                throw new InvalidOperationException("SampleScene is missing " + ModeRootName + ".");
            var controller = modeRoot.GetComponent<AppModeController>();
            if (controller == null)
                throw new InvalidOperationException("Mode Root is missing AppModeController.");
            Camera camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("SampleScene is missing the main camera.");

            AttachSkyboxBackdrop(modeRoot, controller, camera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Skybox backdrop wired into " + ScenePath);
        }

        private static void AttachSkyboxBackdrop(GameObject modeRoot, AppModeController controller,
            Camera camera)
        {
            Material material = BuildGalleryAtmosphereMaterial();
            var backdrop = modeRoot.GetComponent<SkyboxBackdrop>() ??
                modeRoot.AddComponent<SkyboxBackdrop>();
            SetReference(backdrop, "targetCamera", camera);
            SetReference(backdrop, "skyMaterial", material);
            SetReference(controller, "skyboxBackdrop", backdrop);
        }

        private static Material BuildSkyMaterial()
        {
            return BuildPanoramaMaterial(SkyTexturePath, SkyMaterialPath, SkyMaterialName,
                BuildSeamlessSkyPixels(SkyTextureWidth, SkyTextureHeight));
        }

        private static Material BuildGalleryAtmosphereMaterial()
        {
            return BuildPanoramaMaterial(AltSkyTexturePath, AltSkyMaterialPath, AltSkyMaterialName,
                BuildGalleryAtmospherePixels(SkyTextureWidth, SkyTextureHeight));
        }

        private static Material BuildPanoramaMaterial(string texturePath, string materialPath,
            string materialName, Color32[] pixels)
        {
            EnsureFolder(SkyboxFolder);
            Texture2D texture = BuildPanoramaTexture(texturePath, pixels);
            Shader shader = Shader.Find(SkyShaderName);
            if (shader == null)
                throw new InvalidOperationException("Skybox shader is missing: " + SkyShaderName);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool created = material == null;
            if (created)
            {
                material = new Material(shader);
                material.name = materialName;
            }

            material.shader = shader;
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Mapping", 1f); // Latitude Longitude for a 2:1 panorama.
            material.SetFloat("_ImageType", 0f); // Plain 2D texture.
            material.SetFloat("_Exposure", 1f);
            if (created)
                AssetDatabase.CreateAsset(material, materialPath);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureSkyTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                throw new InvalidOperationException("Skybox texture importer is missing: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        private static Texture2D BuildPanoramaTexture(string path, Color32[] pixels)
        {
            var texture = new Texture2D(SkyTextureWidth, SkyTextureHeight, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureSkyTexture(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Builds a horizontally seamless equirectangular panorama. Every horizontal term is
        /// periodic over the texture width, so the first column continues into the last column
        /// without a visible seam, and the poles stay horizontally uniform.
        /// </summary>
        public static Color32[] BuildSeamlessSkyPixels(int width, int height)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(nameof(width), "The panorama needs at least 2x2 pixels.");

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                Color sky = SkyGradientColor(v);
                float polarFade = Mathf.Clamp01(Mathf.Min(v, 1f - v) / 0.08f);
                float cloudBand = Mathf.Exp(-Mathf.Pow((v - 0.42f) / 0.22f, 2f)) * polarFade;
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float cloud = Mathf.Sin(Tau * 2f * u + 0.6f) * 0.5f
                        + Mathf.Sin(Tau * 5f * u + 2.2f) * 0.3f
                        + Mathf.Sin(Tau * 9f * u + 4.1f) * 0.2f;
                    // Tiny periodic dither breaks 8-bit banding without introducing a seam.
                    float dither = Mathf.Sin(Tau * 257f * u + 1.3f) * 0.0015f;
                    float lift = cloud * cloudBand * 0.045f + dither * polarFade;
                    pixels[y * width + x] = ToColor32(sky.r + lift, sky.g + lift * 0.9f, sky.b + lift * 0.8f);
                }
            }
            return pixels;
        }

        /// <summary>
        /// Builds a seamless, softly defocused gallery interior: a warm floor rising into a
        /// luminous wall with large light pools, and a dimmer ceiling above. Every horizontal
        /// term wraps over the texture width, so the panorama has no seam.
        /// </summary>
        public static Color32[] BuildGalleryAtmospherePixels(int width, int height)
        {
            if (width < 2 || height < 2)
                throw new ArgumentOutOfRangeException(nameof(width), "The panorama needs at least 2x2 pixels.");

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                Color surface = GallerySurfaceColor(v);
                float polarFade = Mathf.Clamp01(Mathf.Min(v, 1f - v) / 0.08f);
                float wallBand = Mathf.Exp(-Mathf.Pow((v - 0.58f) / 0.28f, 2f)) * polarFade;
                float floorBand = Mathf.Exp(-Mathf.Pow((v - 0.18f) / 0.16f, 2f)) * polarFade;
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float bloom = Mathf.Exp(-Mathf.Pow(WrappedDistance(u, 0.17f) / 0.20f, 2f)) * 0.62f
                        + Mathf.Exp(-Mathf.Pow(WrappedDistance(u, 0.63f) / 0.16f, 2f)) * 0.38f;
                    float sweep = Mathf.Sin(Tau * u + 0.7f) * 0.5f
                        + Mathf.Sin(Tau * 2f * u + 2.4f) * 0.3f;
                    float reflection = Mathf.Exp(-Mathf.Pow(WrappedDistance(u, 0.42f) / 0.30f, 2f));
                    float dither = Mathf.Sin(Tau * 257f * u + 1.3f) * 0.0015f;
                    float lift = bloom * wallBand * 0.075f
                        + sweep * floorBand * 0.012f
                        + reflection * floorBand * 0.020f
                        + dither * polarFade;
                    pixels[y * width + x] = ToColor32(surface.r + lift, surface.g + lift * 0.96f,
                        surface.b + lift * 0.9f);
                }
            }
            return pixels;
        }

        private static float WrappedDistance(float u, float center)
        {
            float distance = Mathf.Abs(u - center);
            return Mathf.Min(distance, 1f - distance);
        }

        private static Color GallerySurfaceColor(float v)
        {
            if (v >= 0.84f)
                return Color.Lerp(GalleryCeilingBase, GalleryCeilingTop,
                    SmoothStep01((v - 0.84f) / 0.16f));
            if (v >= 0.40f)
                return Color.Lerp(GalleryWallBase, GalleryWallTop,
                    SmoothStep01((v - 0.40f) / 0.44f));
            if (v >= 0.28f)
                return Color.Lerp(GalleryFloorBase, GalleryWallBase,
                    SmoothStep01((v - 0.28f) / 0.12f));
            return Color.Lerp(GalleryFloor, GalleryFloorBase, SmoothStep01(v / 0.28f));
        }

        private static Color SkyGradientColor(float v)
        {
            if (v >= 0.72f)
                return Color.Lerp(SkyMid, SkyZenith, SmoothStep01((v - 0.72f) / 0.28f));
            if (v >= 0.32f)
                return Color.Lerp(SkyHorizon, SkyMid, SmoothStep01((v - 0.32f) / 0.40f));
            return Color.Lerp(SkyGround, SkyHorizon, SmoothStep01(v / 0.32f));
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static Color32 ToColor32(float r, float g, float b)
        {
            return new Color32(ToByte(r), ToByte(g), ToByte(b), 255);
        }

        private static byte ToByte(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);

        private static GameObject BuildReturnButton(Transform camera, Vector3 localPosition)
        {
            GameObject root = CreateCanvasRoot("Mode Return", new Vector2(160, 60));
            root.transform.SetParent(camera, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * 0.001f;
            var canvas = root.GetComponent<Canvas>();
            canvas.worldCamera = camera.GetComponent<Camera>();

            var surfaceRect = Rect("Surface", root.transform, Vector2.zero, Vector2.zero);
            surfaceRect.anchorMin = Vector2.zero;
            surfaceRect.anchorMax = Vector2.one;
            surfaceRect.offsetMin = Vector2.zero;
            surfaceRect.offsetMax = Vector2.zero;
            var background = surfaceRect.gameObject.AddComponent<Image>();
            background.sprite = rounded;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = RoundedPixelsPerUnitMultiplier;
            background.color = Panel;
            var button = surfaceRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Text("Label", surfaceRect, "上一页", Vector2.zero, new Vector2(150, 48), 26, Ink);
            AddEnhanced(button);
            var bridge = root.AddComponent<PicoButtonClickBridge>();
            bridge.Configure(new[] { button });
            return root;
        }

        private static void AddEnhanced(Button button)
        {
            if (button.GetComponent<PXR_EnhancedButton>() == null)
                button.gameObject.AddComponent<PXR_EnhancedButton>();
        }

        private static int ScaleIcon(float value) => Mathf.RoundToInt(value * iconScale);

        private static Sprite BuildIcon(string fileName, bool mr)
        {
            string path = UiFolder + "/" + fileName;
            iconScale = IconTextureSize / 128f;
            var texture = new Texture2D(IconTextureSize, IconTextureSize, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < IconTextureSize; y++)
            for (int x = 0; x < IconTextureSize; x++)
                texture.SetPixel(x, y, clear);

            Color line = new Color(0.88f, 0.95f, 0.9f, 1f);
            if (mr)
            {
                DrawRect(texture, 18, 38, 92, 52, line, 5);
                DrawCircle(texture, 46, 64, 12, line, 4);
                DrawCircle(texture, 82, 64, 12, line, 4);
                DrawLine(texture, 64, 40, 64, 30, line, 5);
                DrawLine(texture, 64, 30, 64, 88, line, 3);
                DrawLine(texture, 30, 88, 98, 88, line, 3);
            }
            else
            {
                DrawRect(texture, 20, 44, 78, 54, line, 5);
                DrawRect(texture, 34, 28, 78, 54, line, 5);
                DrawLine(texture, 38, 72, 58, 52, line, 4);
                DrawLine(texture, 58, 52, 76, 72, line, 4);
                DrawLine(texture, 76, 72, 96, 46, line, 4);
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DrawRect(Texture2D texture, int x, int y, int width, int height,
            Color color, int thickness)
        {
            DrawLine(texture, x, y, x + width, y, color, thickness);
            DrawLine(texture, x, y, x, y + height, color, thickness);
            DrawLine(texture, x + width, y, x + width, y + height, color, thickness);
            DrawLine(texture, x, y + height, x + width, y + height, color, thickness);
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1,
            Color color, int thickness)
        {
            x0 = ScaleIcon(x0);
            y0 = ScaleIcon(y0);
            x1 = ScaleIcon(x1);
            y1 = ScaleIcon(y1);
            thickness = Mathf.Max(1, ScaleIcon(thickness));
            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                for (int oy = -thickness / 2; oy <= thickness / 2; oy++)
                for (int ox = -thickness / 2; ox <= thickness / 2; ox++)
                {
                    int px = x0 + ox;
                    int py = y0 + oy;
                    if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                        texture.SetPixel(px, py, color);
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * error;
                if (e2 >= dy) { error += dy; x0 += sx; }
                if (e2 <= dx) { error += dx; y0 += sy; }
            }
        }

        private static void DrawCircle(Texture2D texture, int centerX, int centerY, int radius,
            Color color, int thickness)
        {
            centerX = ScaleIcon(centerX);
            centerY = ScaleIcon(centerY);
            radius = ScaleIcon(radius);
            thickness = Mathf.Max(1, ScaleIcon(thickness));
            int steps = Mathf.Max(24, radius * 4);
            for (int i = 0; i < steps; i++)
            {
                float angle = Mathf.PI * 2f * i / steps;
                float x = centerX + Mathf.Cos(angle) * radius;
                float y = centerY + Mathf.Sin(angle) * radius;
                for (int oy = -thickness / 2; oy <= thickness / 2; oy++)
                for (int ox = -thickness / 2; ox <= thickness / 2; ox++)
                {
                    int px = Mathf.RoundToInt(x) + ox;
                    int py = Mathf.RoundToInt(y) + oy;
                    if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                        texture.SetPixel(px, py, color);
                }
            }
        }

        private static TMP_Text StretchText(string name, Transform parent, string content,
            float left, float bottom, float right, float top, float fontSize, Color color)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position,
            Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size,
            Color color, bool round = false)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (round)
            {
                image.sprite = rounded;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = RoundedPixelsPerUnitMultiplier;
            }
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, string content,
            Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException("Missing serialized field: " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException("Missing serialized array: " + field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
