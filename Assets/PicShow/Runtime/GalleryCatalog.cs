using System;
using UnityEngine;

namespace PicShow
{
    [CreateAssetMenu(menuName = "PicShow/Gallery Catalog")]
    public sealed class GalleryCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Work
        {
            public string Title;
            [TextArea] public string Description;
            public Texture2D Texture;
        }

        [Serializable]
        public sealed class Category
        {
            public string Name;
            public string Description;
            public Color Accent = Color.white;
            public Work[] Works = Array.Empty<Work>();
        }

        public Category[] Categories = Array.Empty<Category>();

        public bool Validate(out string error)
        {
            error = null;
            if (Categories == null || Categories.Length != 4)
                error = "The gallery requires four categories.";
            else
            {
                foreach (var category in Categories)
                {
                    if (category == null || string.IsNullOrWhiteSpace(category.Name) ||
                        category.Works == null || category.Works.Length != 5)
                    {
                        error = "Every category requires a name and five works.";
                        break;
                    }
                    for (int i = 0; i < category.Works.Length; i++)
                    {
                        var work = category.Works[i];
                        if (work == null || work.Texture == null || string.IsNullOrWhiteSpace(work.Title))
                            error = "Every work requires a texture and a title.";
                        else if (work.Title != Categories[0].Works[i]?.Title)
                            error = "Work ordering must match across categories.";
                        if (error != null) break;
                    }
                    if (error != null) break;
                }
            }
            return error == null;
        }
    }
}
