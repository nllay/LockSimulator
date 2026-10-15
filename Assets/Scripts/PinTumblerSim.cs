using System;
using UnityEngine;

namespace LockSimulator
{
    public enum PinState { Rest, FalseSet, Set, Overset }
    public enum SimEvent { Set, FalseSet, Overset, Free, Reset, CounterRotate, Scrape, Open }

    /// <summary>
    /// 弹子锁的物理规则，不依赖任何 UI。
    /// 长度单位是归一化的弹子行程：lift 0 表示静止，lift == setH 表示上下弹子的分界线正好在剪切线上。
    /// 规则（与真实拨锁一致）：
    ///  1. 张力使锁芯转动，只有“卡位”最紧的那一根弹子（rank 最小且未设定）被卡住，其余弹子是软的；
    ///  2. 卡住的弹子抬到剪切线并停留片刻，锁芯才会让出一点，弹子被设定；抬过头就成为过推并卡死；
    ///  3. 张力太小弹子不会卡住，太大则抬不动；张力松到接近零，已设定的弹子全部弹回；
    ///  4. 蘑菇形安全弹子先出现假设定（锁芯多转一截），张力大时继续抬会被锁芯回弹顶住，必须放松张力。
    /// </summary>
    public class PinTumblerSim
    {
        public const float BindMin = 0.14f;     // 低于此张力没有任何弹子被卡住
        public const float HoldMin = 0.08f;     // 低于此张力，已设定的弹子弹回
        public const float EaseMax = 0.32f;     // 蘑菇弹子：张力不高于此值才能越过假设定
        public const float Dwell = 0.18f;       // 在设定窗口内需要停留的时间
        public const float SpoolOffset = 0.12f; // 假设定位置比真设定低多少
        const float LiftSpeed = 0.55f;
        const float FallSpeed = 2.5f;
        const float FreeSpeed = 3.0f;
        const float ScrapeStep = 0.04f;

        public class Pin
        {
            public float setH;
            public int rank;        // 卡位顺序，0 最先
            public bool spool;
            public float lift;
            public PinState state;
            public float dwell;
        }

        public readonly Pin[] pins;
        public readonly float tolerance;
        public readonly float maxTension;
        public float tension;
        public float angle;         // 锁芯转角 0-1，1 = 打开
        public float resist;        // 拨针手感阻力 0-1
        public float kick;          // 锁芯回弹的瞬时抖动
        public bool opened;
        public int resets, oversets;
        public int selected = -1;
        public float pickTarget;
        public event Action<SimEvent, int> Fired;

        float scrapeAcc, counterTimer;

        public PinTumblerSim(LockSpec spec, int seed)
        {
            var rng = new System.Random(seed);
            int n = spec.pins;
            tolerance = spec.tolerance;
            maxTension = spec.maxTension;
            pins = new Pin[n];
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
            for (int i = 0; i < n; i++)
                pins[i] = new Pin { setH = 0.28f + (float)rng.NextDouble() * 0.42f, rank = order[i] };

            int placed = 0;
            while (placed < spec.spools && placed < n)
            {
                var p = pins[rng.Next(n)];
                if (p.spool) continue;
                p.spool = true;
                p.setH = Mathf.Max(p.setH, 0.34f);
                placed++;
            }
        }

        public int SetCount
        {
            get { int c = 0; foreach (var p in pins) if (p.state == PinState.Set) c++; return c; }
        }

        public int BindingIndex()
        {
            int best = -1;
            for (int i = 0; i < pins.Length; i++)
            {
                if (pins[i].state == PinState.Set) continue;
                if (best < 0 || pins[i].rank < pins[best].rank) best = i;
            }
            return best;
        }

        public void SetTension(float t) { tension = Mathf.Clamp01(t); }
        public void Pick(int pin, float target) { selected = pin; pickTarget = Mathf.Clamp01(target); }
        public void Release() { selected = -1; }

        void Fire(SimEvent ev, int i) { if (Fired != null) Fired(ev, i); }

        public void Tick(float dt)
        {
            resist = 0f;
            kick = Mathf.MoveTowards(kick, 0f, dt * 6f);
            if (opened) return;

            if (tension < HoldMin) ResetAll();
            bool loaded = tension >= BindMin;
            if (!loaded) EaseOff();
            int bind = BindingIndex();

            for (int i = 0; i < pins.Length; i++)
            {
                var p = pins[i];
                bool picked = i == selected;
                if (p.state == PinState.Set) { if (picked) resist = Mathf.Max(resist, 0.2f); continue; }
                if (p.state == PinState.Overset) { if (picked) resist = 1f; continue; }
                if (p.state == PinState.Rest && !picked)
                {
                    p.lift = Mathf.MoveTowards(p.lift, 0f, dt * FallSpeed);
                    p.dwell = 0f;
                    continue;
                }
                if (!picked) continue; // 假设定的弹子保持在原位

                if (loaded && i == bind) MoveBound(p, dt);
                else
                {
                    p.lift = Mathf.MoveTowards(p.lift, pickTarget, dt * FreeSpeed);
                    p.dwell = 0f;
                    resist = Mathf.Max(resist, 0.1f + 0.1f * p.lift);
                }
            }
            UpdateAngle(dt, bind, loaded);
        }

        void EaseOff()
        {
            for (int i = 0; i < pins.Length; i++)
            {
                var p = pins[i];
                if (p.state == PinState.Overset) { p.state = PinState.Rest; p.dwell = 0f; Fire(SimEvent.Free, i); }
                else if (p.state == PinState.FalseSet) { p.state = PinState.Rest; p.dwell = 0f; }
            }
        }

        void ResetAll()
        {
            bool any = false;
            foreach (var p in pins)
            {
                if (p.state != PinState.Rest) any = true;
                p.state = PinState.Rest;
                p.dwell = 0f;
            }
            if (any) { resets++; Fire(SimEvent.Reset, -1); }
        }

        void MoveBound(Pin p, float dt)
        {
            if (tension > maxTension)
            {
                resist = 1f;
                p.dwell = 0f;
                if (pickTarget < p.lift) p.lift = Mathf.MoveTowards(p.lift, pickTarget, dt * LiftSpeed);
                return;
            }

            float falseCenter = p.setH - SpoolOffset;
            float cap = 1f;
            if (p.spool && tension > EaseMax) cap = falseCenter + tolerance * 0.5f;

            float speed = LiftSpeed * (1f - 0.5f * tension);
            float before = p.lift;
            p.lift = Mathf.MoveTowards(p.lift, Mathf.Min(pickTarget, cap), dt * speed);
            resist = Mathf.Lerp(0.35f, 0.9f, tension);

            scrapeAcc += Mathf.Abs(p.lift - before);
            if (scrapeAcc >= ScrapeStep) { scrapeAcc = 0f; Fire(SimEvent.Scrape, SelectedIndex(p)); }

            bool pushingAgainstCap = pickTarget > cap + 0.01f && p.lift >= cap - 0.005f;
            if (pushingAgainstCap)
            {
                resist = 1f;
                counterTimer += dt;
                if (counterTimer > 0.2f) { counterTimer = 0f; kick = 1f; Fire(SimEvent.CounterRotate, SelectedIndex(p)); }
            }
            else counterTimer = 0f;

            bool inTrue = Mathf.Abs(p.lift - p.setH) <= tolerance;
            bool inFalse = p.spool && p.state == PinState.Rest && Mathf.Abs(p.lift - falseCenter) <= tolerance;
            if (inTrue || inFalse)
            {
                p.dwell += dt;
                if (p.dwell >= Dwell)
                {
                    p.dwell = 0f;
                    if (inTrue) { p.state = PinState.Set; p.lift = p.setH; Fire(SimEvent.Set, SelectedIndex(p)); }
                    else { p.state = PinState.FalseSet; Fire(SimEvent.FalseSet, SelectedIndex(p)); }
                }
            }
            else
            {
                p.dwell = Mathf.Max(0f, p.dwell - dt * 2f);
                if (p.lift > p.setH + tolerance)
                {
                    p.state = PinState.Overset;
                    p.lift = p.setH + tolerance + 0.02f;
                    p.dwell = 0f;
                    oversets++;
                    Fire(SimEvent.Overset, SelectedIndex(p));
                }
            }
        }

        int SelectedIndex(Pin p) { return Array.IndexOf(pins, p); }

        void UpdateAngle(float dt, int bind, bool loaded)
        {
            int n = pins.Length;
            float a = SetCount / (float)n * 0.88f;
            if (bind >= 0 && loaded)
            {
                var b = pins[bind];
                if (b.state == PinState.FalseSet) a += 0.6f / n;
                else if (b.dwell > 0f) a += 0.4f / n * (b.dwell / Dwell);
            }
            float target = a * Mathf.Clamp01(tension / BindMin);
            if (bind < 0 && loaded) target = 1f;
            angle = Mathf.MoveTowards(angle, target, dt * 1.5f);
            if (bind < 0 && loaded && angle >= 0.995f)
            {
                angle = 1f;
                opened = true;
                Fire(SimEvent.Open, -1);
            }
        }
    }

    /// <summary>按物理规则逐根拨开的求解器，用于自动测试和截图。</summary>
    public static class SimBot
    {
        public static bool Solve(PinTumblerSim sim, int stopAfterSets = -1, float maxSeconds = 120f)
        {
            const float dt = 1f / 60f;
            sim.SetTension(0.25f);
            for (float t = 0f; t < maxSeconds && !sim.opened; t += dt)
            {
                if (stopAfterSets >= 0 && sim.SetCount >= stopAfterSets) break;
                int bind = sim.BindingIndex();
                if (bind >= 0) sim.Pick(bind, sim.pins[bind].setH); else sim.Release();
                sim.Tick(dt);
            }
            sim.Release();
            return sim.opened;
        }
    }
}
