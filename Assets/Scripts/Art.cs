using System;
using System.Collections.Generic;
using UnityEngine;

namespace LockSimulator
{
    /// <summary>运行时生成的贴图。灰度贴图通过 Image.color 着色，所以一张图可以做出黄铜/钢/暗色等多种材质。</summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Sprite Build(string key, int w, int h, Func<float, float, Color> pixel,
            Vector4 border = default(Vector4), TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            Sprite sprite;
            if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = wrap;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            cache[key] = sprite;
            return sprite;
        }

        static float Hash(float x)
        {
            float s = Mathf.Sin(x * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        static Color Gray(float g, float a = 1f) { return new Color(g, g, g, a); }

        public static Sprite Flat { get { return Build("flat", 4, 4, (u, v) => Color.white); } }

        /// <summary>沿 x 方向明暗的圆柱（竖直的销子、手柄）。</summary>
        public static Sprite Cylinder
        {
            get
            {
                return Build("cyl", 64, 4, (u, v) =>
                {
                    float body = Mathf.Lerp(0.38f, 1f, Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.7f));
                    float spec = Mathf.Exp(-Mathf.Pow((u - 0.30f) / 0.07f, 2f)) * 0.30f;
                    return Gray(Mathf.Clamp01(body * 0.92f + spec));
                });
            }
        }

        /// <summary>沿 y 方向明暗的圆柱（横着的拨针、扳手）。</summary>
        public static Sprite CylinderH
        {
            get
            {
                return Build("cylh", 4, 64, (u, v) =>
                {
                    float body = Mathf.Lerp(0.38f, 1f, Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.7f));
                    float spec = Mathf.Exp(-Mathf.Pow((v - 0.68f) / 0.07f, 2f)) * 0.30f;
                    return Gray(Mathf.Clamp01(body * 0.92f + spec));
                });
            }
        }

        /// <summary>拉丝金属，可平铺。</summary>
        public static Sprite Brushed
        {
            get
            {
                return Build("brushed", 128, 64, (u, v) =>
                {
                    int row = Mathf.FloorToInt(v * 64f);
                    float g = 0.78f + 0.12f * Hash(row) + 0.10f * Hash(row * 3.7f + Mathf.Floor(u * 10f));
                    return Gray(g);
                }, default(Vector4), TextureWrapMode.Repeat);
            }
        }

        public static Sprite VGrad(Color top, Color bottom)
        {
            string key = "vg" + ColorUtility.ToHtmlStringRGBA(top) + ColorUtility.ToHtmlStringRGBA(bottom);
            return Build(key, 4, 128, (u, v) => Color.Lerp(bottom, top, v));
        }

        public static Sprite Disc
        {
            get
            {
                return Build("disc", 128, 128, (u, v) =>
                {
                    float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    return Gray(1f, Mathf.Clamp01((1f - d) * 64f));
                });
            }
        }

        public static Sprite Ring
        {
            get
            {
                return Build("ring", 128, 128, (u, v) =>
                {
                    float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    return Gray(1f, Mathf.Clamp01((1f - d) * 64f) * Mathf.Clamp01((d - 0.80f) * 64f));
                });
            }
        }

        public static Sprite Glow
        {
            get
            {
                return Build("glow", 128, 128, (u, v) =>
                {
                    float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    return Gray(1f, a * a);
                });
            }
        }

        public static Sprite Keyhole
        {
            get
            {
                return Build("keyhole", 128, 128, (u, v) =>
                {
                    float dx = u - 0.5f, dy = v - 0.66f;
                    float circle = 0.20f - Mathf.Sqrt(dx * dx + dy * dy);
                    float halfWidth = Mathf.Lerp(0.05f, 0.11f, Mathf.InverseLerp(0.12f, 0.66f, v));
                    float stem = Mathf.Min(halfWidth - Mathf.Abs(u - 0.5f), Mathf.Min(v - 0.12f, 0.62f - v + 0.2f));
                    return Gray(1f, Mathf.Clamp01(Mathf.Max(circle, stem) * 64f + 0.5f));
                });
            }
        }

        public static Sprite Star
        {
            get
            {
                return Build("star", 96, 96, (u, v) =>
                {
                    float x = (u - 0.5f) * 2f, y = (v - 0.46f) * 2f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float phi = Mathf.Atan2(x, y);
                    if (phi < 0f) phi += Mathf.PI * 2f;
                    float sector = Mathf.PI / 5f;
                    float local = Mathf.Repeat(phi, sector * 2f);
                    if (local > sector) local = sector * 2f - local;
                    // 外顶点在角度 0，内顶点在 sector，取两点连线与射线的交点
                    const float inner = 0.42f;
                    float ox = 0f, oy = 1f;
                    float ix = Mathf.Sin(sector) * inner, iy = Mathf.Cos(sector) * inner;
                    float dx = Mathf.Sin(local), dy = Mathf.Cos(local);
                    float ex = ix - ox, ey = iy - oy;
                    float denom = dx * ey - dy * ex;
                    float edge = Mathf.Abs(denom) < 1e-5f ? 1f : (ox * ey - oy * ex) / denom;
                    return Gray(1f, Mathf.Clamp01((edge - r) * 40f));
                });
            }
        }

        /// <summary>九宫格圆角面板，边缘比内部亮一点，形成一圈细描边。</summary>
        public static Sprite RoundedBorder
        {
            get
            {
                const int size = 64;
                const float radius = 20f;
                return Build("rounded", size, size, (u, v) =>
                {
                    float px = (u - 0.5f) * size, py = (v - 0.5f) * size;
                    float qx = Mathf.Abs(px) - (size * 0.5f - radius);
                    float qy = Mathf.Abs(py) - (size * 0.5f - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                        + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    float g = d > -2.2f ? 1f : 0.86f;
                    return Gray(g, alpha);
                }, new Vector4(radius, radius, radius, radius));
            }
        }
    }
}
