using System;
using System.IO;
using System.Linq;
using ByteDance.PICO.Interaction;
using PicShow.PicoInteraction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PicShow.Editor
{
    public static class GallerySceneBuilder
    {
        private const string Root = "Assets/PicShow";
        public const string PrefabPath = Root + "/Prefabs/FourRealmsGallery.prefab";
        public const string CatalogPath = Root + "/Data/GalleryCatalog.asset";
        // The rounded sprite is generated at 512 px with a 160 px nine-slice border; this
        // multiplier keeps the on-screen corner radius identical to the previous 64 px sprite.
        private const float RoundedPixelsPerUnitMultiplier = 4.4f;
        private static TMP_FontAsset font;
        private static Sprite rounded;
        private static readonly Color Ink = Hex("#EDF1E9");
        private static readonly Color Muted = Hex("#ACB9B4");
        private static readonly Color Accent = Hex("#C1DECE");
        private static readonly string[] CategoryNames = { "水彩治愈", "现代都市", "赛博朋克", "超现实奇幻" };
        private static readonly string[] Titles = { "雅典学院", "最后的晚餐", "倒牛奶的女仆", "宫娥", "林荫道" };
        private static readonly string[] Descriptions = {
            "让思想相遇，让光照进古典长廊。",
            "一场跨越时间的相聚，一次全新的凝望。",
            "在日常的微光里，发现安静的美。",
            "目光交错之间，走进画中的另一重空间。",
            "沿着光影铺就的小路，慢慢走进风景。"
        };
        private static readonly string[] StyleDescriptions = {
            "轻盈笔触 · 柔和光影 · 温暖治愈",
            "当代空间 · 都市秩序 · 简练线条",
            "霓虹夜色 · 未来城市 · 电子幻想",
            "梦境漫游 · 奇幻色彩 · 无界想象"
        };
        private const string InterfaceText =
            "PicShow万象画境四境画廊04风格20作品经典的另一种可能一幅经典，四种意境" +
            "在光影与想象之间，找到此刻喜欢的风景。点击侧图或箭头切换方向键浏览本地精选画作" +
            "水彩治愈现代都市赛博朋克超现实奇幻";

        [MenuItem("PicShow/Create Four Realms Gallery")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before building.");
            if (UnityEngine.Object.FindAnyObjectByType<GalleryCarousel>() != null)
                throw new InvalidOperationException("The scene already contains a gallery. Edit the existing instance.");
            GalleryInteractionSceneMigrator.MigrateActiveScene();
            EnsureFolder(Root + "/Data");
            EnsureFolder(Root + "/UI");
            EnsureFolder(Root + "/Prefabs");
            var catalog = BuildCatalog();
            rounded = BuildRoundedSprite();
            font = BuildFont();

            var root = Rect("Four Realms Gallery", null, Vector2.zero, new Vector2(2580, 1660));
            root.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create PicShow gallery");
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            root.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;
            root.gameObject.AddComponent<GraphicRaycaster>();
            root.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.localScale = Vector3.one * 0.001f;
            var camera = Camera.main;
            root.position = camera != null ? camera.transform.position + camera.transform.forward * 2f : new Vector3(0, 1.36f, 2);
            root.rotation = camera != null ? camera.transform.rotation : Quaternion.identity;

            Text("Eyebrow", root, "万象画境 · 经典的另一种可能", new Vector2(0, 600), new Vector2(1200, 48), 23, Accent);
            Text("Heading", root, "一幅经典，四种意境", new Vector2(0, 528), new Vector2(1500, 90), 64, Ink);
            Text("Introduction", root, "在光影与想象之间，找到此刻喜欢的风景。", new Vector2(0, 460), new Vector2(1600, 50), 26, Muted);

            var tabBar = Rect("Style bar", root, new Vector2(0, 363), new Vector2(1290, 104));
            Image("Tab rail", tabBar, Vector2.zero, tabBar.sizeDelta, Hex("#24332E"), true);
            var tabs = new GalleryCarousel.Tab[4];
            for (int i = 0; i < 4; i++)
            {
                Button tab = Button("Style " + CategoryNames[i], tabBar, new Vector2((i - 1.5f) * 318, 0), new Vector2(310, 86), Color.clear);
                tabs[i] = new GalleryCarousel.Tab
                {
                    Button = tab,
                    Background = tab.GetComponent<Image>(),
                    Label = Text("Label", tab.transform, CategoryNames[i], Vector2.zero, new Vector2(300, 76), 29, Ink)
                };
            }
            TMP_Text style = Text("Style description", root, "", new Vector2(0, 275), new Vector2(1400, 55), 24, Muted);
            // A separate stage keeps card sibling ordering independent of the navigation.
            var stage = Rect("Three card stage", root, new Vector2(0, -87), new Vector2(2500, 720));
            var cards = new GalleryCarousel.Card[3];
            for (int i = 0; i < 3; i++) cards[i] = BuildCard(stage, i);
            Image rule = Image("Accent line", root, new Vector2(0, -451), new Vector2(72, 3), Accent);
            TMP_Text meta = Text("Current style and position", root, "", new Vector2(0, -485), new Vector2(1250, 46), 23, Accent);
            TMP_Text title = Text("Current title", root, "", new Vector2(0, -545), new Vector2(1500, 78), 48, Ink);
            TMP_Text description = Text("Current description", root, "", new Vector2(0, -609), new Vector2(1900, 55), 25, Muted);
            var dots = new GalleryCarousel.PositionDot[5];
            for (int i = 0; i < 5; i++)
            {
                var dot = Button("Work " + (i + 1), root, new Vector2((i - 2) * 56, -695), new Vector2(54, 76), Color.clear);
                dots[i] = new GalleryCarousel.PositionDot
                {
                    Button = dot,
                    Marker = Image("Marker", dot.transform, Vector2.zero, new Vector2(10, 10), Muted, true)
                };
            }
            var carousel = root.gameObject.AddComponent<GalleryCarousel>();
            carousel.Configure(catalog, cards, tabs, dots, style, meta, title, description, rule);
            ConfigurePicoInteraction(root.gameObject, carousel, stage);
            root.gameObject.SetActive(true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("PicShow gallery saved in the active scene and as an editable prefab.");
        }

        [MenuItem("PicShow/Upgrade PICO Multimodal Interaction")]
        public static void UpgradePicoInteraction()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before upgrading.");

            GalleryInteractionSceneMigrator.MigrateActiveScene();
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var carousel = root.GetComponent<GalleryCarousel>();
                var stage = root.transform.Find("Three card stage") as RectTransform;
                if (carousel == null || stage == null)
                    throw new InvalidOperationException("The gallery prefab is missing its carousel or card stage.");

                ConfigurePicoInteraction(root, carousel, stage);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        // Prefab-only entry point used by batchmode to apply PICO bindings without touching scenes.
        public static void RefreshPicoBindings()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before refreshing PICO bindings.");

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var carousel = root.GetComponent<GalleryCarousel>();
                var stage = root.transform.Find("Three card stage") as RectTransform;
                if (carousel == null || stage == null)
                    throw new InvalidOperationException("The gallery prefab is missing its carousel or card stage.");

                ConfigurePicoInteraction(root, carousel, stage);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("PicShow/Apply Head Follow")]
        public static void ApplyHeadFollow()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before applying head follow.");

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var follow = root.GetComponent<LazyFollow>() ?? root.AddComponent<LazyFollow>();
                follow.target = null; // Falls back to Camera.main when the component is enabled.
                follow.targetOffset = new Vector3(0f, 0f, 2f);
                follow.followInLocalSpace = false;
                follow.applyTargetInLocalSpace = false;
                follow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
                follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
                follow.snapOnEnable = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigurePicoInteraction(
            GameObject root,
            GalleryCarousel carousel,
            RectTransform stage)
        {
            // All three card slots are recycled by the carousel, so every slot needs the
            // PICO enhanced button; the center card stays non-interactable at runtime.
            Button[] interactiveButtons = carousel.Tabs.Select(tab => tab.Button)
                .Concat(carousel.Dots.Select(dot => dot.Button))
                .Concat(carousel.Cards.Select(card => card.Button))
                .ToArray();

            foreach (Button button in interactiveButtons)
            {
                if (button.GetComponent<PXR_EnhancedButton>() == null)
                    button.gameObject.AddComponent<PXR_EnhancedButton>();
            }

            var adapter = root.GetComponent<GalleryPicoInteractionAdapter>() ??
                root.AddComponent<GalleryPicoInteractionAdapter>();
            adapter.Configure(interactiveButtons);

            var controller = root.GetComponent<GalleryControllerNavigator>() ??
                root.AddComponent<GalleryControllerNavigator>();
            controller.Configure(
                carousel,
                FindActionReference("XRI Left Locomotion", "Move"),
                FindActionReference("XRI Right Locomotion", "Move"));

            var swipeSurface = stage.GetComponent<Image>() ?? stage.gameObject.AddComponent<Image>();
            swipeSurface.color = Color.clear;
            swipeSurface.raycastTarget = true;
            var legacySwipe = stage.GetComponent<PXR_SwipeFrame>();
            if (legacySwipe != null)
                UnityEngine.Object.DestroyImmediate(legacySwipe);
            var swipe = stage.GetComponent<GallerySwipeNavigator>() ??
                stage.gameObject.AddComponent<GallerySwipeNavigator>();
            swipe.Configure(carousel, 0.1f);
        }

        private static InputActionReference FindActionReference(string mapName, string actionName)
        {
            const string path =
                "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/XRI Default Input Actions.inputactions";
            var reference = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<InputActionReference>()
                .FirstOrDefault(candidate =>
                    candidate.action != null &&
                    candidate.action.actionMap.name == mapName &&
                    candidate.action.name == actionName);
            if (reference == null)
                throw new InvalidOperationException($"Missing input action reference: {mapName}/{actionName}");
            return reference;
        }

        private static GalleryCarousel.Card BuildCard(Transform parent, int index)
        {
            var root = Rect("Card " + index, parent, Vector2.zero, new Vector2(932, 632));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            Image("Shadow", root, new Vector2(0, -20), new Vector2(956, 651), new Color(0, 0, 0, 0.32f), true);
            Image border = Image("Frame border", root, Vector2.zero, new Vector2(932, 632), Accent, true);
            Image face = Image("Frame", root, Vector2.zero, new Vector2(928, 628), Hex("#27362F"), true);
            var button = root.gameObject.AddComponent<Button>();
            face.raycastTarget = true;
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            RectTransform artRoot = Rect("Artwork", root, Vector2.zero, new Vector2(900, 600));
            RawImage art = artRoot.gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            return new GalleryCarousel.Card
            {
                Root = root, Group = group, Artwork = art, Border = border, Button = button,
                Caption = Text("Caption", root, "", new Vector2(-225, -365), new Vector2(470, 58), 27, Muted, TextAlignmentOptions.MidlineLeft),
                Number = Text("Position", root, "", new Vector2(338, -365), new Vector2(235, 58), 22, Muted, TextAlignmentOptions.MidlineRight)
            };
        }

        private static GalleryCatalog BuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GalleryCatalog>(CatalogPath);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<GalleryCatalog>();
            string[] colors = { "#C1DECE", "#B9D2E6", "#E9B4E7", "#D0B9EC" };
            catalog.Categories = new GalleryCatalog.Category[4];
            for (int c = 0; c < 4; c++)
            {
                var category = new GalleryCatalog.Category
                {
                    Name = CategoryNames[c], Description = StyleDescriptions[c],
                    Accent = Hex(colors[c]), Works = new GalleryCatalog.Work[5]
                };
                for (int w = 0; w < 5; w++)
                {
                    string path = $"Assets/Pic/{CategoryNames[c]}/{w + 1:00}_{Titles[w]}_{CategoryNames[c]}.jpg";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new FileNotFoundException(path);
                    importer.textureType = TextureImporterType.Default;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = 4096;
                    importer.isReadable = false;
                    importer.mipmapEnabled = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    {
                        name = "Android", overridden = true, maxTextureSize = 4096,
                        format = TextureImporterFormat.ASTC_6x6
                    });
                    importer.SaveAndReimport();
                    category.Works[w] = new GalleryCatalog.Work
                    {
                        Title = Titles[w], Description = Descriptions[w],
                        Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                    };
                }
                catalog.Categories[c] = category;
            }
            if (!catalog.Validate(out string error)) throw new InvalidOperationException(error);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        private static TMP_FontAsset BuildFont()
        {
            const string path = Root + "/Fonts/GalleryChinese SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansSC-Regular.ttf");
            var asset = TMP_FontAsset.CreateFontAsset(source, 80, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            string characters = InterfaceText + string.Concat(Titles) + string.Concat(Descriptions) +
                string.Concat(StyleDescriptions) + new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray()) + "‹›—·";
            if (!asset.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Missing font glyphs: " + missing);
            asset.name = "GalleryChinese SDF";
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = "GalleryChinese Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (Texture2D texture in asset.atlasTextures)
            {
                texture.name = "GalleryChinese Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Sprite BuildRoundedSprite()
        {
            const string path = Root + "/UI/RoundedPanel.png";
            const int size = 512;
            const int border = 160;
            const float center = 255.5f;
            const float cornerRadius = 131.5f;
            const float flatHalf = center - cornerRadius;
            const float antialias = 8f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to the rounded rect boundary: opaque inside the radius,
                // transparent outside, with an 8 px ramp that maps to about one screen pixel.
                var delta = new Vector2(
                    Mathf.Max(Mathf.Abs(x - center) - flatHalf, 0),
                    Mathf.Max(Mathf.Abs(y - center) - flatHalf, 0));
                float alpha = (cornerRadius - delta.magnitude) / antialias + 0.5f;
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(alpha)));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("PicShow/Upgrade UI Sharpness")]
        public static void UpgradeSharpness()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before upgrading UI sharpness.");

            rounded = BuildRoundedSprite();
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.sprite == rounded)
                        image.pixelsPerUnitMultiplier = RoundedPixelsPerUnitMultiplier;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("PicShow rounded UI sprite upgraded to 512 px.");
        }

        private static void EnsureEventSystem()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) return;
            var go = new GameObject("Gallery EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create gallery EventSystem");
            var module = go.GetComponent<XRUIInputModule>();
            module.enableMouseInput = true;
            module.enableTouchInput = true;
            module.enableXRInput = true;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool round = false)
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

        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            Image image = Image(name, parent, position, size, color, true);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static TMP_Text Text(string name, Transform parent, string content, Vector2 position,
            Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Midline)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
