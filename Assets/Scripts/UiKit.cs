using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LockSimulator
{
    /// <summary>程序化 UI 的构件。位置全部用父级内的 0-1 锚点，偏移为 0。</summary>
    public static class UiKit
    {
        static Font font;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Font.CreateDynamicFontFromOSFont(new[] { "PingFang SC", "Heiti SC", "Noto Sans CJK SC", "Microsoft YaHei", "sans-serif" }, 32);
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>在区域内放一个固定宽高比的盒子，内部可以用比例坐标画出不变形的图形。</summary>
        public static RectTransform FitBox(Transform parent, string name, float x0, float y0, float x1, float y1, float ratio)
        {
            var area = Rect(name + "Area", parent, x0, y0, x1, y1);
            var inner = Rect(name, area, 0f, 0f, 1f, 1f);
            var fitter = inner.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = ratio;
            return inner;
        }

        public static Image Picture(Transform parent, string name, Sprite sprite, Color color,
            float x0 = 0f, float y0 = 0f, float x1 = 1f, float y1 = 1f, bool tiled = false, bool raycast = false)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (tiled) image.type = Image.Type.Tiled;
            return image;
        }

        public static RectTransform Panel(Transform parent, string name, Color color,
            float x0 = 0f, float y0 = 0f, float x1 = 1f, float y1 = 1f, bool round = true)
        {
            var image = Picture(parent, name, round ? Art.RoundedBorder : Art.Flat, color, x0, y0, x1, y1);
            if (round) image.type = Image.Type.Sliced;
            return image.rectTransform;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color color,
            float x0 = 0f, float y0 = 0f, float x1 = 1f, float y1 = 1f)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var t = rect.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Color background, Color foreground, int size,
            float x0 = 0f, float y0 = 0f, float x1 = 1f, float y1 = 1f)
        {
            var rect = Panel(parent, name, background, x0, y0, x1, y1);
            rect.GetComponent<Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            SetButtonColor(button, background);
            var text = Label(rect, "Label", label, size, TextAnchor.MiddleCenter, foreground);
            text.rectTransform.offsetMin = new Vector2(10f, 4f);
            text.rectTransform.offsetMax = new Vector2(-10f, -4f);
            return button;
        }

        public static void SetButtonColor(Button button, Color background)
        {
            // Button 的颜色过渡会与 Image.color 相乘，所以图片保持白色，颜色只由过渡提供。
            button.targetGraphic.color = Color.white;
            var colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.10f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Color.Lerp(background, Color.black, 0.45f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        public static Text ButtonLabel(Button button) { return button.GetComponentInChildren<Text>(); }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }
}
