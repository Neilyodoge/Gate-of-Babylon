using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 天赋树的轻量程序化底图。正式切图可通过同一入口替换，
    /// 功能验证阶段不依赖概念图直接裁切。
    /// </summary>
    public static class SpiritTalentTreeVisuals
    {
        public static readonly Color Paper =
            new(0.94f, 0.88f, 0.72f, 0.99f);
        public static readonly Color PaperInset =
            new(0.88f, 0.81f, 0.64f, 0.96f);
        public static readonly Color Ink =
            new(0.25f, 0.18f, 0.11f, 1f);
        public static readonly Color InkDim =
            new(0.42f, 0.36f, 0.26f, 0.75f);
        public static readonly Color Ember =
            new(0.91f, 0.38f, 0.13f, 1f);
        public static readonly Color Moss =
            new(0.43f, 0.53f, 0.24f, 1f);
        public static readonly Color Teal =
            new(0.25f, 0.55f, 0.56f, 1f);

        private static Sprite _circle;
        private static Sprite _star;
        private static Sprite _ultimate;
        private static Sprite _roundedPanel;
        private static Sprite _pill;

        public static Sprite Circle =>
            _circle ??= CreateRadial(
                "TalentCircle",
                radius: _ => 0.82f);

        public static Sprite Star =>
            _star ??= CreateRadial(
                "TalentRoundedStar",
                radius: angle =>
                    0.53f +
                    0.32f * Mathf.Pow(
                        Mathf.Abs(Mathf.Cos(angle * 2f)),
                        3f));

        public static Sprite Ultimate =>
            _ultimate ??= CreateUltimate();

        public static Sprite RoundedPanel =>
            _roundedPanel ??= CreateRoundedRect(
                "TalentRoundedPanel",
                128,
                0.14f,
                26f);

        public static Sprite Pill =>
            _pill ??= CreateRoundedRect(
                "TalentPill",
                64,
                0.42f,
                22f);

        public static Color RouteColor(int branch)
        {
            return branch switch
            {
                0 => Ember,
                1 => Moss,
                _ => Teal
            };
        }

        private static Sprite CreateRadial(
            string name,
            Func<float, float> radius)
        {
            const int size = 96;
            var texture = NewTexture(name, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = PixelPoint(x, y, size);
                    float angle = Mathf.Atan2(point.y, point.x);
                    float edge = radius(angle);
                    float alpha = Mathf.Clamp01(
                        (edge - point.magnitude) * size * 0.35f);
                    pixels[y * size + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }
            return Finish(texture, pixels);
        }

        private static Sprite CreateUltimate()
        {
            const int size = 128;
            var texture = NewTexture("TalentUltimate", size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = PixelPoint(x, y, size);
                    float distance = point.magnitude;
                    float angle = Mathf.Atan2(point.y, point.x);
                    float starEdge =
                        0.48f +
                        0.27f * Mathf.Pow(
                            Mathf.Abs(Mathf.Cos(angle * 2f)),
                            3f);
                    float star = Mathf.Clamp01(
                        (starEdge - distance) * size * 0.3f);
                    float ring = Mathf.Clamp01(
                        (0.04f - Mathf.Abs(distance - 0.88f)) *
                        size * 0.35f);
                    pixels[y * size + x] =
                        new Color(
                            1f,
                            1f,
                            1f,
                            Mathf.Max(star, ring));
                }
            }
            return Finish(texture, pixels);
        }

        private static Sprite CreateRoundedRect(
            string name,
            int size,
            float radius,
            float border)
        {
            var texture = NewTexture(name, size);
            var pixels = new Color[size * size];
            Vector2 half = new(0.92f - radius, 0.92f - radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = PixelPoint(x, y, size);
                    Vector2 q = new(
                        Mathf.Abs(point.x) - half.x,
                        Mathf.Abs(point.y) - half.y);
                    float outside = new Vector2(
                        Mathf.Max(q.x, 0f),
                        Mathf.Max(q.y, 0f)).magnitude;
                    float inside = Mathf.Min(
                        Mathf.Max(q.x, q.y),
                        0f);
                    float distance = outside + inside - radius;
                    float alpha = Mathf.Clamp01(
                        -distance * size * 0.45f);
                    pixels[y * size + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        private static Texture2D NewTexture(string name, int size)
        {
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }

        private static Vector2 PixelPoint(int x, int y, int size)
        {
            return new Vector2(
                (x + 0.5f) / size * 2f - 1f,
                (y + 0.5f) / size * 2f - 1f);
        }

        private static Sprite Finish(
            Texture2D texture,
            Color[] pixels)
        {
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            int size = texture.width;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
