using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LockSimulator
{
    /// <summary>
    /// 弹子锁的剖面视图 + 拨针区 + 张力条。所有几何都在 1.5 × 1.55 的“舞台单位”里，
    /// 宿主是固定宽高比的盒子，所以窗口怎么缩放都不会变形。
    /// 上部（y ≥ 0.55）是剖面；中部是拨针区；底部是张力条。
    /// </summary>
    public class LockStage
    {
        public const float W = 1.5f, H = 1.55f, VY = 0.55f;
        public const float Travel = 0.22f;     // 弹子满行程对应的剖面长度
        public const float Shear = 0.55f;      // 剪切线（剖面局部坐标）
        public const float Pitch = 0.17f;
        public const float LaneBottom = 0.20f, LaneTop = 0.50f;
        const float KeyBottom = 0.27f, ChamberTop = 0.88f;
        const float DriverLen = 0.17f, SpoolLen = 0.20f, PinW = 0.085f, ChamberW = 0.115f;
        const float TensionY = 0.075f, TensionX0 = 0.17f, TensionX1 = 1.33f;
        const float PadTop = 0.60f, TensionTop = 0.15f;
        const int Coils = 9;

        static readonly Color Steel = new Color(0.62f, 0.68f, 0.76f);
        static readonly Color Brass = new Color(0.96f, 0.76f, 0.36f);
        static readonly Color Cyan = new Color(0.25f, 0.85f, 0.98f);
        static readonly Color Amber = new Color(1f, 0.78f, 0.30f);
        static readonly Color Good = new Color(0.40f, 0.95f, 0.65f);
        static readonly Color Bad = new Color(1f, 0.38f, 0.34f);
        static readonly Color Dark = new Color(0.02f, 0.03f, 0.05f);

        class PinView
        {
            public float x;
            public Image key, driver, cap1, waist, cap2, bindMark, laneDot, stateDot;
            public readonly List<RectTransform> coils = new List<RectTransform>();
            public bool spool;
        }

        public Action<float> TensionChanged;
        public Action<int, float> PickChanged;
        public Action PickEnded;

        readonly RectTransform host;
        PinView[] views;
        int n;
        RectTransform cover, pick, wrench, keyhole, fingerRing, knob, tensionFill;
        Image fillImage;
        float tipX = 0.3f, tipY = KeyBottom - 0.04f, dialAngle;

        // 输入
        const int NoPointer = int.MinValue;
        int tensionPointer = NoPointer, pickPointer = NoPointer, lane = -1;

        public LockStage(RectTransform host) { this.host = host; }

        public static float PinX(int i, int count) { return 0.9f + (i - (count - 1) * 0.5f) * Pitch; }
        static float V(float y) { return y + VY; }

        static void Box(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0 / W, y0 / H);
            r.anchorMax = new Vector2(x1 / W, y1 / H);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static void At(RectTransform r, float cx, float cy, float w, float h) { Box(r, cx - w * 0.5f, cy - h * 0.5f, cx + w * 0.5f, cy + h * 0.5f); }

        Image Img(Transform parent, string name, Sprite sprite, Color color, bool tiled = false, bool sliced = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (tiled) image.type = Image.Type.Tiled;
            if (sliced) image.type = Image.Type.Sliced;
            return image;
        }

        Image Place(Image image, float x0, float y0, float x1, float y1) { Box(image.rectTransform, x0, y0, x1, y1); return image; }

        // ---------------------------------------------------------------- 构建

        public void Rebuild(int pinCount)
        {
            UiKit.Clear(host);
            n = pinCount;
            views = new PinView[n];
            tensionPointer = pickPointer = NoPointer;
            lane = -1;
            tipX = 0.3f; tipY = KeyBottom - 0.04f; dialAngle = 0f;

            Place(Img(host, "ViewBg", Art.RoundedBorder, new Color(0.03f, 0.055f, 0.10f), false, true), 0f, VY, W, H);

            // 壳体、锁芯、钥匙孔
            var housing = Place(Img(host, "Housing", Art.Brushed, new Color(0.62f, 0.67f, 0.75f), true), 0.03f, V(0.55f), 1.47f, V(0.94f));
            Box0(Img(housing.transform, "Shade", Art.VGrad(new Color(1, 1, 1, 0.10f), new Color(0, 0, 0, 0.35f)), Color.white).rectTransform, 0f, 0f, 1f, 1f);
            Place(Img(host, "Body", Art.Brushed, new Color(0.40f, 0.44f, 0.50f), true), 0.03f, V(0.06f), 1.47f, V(0.17f));
            var plug = Place(Img(host, "Plug", Art.Brushed, Brass, true), 0.03f, V(0.17f), 1.47f, V(0.55f));
            Box0(Img(plug.transform, "Shade", Art.VGrad(new Color(1, 1, 1, 0.20f), new Color(0, 0, 0, 0.42f)), Color.white).rectTransform, 0f, 0f, 1f, 1f);
            Place(Img(host, "Keyway", Art.VGrad(new Color(0.01f, 0.015f, 0.025f), new Color(0.06f, 0.07f, 0.09f)), Color.white), 0.03f, V(0.20f), 1.40f, V(0.31f));

            for (int i = 0; i < n; i++)
            {
                float cx = PinX(i, n);
                Place(Img(host, "Chamber" + i, Art.Flat, Dark), cx - ChamberW * 0.5f, V(0.31f), cx + ChamberW * 0.5f, V(ChamberTop + 0.02f));
            }

            // 剪切线
            Place(Img(host, "ShearGlow", Art.Flat, new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f)), 0.03f, V(Shear) - 0.014f, 1.47f, V(Shear) + 0.014f);
            Place(Img(host, "ShearLine", Art.Flat, new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f)), 0.03f, V(Shear) - 0.003f, 1.47f, V(Shear) + 0.003f);
            var shearLabel = UiKit.Label(host, "ShearLabel", "剪切线", 15, TextAnchor.MiddleLeft, new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f));
            Box(shearLabel.rectTransform, 0.06f, V(0.565f), 0.40f, V(0.625f));

            for (int i = 0; i < n; i++) views[i] = BuildPin(i);

            // 盲拨遮罩
            var coverImg = Place(Img(host, "BlindCover", Art.Brushed, new Color(0.20f, 0.23f, 0.28f), true), 0.03f, V(0.06f), 1.47f, V(0.94f));
            cover = coverImg.rectTransform;
            Box0(Img(cover, "Hole", Art.Keyhole, new Color(0.05f, 0.06f, 0.08f, 0.9f)).rectTransform, 0.36f, 0.30f, 0.64f, 0.75f);
            var coverText = UiKit.Label(cover, "Text", "盲拨：只能靠手感、声音和锁芯的转动判断", 20, TextAnchor.MiddleCenter, new Color(0.75f, 0.82f, 0.9f, 0.85f), 0.05f, 0.04f, 0.95f, 0.22f);
            coverText.fontStyle = FontStyle.Normal;
            cover.gameObject.SetActive(false);

            // 前视图：锁芯转角
            At(Img(host, "DialRing", Art.Disc, new Color(0.30f, 0.34f, 0.41f)).rectTransform, 0.21f, V(0.76f), 0.29f, 0.29f);
            At(Img(host, "DialFace", Art.Disc, Brass).rectTransform, 0.21f, V(0.76f), 0.25f, 0.25f);
            At(Img(host, "DialEdge", Art.Ring, new Color(0, 0, 0, 0.35f)).rectTransform, 0.21f, V(0.76f), 0.25f, 0.25f);
            keyhole = Img(host, "DialKeyhole", Art.Keyhole, new Color(0.05f, 0.06f, 0.08f)).rectTransform;
            At(keyhole, 0.21f, V(0.76f), 0.13f, 0.13f);

            // 张力扳手（整体绕锁芯中心旋转）
            wrench = UiKit.Rect("Wrench", host, 0f, 0f, 1f, 1f);
            wrench.pivot = new Vector2(0.07f / W, V(0.20f) / H);
            var steel = new Color(0.72f, 0.77f, 0.85f);
            Place(Img(wrench, "WrenchBar", Art.CylinderH, steel), 0.05f, V(0.186f), 0.44f, V(0.214f));
            Place(Img(wrench, "WrenchHandle", Art.Cylinder, steel), 0.052f, V(0.03f), 0.086f, V(0.205f));

            // 拨针
            var pickImg = Img(host, "Pick", Art.CylinderH, new Color(0.85f, 0.89f, 0.95f));
            pick = pickImg.rectTransform;
            pick.pivot = new Vector2(1f, 0.5f);
            pick.localRotation = Quaternion.Euler(0f, 0f, -4f);

            BuildPad();
            BuildTension();

            var catcher = Img(host, "Input", Art.Flat, new Color(0, 0, 0, 0));
            catcher.raycastTarget = true;
            Box(catcher.rectTransform, 0f, 0f, W, H);
            catcher.gameObject.AddComponent<StageInput>().stage = this;
        }

        PinView BuildPin(int i)
        {
            var v = new PinView { x = PinX(i, n) };
            for (int k = 0; k < Coils; k++)
            {
                var coil = Img(host, "Coil" + i + "_" + k, Art.CylinderH, new Color(0.78f, 0.83f, 0.9f));
                coil.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                v.coils.Add(coil.rectTransform);
            }
            v.key = Img(host, "KeyPin" + i, Art.Cylinder, Brass);
            v.driver = Img(host, "Driver" + i, Art.Cylinder, Steel);
            v.cap1 = Img(host, "SpoolA" + i, Art.Cylinder, Steel);
            v.waist = Img(host, "SpoolW" + i, Art.Cylinder, Steel);
            v.cap2 = Img(host, "SpoolB" + i, Art.Cylinder, Steel);
            v.bindMark = Img(host, "Bind" + i, Art.Flat, Amber);
            v.bindMark.gameObject.SetActive(false);
            return v;
        }

        void BuildPad()
        {
            Place(Img(host, "PadBg", Art.RoundedBorder, new Color(0.05f, 0.085f, 0.14f), false, true), 0f, 0.15f, W, 0.54f);
            for (int i = 0; i < n; i++)
            {
                var v = views[i];
                Place(Img(host, "Lane" + i, Art.RoundedBorder, new Color(0.015f, 0.03f, 0.055f), false, true), v.x - 0.045f, LaneBottom, v.x + 0.045f, LaneTop);
                v.stateDot = Img(host, "State" + i, Art.Disc, Steel);
                At(v.stateDot.rectTransform, v.x, 0.525f, 0.034f, 0.034f);
                v.laneDot = Img(host, "Marker" + i, Art.Disc, Brass);
                var num = UiKit.Label(host, "Num" + i, (i + 1).ToString(), 18, TextAnchor.MiddleCenter, new Color(0.55f, 0.65f, 0.78f));
                At(num.rectTransform, v.x, 0.17f, 0.08f, 0.045f);
            }
            var padHelp = UiKit.Label(host, "PadHelp", "拨针区\n按住右侧一列\n向上拖动，抬起弹子", 18, TextAnchor.MiddleCenter, new Color(0.55f, 0.65f, 0.78f));
            Box(padHelp.rectTransform, 0.03f, 0.22f, 0.40f, 0.48f);
            fingerRing = Img(host, "Finger", Art.Ring, new Color(1f, 1f, 1f, 0.85f)).rectTransform;
            fingerRing.gameObject.SetActive(false);
        }

        void BuildTension()
        {
            Place(Img(host, "TensionBg", Art.RoundedBorder, new Color(0.05f, 0.085f, 0.14f), false, true), 0f, 0f, W, 0.14f);
            var label = UiKit.Label(host, "TensionLabel", "张力", 20, TextAnchor.MiddleCenter, new Color(0.75f, 0.82f, 0.9f));
            Box(label.rectTransform, 0.02f, 0.02f, 0.15f, 0.12f);
            Place(Img(host, "TensionTrack", Art.RoundedBorder, new Color(0.015f, 0.03f, 0.055f), false, true), TensionX0 - 0.03f, TensionY - 0.017f, TensionX1 + 0.03f, TensionY + 0.017f);
            fillImage = Img(host, "TensionFill", Art.RoundedBorder, Cyan, false, true);
            tensionFill = fillImage.rectTransform;
            for (int k = 0; k <= 4; k++)
            {
                float x = Mathf.Lerp(TensionX0, TensionX1, k / 4f);
                Place(Img(host, "Tick" + k, Art.Flat, new Color(1, 1, 1, 0.14f)), x - 0.002f, TensionY - 0.045f, x + 0.002f, TensionY - 0.025f);
            }
            var knobImg = Img(host, "TensionKnob", Art.Disc, new Color(0.88f, 0.92f, 0.98f));
            knob = knobImg.rectTransform;
            var knobCore = Img(knob, "Core", Art.Disc, new Color(0.35f, 0.4f, 0.48f));
            Box0(knobCore.rectTransform, 0.28f, 0.28f, 0.72f, 0.72f);
        }

        static void Box0(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- 每帧刷新

        public void Refresh(PinTumblerSim sim, bool xray, bool hint, float dt, float time)
        {
            if (views == null || sim == null) return;
            dialAngle = Mathf.MoveTowards(dialAngle, sim.angle, dt * 1.5f);
            int bind = sim.BindingIndex();
            cover.gameObject.SetActive(!xray);
            pick.gameObject.SetActive(xray);
            wrench.gameObject.SetActive(xray);

            for (int i = 0; i < n; i++) RefreshPin(sim, i, xray, hint && i == bind, time);

            // 拨针
            float tx = 0.3f, ty = KeyBottom - 0.04f;
            if (sim.selected >= 0)
            {
                var p = sim.pins[sim.selected];
                tx = views[sim.selected].x;
                ty = KeyBottom - 0.012f + (p.state == PinState.Set ? 0f : p.lift * Travel);
            }
            float k = 1f - Mathf.Exp(-dt * 18f);
            tipX = Mathf.Lerp(tipX, tx, k);
            tipY = Mathf.Lerp(tipY, ty, k);
            Box(pick, tipX - 0.95f, V(tipY) - 0.011f, tipX, V(tipY) + 0.011f);

            wrench.localRotation = Quaternion.Euler(0f, 0f, sim.tension * 3f + dialAngle * 8f - sim.kick * 4f);
            keyhole.localRotation = Quaternion.Euler(0f, 0f, -dialAngle * 90f);

            // 张力条
            float kx = Mathf.Lerp(TensionX0, TensionX1, sim.tension);
            At(knob, kx, TensionY, 0.095f, 0.095f);
            Box(tensionFill, TensionX0 - 0.03f, TensionY - 0.012f, kx, TensionY + 0.012f);
            fillImage.color = sim.tension < 0.45f ? Color.Lerp(Cyan, Amber, sim.tension / 0.45f) : Color.Lerp(Amber, Bad, (sim.tension - 0.45f) / 0.55f);

            if (sim.selected >= 0 && lane == sim.selected)
            {
                fingerRing.gameObject.SetActive(true);
                At(fingerRing, views[sim.selected].x, LaneBottom + sim.pickTarget * (LaneTop - LaneBottom), 0.105f, 0.105f);
            }
            else fingerRing.gameObject.SetActive(false);
        }

        void RefreshPin(PinTumblerSim sim, int i, bool xray, bool showBind, float time)
        {
            var pin = sim.pins[i];
            var v = views[i];
            float cx = v.x;
            bool set = pin.state == PinState.Set;
            float keyTop0 = Shear - pin.setH * Travel;
            float lift = set ? pin.setH : pin.lift;
            float keyShift = set ? 0f : pin.lift * Travel;
            float boundary = keyTop0 + lift * Travel;

            // 下弹子
            Place(v.key, cx - PinW * 0.5f, V(KeyBottom + keyShift), cx + PinW * 0.5f, V(keyTop0 + keyShift));
            v.key.color = pin.state == PinState.Overset ? Bad : Brass;

            // 上弹子（蘑菇形有两个帽和一个细腰）
            float len = pin.spool ? SpoolLen : DriverLen;
            float top = boundary + len;
            Color dcol = pin.state == PinState.Set ? Good : pin.state == PinState.FalseSet ? Amber : Steel;
            v.driver.gameObject.SetActive(!pin.spool);
            v.cap1.gameObject.SetActive(pin.spool);
            v.waist.gameObject.SetActive(pin.spool);
            v.cap2.gameObject.SetActive(pin.spool);
            if (!pin.spool)
            {
                Place(v.driver, cx - PinW * 0.5f, V(boundary), cx + PinW * 0.5f, V(top));
                v.driver.color = dcol;
            }
            else
            {
                float cap = 0.06f;
                Place(v.cap1, cx - PinW * 0.5f, V(boundary), cx + PinW * 0.5f, V(boundary + cap));
                Place(v.waist, cx - PinW * 0.26f, V(boundary + cap), cx + PinW * 0.26f, V(top - cap));
                Place(v.cap2, cx - PinW * 0.5f, V(top - cap), cx + PinW * 0.5f, V(top));
                v.cap1.color = v.waist.color = v.cap2.color = dcol;
            }

            // 弹簧
            float span = ChamberTop - top;
            for (int k = 0; k < Coils; k++)
            {
                float cy = top + (k + 0.5f) / Coils * span;
                At(v.coils[k], cx, V(cy), PinW * 1.2f, 0.016f);
            }

            // 卡位提示
            v.bindMark.gameObject.SetActive(showBind && xray && !set);
            if (v.bindMark.gameObject.activeSelf)
            {
                At(v.bindMark.rectTransform, cx, V(Shear), ChamberW + 0.03f, 0.014f);
                v.bindMark.color = new Color(Amber.r, Amber.g, Amber.b, 0.5f + 0.5f * Mathf.Sin(time * 6f));
            }

            // 拨针区标记
            // 盲拨时标记只显示手指自己的位置，不能泄露弹子的真实高度。
            float shown = xray ? lift : (sim.selected == i ? sim.pickTarget : 0f);
            float my = LaneBottom + shown * (LaneTop - LaneBottom);
            At(v.laneDot.rectTransform, cx, my, 0.07f, 0.07f);
            v.laneDot.color = xray ? (pin.state == PinState.Overset ? Bad : set ? Good : Brass) : Brass;
            Color sc = new Color(0.3f, 0.36f, 0.45f);
            if (xray) sc = pin.state == PinState.Set ? Good : pin.state == PinState.FalseSet ? Amber : pin.state == PinState.Overset ? Bad : sc;
            v.stateDot.color = sc;
        }

        // ---------------------------------------------------------------- 输入

        public Vector2 ToU(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(host, e.position, e.pressEventCamera, out local);
            var r = host.rect;
            if (r.width <= 0f || r.height <= 0f) return Vector2.zero;
            return new Vector2((local.x / r.width + host.pivot.x) * W, (local.y / r.height + host.pivot.y) * H);
        }

        int NearestLane(float x)
        {
            return Mathf.Clamp(Mathf.RoundToInt((x - PinX(0, n)) / Pitch), 0, n - 1);
        }

        public void PointerDown(int id, Vector2 u)
        {
            if (u.y < TensionTop)
            {
                if (tensionPointer != NoPointer) return;
                tensionPointer = id;
                SendTension(u);
            }
            else if (u.y < PadTop)
            {
                if (pickPointer != NoPointer) return;
                int l = NearestLane(u.x);
                if (Mathf.Abs(u.x - PinX(l, n)) > Pitch * 0.75f) return;
                pickPointer = id;
                lane = l;
                SendPick(u);
            }
        }

        public void PointerDrag(int id, Vector2 u)
        {
            if (id == tensionPointer) SendTension(u);
            else if (id == pickPointer)
            {
                int nearest = NearestLane(u.x);
                if (nearest != lane && Mathf.Abs(u.x - PinX(lane, n)) > Pitch * 0.65f) lane = nearest;
                SendPick(u);
            }
        }

        public void PointerUp(int id)
        {
            if (id == tensionPointer) tensionPointer = NoPointer;
            else if (id == pickPointer)
            {
                pickPointer = NoPointer;
                lane = -1;
                if (PickEnded != null) PickEnded();
            }
        }

        void SendTension(Vector2 u)
        {
            if (TensionChanged != null) TensionChanged(Mathf.Clamp01((u.x - TensionX0) / (TensionX1 - TensionX0)));
        }

        void SendPick(Vector2 u)
        {
            if (PickChanged != null) PickChanged(lane, Mathf.Clamp01((u.y - LaneBottom) / (LaneTop - LaneBottom)));
        }
    }

    /// <summary>把 EventSystem 的指针事件转给舞台；每个手指按 pointerId 独立处理，所以可以一手张力一手拨针。</summary>
    public class StageInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public LockStage stage;
        public void OnPointerDown(PointerEventData e) { stage.PointerDown(e.pointerId, stage.ToU(e)); }
        public void OnDrag(PointerEventData e) { stage.PointerDrag(e.pointerId, stage.ToU(e)); }
        public void OnPointerUp(PointerEventData e) { stage.PointerUp(e.pointerId); }
    }
}
