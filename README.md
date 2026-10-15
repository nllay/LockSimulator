# 开锁模拟器（Lock Simulator）

Unity 6000.3.20f1，竖屏单机。当前版本是**弹子锁的垂直切片**：先把一种机构的物理仿真、剖面美术和手感做到位，其余机构（叶片、杠杆、盘片、转盘、电子锁……）后续沿用同一套框架逐个加入。

## 怎么玩

和真实拨锁一样，需要两只手配合：

- **张力条（底部）**：模拟张力扳手。拖到哪里就保持在哪里。桌面端可以用滚轮或 ↑↓ 微调。
- **拨针区（中部）**：按住某一列向上拖动，用拨针抬起对应的弹子。

规则与真实弹子锁一致：

1. 只有“卡位”最紧的那一根弹子会被卡住，其余弹子是软的，拨它们没有意义。卡位顺序每把锁都不同，要靠阻力和反馈找出来。
2. 把卡住的弹子抬到剪切线并停一下，锁芯让出一点，弹子被设定（咔哒声）。抬过头会成为**过推**，弹子卡死。
3. 张力太小弹子不会卡住，太大则抬不动；张力松到接近零，已设定的弹子全部弹回。
4. **蘑菇形安全弹子**会先给出**假设定**（锁芯多转一截），此时继续抬会被锁芯回弹顶住，必须先放松一点张力。
5. 全部设定后锁芯转到底，锁打开。

两种模式：**透视**（看得到内部结构）和**盲拨**（内部被遮住，只靠拨针阻力、声音和锁芯转角判断，完成有额外加分）。

共 5 把锁，从 3 针挂锁到带 3 个安全弹子的高安全锁，设定窗口逐渐变窄。每次开始都会生成新的弹子高度和卡位顺序。

## 代码结构

- `Assets/Scripts/PinTumblerSim.cs`：弹子锁物理规则，不依赖 UI，可直接批处理测试；`SimBot` 是按规则求解的机器人。
- `Assets/Scripts/LockStage.cs`：剖面视图、拨针区、张力条的绘制与多点触控输入（每根手指按 pointerId 独立处理）。
- `Assets/Scripts/Art.cs`：运行时生成的金属/拉丝/圆柱贴图，不依赖外部素材和自定义着色器。
- `Assets/Scripts/Sfx.cs`：程序化咔哒声（噪声脉冲 + 金属共振）。
- `Assets/Scripts/LockCatalog.cs`：5 把锁的参数与本地进度。
- `Assets/Scripts/LockSimulatorApp.cs`：界面流程（首页、拨锁、结算）。
- `Assets/Editor/LockSimulatorProjectSetup.cs`：项目配置、构建入口、批处理测试。

## 验证

```bash
/Applications/Unity/Hub/Editor/6000.3.20f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath /absolute/path/to/LockSimulator \
  -executeMethod LockSimulator.LockSimulatorProjectSetup.RuntimeSmokeTest
```

测试内容：每把锁 × 30 个随机种子都能按规则打开；过推可恢复；张力过大抬不动；松开张力全部弹回；非卡位弹子不会被设定；蘑菇弹子的假设定；剖面视图和首页能正常构建。

截图自检：构建后用 `-shot <目录>` 启动，会依次截取首页、拨锁、卡位提示、盲拨、结算画面并退出（该模式不写入存档）。

## 构建

菜单 **Lock Simulator → Build Mac / Build Android**，或命令行调用 `LockSimulator.LockSimulatorProjectSetup.BuildMac` / `BuildAndroid`。桌面端窗口默认 540×960，可任意缩放，内容保持 9:16。

`Builds/Android/LockSimulator.apk` 和上级目录的 `LockSimulator_Android.zip` 是重构前的旧版本，需要重新构建。

## 已知限制

- 手机上的触觉反馈目前只有“打开”时的系统震动；拨针的细腻阻力靠视觉和声音表达，后续可接入 Android 原生震动波形。
- 只有弹子锁一种机构，`LOCK_SIMULATOR_PLAN.md` 中的其余 95 把锁尚未实现。
