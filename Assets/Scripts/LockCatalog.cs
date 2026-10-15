using System.Collections.Generic;
using UnityEngine;

namespace LockSimulator
{
    public class LockSpec
    {
        public int id;
        public string name;
        public string note;
        public int pins;
        public float tolerance;   // 设定窗口半宽（归一化行程）
        public int spools;        // 蘑菇形安全弹子数量
        public float maxTension;  // 超过此张力，卡住的弹子抬不动
        public float par;         // 参考用时（秒）
        public Color accent;
    }

    /// <summary>
    /// 当前只有弹子锁一种机构。其他机构（叶片、杠杆、转盘……）后续各自实现独立的仿真类，
    /// 再把 LockSpec 扩展出机构类型即可，不需要改动这里的进度逻辑。
    /// </summary>
    public static class LockCatalog
    {
        public static readonly List<LockSpec> All = new List<LockSpec>
        {
            new LockSpec { id = 1, name = "三针挂锁", note = "入门：3 个弹子，无安全弹子", pins = 3, tolerance = 0.07f, spools = 0, maxTension = 0.85f, par = 40f, accent = new Color(0.22f, 0.83f, 0.96f) },
            new LockSpec { id = 2, name = "五针挂锁", note = "5 个弹子，卡位顺序是随机的", pins = 5, tolerance = 0.055f, spools = 0, maxTension = 0.80f, par = 70f, accent = new Color(0.36f, 0.86f, 0.62f) },
            new LockSpec { id = 3, name = "圆柱门锁", note = "5 个弹子，含 1 个蘑菇形安全弹子", pins = 5, tolerance = 0.045f, spools = 1, maxTension = 0.75f, par = 110f, accent = new Color(0.95f, 0.72f, 0.30f) },
            new LockSpec { id = 4, name = "防盗门锁", note = "6 个弹子，含 2 个安全弹子", pins = 6, tolerance = 0.04f, spools = 2, maxTension = 0.70f, par = 170f, accent = new Color(0.98f, 0.52f, 0.36f) },
            new LockSpec { id = 5, name = "高安全锁", note = "6 个弹子，含 3 个安全弹子，窗口极窄", pins = 6, tolerance = 0.032f, spools = 3, maxTension = 0.62f, par = 260f, accent = new Color(0.72f, 0.5f, 0.98f) },
        };
    }

    /// <summary>本地进度。</summary>
    public static class ProgressStore
    {
        /// <summary>自动截图/测试时置为 true，不写入玩家的真实存档。</summary>
        public static bool ReadOnly;

        public static bool IsDone(int id) { return PlayerPrefs.GetInt("pin_done_" + id, 0) == 1; }
        public static int Stars(int id) { return PlayerPrefs.GetInt("pin_stars_" + id, 0); }
        public static int Best(int id) { return PlayerPrefs.GetInt("pin_best_" + id, 0); }
        public static bool IsUnlocked(int id) { return id <= 1 || IsDone(id - 1); }

        /// <summary>每次开始递增，保证“再来一把”换一把新锁（弹子高度和卡位顺序都会变）。</summary>
        public static int NextRun(int id)
        {
            int run = PlayerPrefs.GetInt("pin_runs_" + id, 0);
            if (ReadOnly) return run;
            PlayerPrefs.SetInt("pin_runs_" + id, run + 1);
            return run;
        }

        public static bool Record(int id, int score, int stars)
        {
            bool record = score > Best(id);
            if (ReadOnly) return record;
            PlayerPrefs.SetInt("pin_done_" + id, 1);
            if (record) PlayerPrefs.SetInt("pin_best_" + id, score);
            if (stars > Stars(id)) PlayerPrefs.SetInt("pin_stars_" + id, stars);
            PlayerPrefs.Save();
            return record;
        }
    }
}
