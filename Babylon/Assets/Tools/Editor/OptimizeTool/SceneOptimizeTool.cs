using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 小n的 场景优化工具
/// 菜单路径：nTools/性能优化/场景优化
/// 提供特效、材质、模型面数三个维度的场景检查功能
/// </summary>
public class SceneOptimizeTool : EditorWindow
{
    // ==================== Tab 定义 ====================
    private enum TabType
    {
        VFX,        // 特效
        Material,   // 材质
        Mesh        // 模型面数
    }

    private TabType _currentTab = TabType.VFX;

    // ==================== 滚动位置 ====================
    private Vector2 _scrollPos;

    // ==================== 检查标准设置====================
    // 特效 - 阈值
    private int _maxParticlesThreshold = 30;
    private float _emissionRateThreshold = 12f;
    private int _burstCountThreshold = 18;
    private float _startLifetimeThreshold = 3f;
    private int _maxLightsThreshold = 0;
    private int _lineRendererVertThreshold = 60;

    // 特效 - 检查项开关
    private bool _chkMaxParticles = true;
    private bool _chkEmissionRate = true;
    private bool _chkBurst = true;
    private bool _chkStartLifetime = true;
    private bool _chkCollision = true;
    private bool _chkSubEmitters = true;
    private bool _chkLights = true;
    private bool _chkMeshMode = true;
    private bool _chkPrewarm = true;
    private bool _chkTrails = true;
    private bool _chkNoise = true;
    private bool _chkTrigger = true;
    private bool _chkShadowCasting = true;
    private bool _chkTrailLine = true;
    private bool _chkCullOff = true;

    // 材质
    private int _textureHighThreshold = 2048;

    // 模型
    private int _highPolyThreshold = 10000;
    private int _meshColliderThreshold = 5000;
    private int _duplicateMeshMinTri = 1000;        // 重复 Mesh 最低关注面数

    // ==================== UI 状态====================
    private bool _showSettings = true;        // 标准设置折叠（默认展开）
    private bool _onlyShowWarnings = false;   // 仅显示警告
    private string _searchFilter = "";        // 搜索过滤
    private bool _hasCheckedVFX = false;
    private bool _hasCheckedMat = false;
    private bool _hasCheckedMesh = false;

    // ==================== 检查结果====================
    private class CheckResult
    {
        public string category;
        public string description;
        public string tooltip; // 鼠标悬停提示文字
        public GameObject targetObj;
        public MessageType msgType;
    }

    // ==================== 分类悬停提示 ====================
    private static readonly Dictionary<string, string> _categoryTooltips = new Dictionary<string, string>
    {
        // ----- 粒子特效 -----
        { "粒子系统总览", "统计场景中所有粒子系统的数量及基本信息。\n粒子系统过多会增加 CPU/GPU 开销。" },
        { "MaxParticles 超标", "【影响: CPU + GPU】内存分配、顶点数。\n过高的 MaxParticles 会导致大量粒子同时存在，增加 GPU 渲染和内存压力。\n移动端建议 ≤50，PC 建议 ≤500。" },
        { "EmissionRate 超标", "【影响: CPU + GPU】实际同屏粒子数。\n过高的发射速率会导致每帧产生大量新粒子，加重 CPU 模拟和 GPU 渲染负担。\n移动端建议 ≤20/s，PC 建议 ≤100/s。" },
        { "Burst 超标", "【影响: CPU + GPU】瞬间高峰粒子数。\n单次 Burst 产生大量粒子会导致帧率瞬间下降。\n移动端单次 Burst 建议 ≤30。" },
        { "Start Lifetime 超标", "【影响: CPU（高）】同屏存活粒子数。\n生命周期越长同屏活跃粒子越多，缩短 Lifetime 可有效降低同屏粒子数。\n移动端建议 ≤3s。" },
        { "粒子 Collision 模块", "【影响: CPU（极高）】物理查询 Raycast/Spherecast。\n每粒子每帧做射线检测，代价极高。\n确认必要后用 World 碰撞 + 低精度，否则关闭。" },
        { "粒子 Sub Emitters", "【影响: CPU + GPU（极高）】指数级粒子生成。\n子发射器会连锁产生大量粒子，严格限制层级和子粒子数。\n每级子发射都会使粒子总量指数增长。" },
        { "粒子 Lights 模块", "【影响: GPU（极高）】每粒子附加一个实时点光源。\n极度昂贵！每个粒子光源都需要额外的光照计算 Pass。\n移动端建议 MaxLights ≤2 或完全禁用，改用自发光 Shader。" },
        { "粒子 Mesh 渲染模式", "【影响: GPU（极高）】顶点数成倍增长。\nMesh 模式下顶点数 = 粒子数 × 网格顶点数。\nBillboard 模式开销最低（每粒子仅 4 个顶点），优先使用。" },
        { "粒子 Prewarm", "【影响: CPU（高）】首帧模拟尖峰。\n启用后首帧会模拟完整生命周期的粒子，可能导致明显卡顿。\n除非确实需要一开始就有满屏粒子，否则关闭。" },
        { "粒子 Trails 拖尾", "【影响: GPU + CPU（高）】额外顶点生成和计算。\n每粒子额外生成顶点段，同时增加 DrawCall 和顶点数。\n移动端慎用，或减少 Trail 段数。" },
        { "粒子 Noise 模块", "【影响: CPU（高）】每帧 Perlin Noise 采样。\nQuality 越高 octave 越多，CPU 开销越大。\n建议 Quality 设为 Low（1 octave）。" },
        { "粒子 Trigger 模块", "【影响: CPU（高）】每帧检测粒子是否在 Collider 内。\n粒子数多时代价高，按需启用。" },
        { "粒子投射阴影", "【影响: GPU（高）】额外 Shadow Pass。\n粒子通常不需要投射阴影，开启会显著增加 Draw Call 和 GPU 开销。\n建议全部关闭。" },
        { "Trail/Line 渲染器", "【影响: CPU + GPU】动态 Mesh 生成。\n这类渲染器会动态生成 Mesh，过多会增加 CPU 和内存开销。" },
        { "粒子材质 Cull Off", "【影响: GPU】双面渲染使绘制量翻倍。\n双面渲染会使 GPU 绘制量翻倍，应仅在确实需要时使用。" },

        // ----- 材质贴图 -----
        { "空材质引用", "检查渲染器上是否存在空（Missing）材质引用。\n空材质会导致粉色显示错误并产生不必要的 Draw Call。" },
        { "Shader 分类统计", "统计场景中使用的 Shader 种类和数量。\nShader 种类过多会增加 Shader 编译时间和变体数量。" },
        { "冗余材质", "检查是否有多个材质使用相同的 Shader 和贴图。\n合并冗余材质可以减少 Draw Call 和内存占用。" },
        { "贴图尺寸统计", "统计场景中所有贴图的尺寸分布。\n过大的贴图会占用大量显存和内存，应根据实际需要选择合适尺寸。" },
        { "GPU Instancing", "检查可以开启 GPU Instancing 但未开启的材质。\n开启 GPU Instancing 可以合批绘制，显著减少 Draw Call。" },

        // ----- 模型面数 -----
        { "Mesh 统计总览", "统计场景中所有 Mesh 的数量、三角面数和顶点数。\n用于整体评估场景的模型复杂度。" },
        { "高面数模型", "检查三角面数超过标准的 Mesh。\n高面数模型会增加 GPU 顶点处理和光栅化负担，应考虑减面或 LOD。" },
        { "Read/Write Enabled", "检查 Mesh 是否开启了 Read/Write Enabled。\n开启后 Mesh 数据会在 CPU 内存中保留一份副本，导致双倍内存占用。\n除非需要运行时读写 Mesh 数据，否则应关闭。" },
        { "重复 Mesh", "检查不同名称的 Mesh Asset 是否具有相同的顶点和三角面数。\n可能是重复导入的相同模型，合并后可减少内存占用。" },
        { "MeshCollider 面数", "检查 MeshCollider 使用的 Mesh 面数是否超过标准。\n高面数的碰撞网格会严重影响物理计算性能，建议使用简化的碰撞体。" },
        { "检查范围", "当前检查的对象范围。\n未指定时检查全场景，指定后仅检查选定对象及其所有子节点。" },
    };

    private List<CheckResult> _vfxResults = new List<CheckResult>();
    private List<CheckResult> _materialResults = new List<CheckResult>();
    private List<CheckResult> _meshResults = new List<CheckResult>();

    // 每个分类的警告数量缓存
    private Dictionary<string, int> _categoryWarningCount = new Dictionary<string, int>();
    private Dictionary<string, int> _categoryTotalCount = new Dictionary<string, int>();

    // 折叠状态
    private Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

    // ==================== VFX 检查范围（拖拽式） ====================
    private List<GameObject> _vfxScopeTargets = new List<GameObject>();
    private Vector2 _vfxScopeScrollPos;

    // ==================== 模型检查范围（拖拽式） ====================
    // 用户手动拖入的检查对象列表，为空时检查全场景
    private List<GameObject> _meshScopeTargets = new List<GameObject>();
    private Vector2 _scopeScrollPos;
    private int _meshScopeWarnThreshold = 2000; // 检查范围列表中单个 obj 面数警告标准

    // ==================== 样式缓存 ====================
    private GUIStyle _warningRowStyle;
    private GUIStyle _normalRowStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _summaryStyle;
    private GUIStyle _tabButtonStyle;
    private GUIStyle _tabButtonActiveStyle;
    private bool _stylesInitialized = false;

    private void InitStyles()
    {
        if (_stylesInitialized) return;

        _warningRowStyle = new GUIStyle(EditorStyles.helpBox);
        _warningRowStyle.margin = new RectOffset(4, 4, 1, 1);
        _warningRowStyle.padding = new RectOffset(6, 6, 4, 4);

        _normalRowStyle = new GUIStyle(EditorStyles.label);
        _normalRowStyle.margin = new RectOffset(4, 4, 1, 1);
        _normalRowStyle.padding = new RectOffset(6, 6, 4, 4);

        _headerStyle = new GUIStyle(EditorStyles.boldLabel);
        _headerStyle.fontSize = 13;

        _summaryStyle = new GUIStyle(EditorStyles.helpBox);
        _summaryStyle.fontSize = 12;
        _summaryStyle.richText = true;
        _summaryStyle.padding = new RectOffset(10, 10, 8, 8);

        _stylesInitialized = true;
    }

    [MenuItem("nTools/性能优化/场景优化", false, 200)]
    public static void ShowWindow()
    {
        var window = GetWindow<SceneOptimizeTool>("场景优化工具");
        window.minSize = new Vector2(550, 450);
        window.Show();
    }

    private void OnGUI()
    {
        InitStyles();

        // ===== 顶部工具栏=====
        DrawTopToolbar();

        // ===== 汇总摘要条（置顶） =====
        DrawSummaryBar();

        EditorGUILayout.Space(3);

        // ===== Tab 页签（自定义样式）=====
        DrawTabBar();

        EditorGUILayout.Space(3);

        // ===== 过滤栏 =====
        DrawFilterBar();

        // ===== 检查标准设置（按当前 Tab 显示）=====
        DrawStandardsPanel();

        // ===== 结果内容 =====
        switch (_currentTab)
        {
            case TabType.VFX:
                DrawVFXScopePanel();
                DrawResults(_vfxResults);
                break;
            case TabType.Material:
                DrawResults(_materialResults);
                break;
            case TabType.Mesh:
                DrawMeshScopePanel();
                DrawResults(_meshResults);
                break;
        }
    }

    // ==================================================================================
    //  顶部工具栏
    // ==================================================================================
    private void DrawTopToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            // 全部检查按钮
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("▶ 全部检查", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                CheckVFX();
                CheckMaterials();
                CheckMesh();
            }
            GUI.backgroundColor = prevColor;

            // 当前 Tab 检查按钮
            string tabName = _currentTab == TabType.VFX ? "特效" : _currentTab == TabType.Material ? "材质" : "模型";
            if (GUILayout.Button($"▶ 检查{tabName}", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                switch (_currentTab)
                {
                    case TabType.VFX: CheckVFX(); break;
                    case TabType.Material: CheckMaterials(); break;
                    case TabType.Mesh: CheckMesh(); break;
                }
            }

            GUILayout.FlexibleSpace();

            // 设置齿轮按钮
            if (GUILayout.Button(_showSettings ? "⚙ 收起标准" : "⚙ 检查标准", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                _showSettings = !_showSettings;
            }
        }
    }

    // ==================================================================================
    //  Tab 页签（带警告计数小气泡）
    // ==================================================================================
    private void DrawTabBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(4);
            DrawTabButton("特效", TabType.VFX, _vfxResults, _hasCheckedVFX);
            DrawTabButton("材质", TabType.Material, _materialResults, _hasCheckedMat);
            DrawTabButton("模型面数", TabType.Mesh, _meshResults, _hasCheckedMesh);
            GUILayout.Space(4);
        }
    }

    private void DrawTabButton(string label, TabType tab, List<CheckResult> results, bool hasChecked)
    {
        int warningCount = results.Count(r => r.msgType == MessageType.Warning || r.msgType == MessageType.Error);
        string displayLabel = label;
        if (hasChecked && warningCount > 0)
            displayLabel = $"{label}  ⚠{warningCount}";
        else if (hasChecked && warningCount == 0 && results.Count > 0)
            displayLabel = $"{label}  ✓";

        bool isActive = _currentTab == tab;
        var style = isActive ? new GUIStyle("LargeButtonMid") : new GUIStyle("LargeButtonMid");
        style.fixedHeight = 28;
        style.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
        style.fontSize = 12;

        var prevColor = GUI.backgroundColor;
        if (isActive)
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        else if (hasChecked && warningCount > 0)
            GUI.backgroundColor = new Color(1f, 0.9f, 0.7f);

        if (GUILayout.Button(displayLabel, style, GUILayout.MinWidth(80)))
        {
            _currentTab = tab;
            _scrollPos = Vector2.zero;
        }
        GUI.backgroundColor = prevColor;
    }

    // ==================================================================================
    //  检查标准设置面板（根据当前 Tab 显示对应标准）
    // ==================================================================================
    private void DrawStandardsPanel()
    {
        if (!_showSettings) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            switch (_currentTab)
            {
                case TabType.VFX:
                    DrawVFXCheckRow(ref _chkMaxParticles, "极高", "Max Particles",
                        "【CPU + GPU】内存分配、顶点数。\n过高会导致大量粒子同时存在，增加渲染和内存压力。\n移动端建议 ≤30，PC 建议 ≤500。",
                        ref _maxParticlesThreshold);
                    DrawVFXCheckRow(ref _chkEmissionRate, "极高", "Emission Rate",
                        "【CPU + GPU】实际同屏粒子数。\n过高会导致每帧产生大量新粒子，加重 CPU 模拟和 GPU 渲染。\n移动端建议 ≤12/s，PC 建议 ≤100/s。",
                        ref _emissionRateThreshold);
                    DrawVFXCheckRow(ref _chkBurst, "高", "Burst Count",
                        "【CPU + GPU】瞬间高峰粒子数。\n单次 Burst 大量粒子会导致帧率瞬间下降。\n移动端建议 ≤18。",
                        ref _burstCountThreshold);
                    DrawVFXCheckRow(ref _chkStartLifetime, "高", "Start Lifetime (s)",
                        "【CPU】同屏存活粒子数。\n生命周期越长同屏活跃粒子越多。\n缩短 Lifetime 可有效降低同屏粒子数。\n移动端建议 ≤3s。",
                        ref _startLifetimeThreshold);
                    DrawVFXCheckRow(ref _chkCollision, "极高", "Collision Module",
                        "【CPU（极高）】物理查询 Raycast/Spherecast。\n每粒子每帧做射线检测，代价极高。\n确认必要后用 World 碰撞 + Low Quality，否则关闭。");
                    DrawVFXCheckRow(ref _chkSubEmitters, "极高", "Sub Emitters",
                        "【CPU + GPU（极高）】指数级粒子生成。\n子发射器会连锁产生大量粒子。\n每级子发射使粒子总量指数增长，严格限制层级。");
                    DrawVFXCheckRow(ref _chkLights, "极高", "Lights Module",
                        "【GPU（极高）】每粒子附加一个实时点光源。\n极度昂贵！每个粒子光源都需要额外光照 Pass。\n移动端建议 MaxLights = 0 或禁用，改用自发光 Shader。",
                        ref _maxLightsThreshold);
                    DrawVFXCheckRow(ref _chkMeshMode, "极高", "Mesh Render Mode",
                        "【GPU（极高）】顶点数成倍增长。\nMesh 模式下顶点数 = 粒子数 × 网格顶点。\nBillboard 模式每粒子仅 4 顶点，优先使用。");
                    DrawVFXCheckRow(ref _chkPrewarm, "高", "Prewarm",
                        "【CPU（高）】首帧模拟尖峰。\n启用后首帧会模拟完整生命周期的粒子，可能导致明显卡顿。\n除非需要一开始就有满屏粒子，否则关闭。");
                    DrawVFXCheckRow(ref _chkTrails, "高", "Trails 拖尾",
                        "【GPU + CPU（高）】额外顶点生成和计算。\n每粒子额外生成顶点段，增加 DrawCall 和顶点数。\n移动端慎用，或减少 Trail 段数。");
                    DrawVFXCheckRow(ref _chkNoise, "高", "Noise Module",
                        "【CPU（高）】每帧 Perlin Noise 采样。\nQuality 越高 octave 越多，CPU 开销越大。\n建议 Quality 设为 Low（1 octave）。");
                    DrawVFXCheckRow(ref _chkTrigger, "高", "Trigger Module",
                        "【CPU（高）】每帧检测粒子是否在 Collider 内。\n粒子数多时代价高，按需启用。");
                    DrawVFXCheckRow(ref _chkShadowCasting, "高", "Shadow Casting",
                        "【GPU（高）】额外 Shadow Pass。\n粒子通常不需要投射阴影，开启会显著增加 DrawCall。\n建议全部关闭。");
                    DrawVFXCheckRow(ref _chkTrailLine, "中", "Trail/Line Renderer",
                        "【CPU + GPU】动态 Mesh 生成。\n这类渲染器会动态生成 Mesh，过多增加 CPU 和内存开销。",
                        ref _lineRendererVertThreshold);
                    DrawVFXCheckRow(ref _chkCullOff, "中", "粒子材质 Cull Off",
                        "【GPU】双面渲染使绘制量翻倍。\n双面渲染会使 GPU 绘制量翻倍，仅在确实需要时使用。");

                    if (_maxParticlesThreshold < 1) _maxParticlesThreshold = 1;
                    if (_emissionRateThreshold < 1f) _emissionRateThreshold = 1f;
                    if (_burstCountThreshold < 1) _burstCountThreshold = 1;
                    if (_startLifetimeThreshold < 0.1f) _startLifetimeThreshold = 0.1f;
                    if (_maxLightsThreshold < 0) _maxLightsThreshold = 0;
                    if (_lineRendererVertThreshold < 1) _lineRendererVertThreshold = 1;
                    break;

                case TabType.Material:
                    _textureHighThreshold = EditorGUILayout.IntField(new GUIContent("贴图尺寸标准 (单边)", "贴图单边尺寸超过此值将标记为超大贴图"), _textureHighThreshold);

                    if (_textureHighThreshold < 64) _textureHighThreshold = 64;
                    break;

                case TabType.Mesh:
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _highPolyThreshold = EditorGUILayout.IntField(new GUIContent("高面数标准 (三角面)", "Mesh 三角面数超过此值将标记为高面数模型"), _highPolyThreshold);
                        _meshColliderThreshold = EditorGUILayout.IntField(new GUIContent("MeshCollider 面数标准", "MeshCollider 使用的 Mesh 面数超过此值将发出警告"), _meshColliderThreshold);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _duplicateMeshMinTri = EditorGUILayout.IntField(new GUIContent("重复Mesh最低关注面数", "只关注三角面数大于此值的重复 Mesh"), _duplicateMeshMinTri);
                        _meshScopeWarnThreshold = EditorGUILayout.IntField(new GUIContent("检查范围面数标准", "检查范围中单个对象面数超过此值将标记警告"), _meshScopeWarnThreshold);
                    }

                    if (_highPolyThreshold < 100) _highPolyThreshold = 100;
                    if (_meshColliderThreshold < 100) _meshColliderThreshold = 100;
                    if (_duplicateMeshMinTri < 0) _duplicateMeshMinTri = 0;
                    if (_meshScopeWarnThreshold < 0) _meshScopeWarnThreshold = 0;
                    break;
            }
        }
    }

    // ==================================================================================
    //  汇总摘要条
    // ==================================================================================
    private void DrawSummaryBar()
    {
        // 汇总所有 Tab 的检查结果
        int totalVFX = _vfxResults.Count;
        int totalMat = _materialResults.Count;
        int totalMesh = _meshResults.Count;
        int totalAll = totalVFX + totalMat + totalMesh;
        if (totalAll == 0) return;

        int warnVFX = _vfxResults.Count(r => r.msgType == MessageType.Warning || r.msgType == MessageType.Error);
        int warnMat = _materialResults.Count(r => r.msgType == MessageType.Warning || r.msgType == MessageType.Error);
        int warnMesh = _meshResults.Count(r => r.msgType == MessageType.Warning || r.msgType == MessageType.Error);
        int totalWarnings = warnVFX + warnMat + warnMesh;

        // 根据有无警告选择不同的背景色和图标
        bool hasWarning = totalWarnings > 0;
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = hasWarning ? new Color(1f, 0.82f, 0.4f, 0.6f) : new Color(0.45f, 0.85f, 0.55f, 0.6f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // 大图标
                string icon = hasWarning ? "⚠" : "✓";
                var iconStyle = new GUIStyle(EditorStyles.boldLabel);
                iconStyle.fontSize = 18;
                iconStyle.alignment = TextAnchor.MiddleCenter;
                iconStyle.normal.textColor = hasWarning ? new Color(0.9f, 0.6f, 0.1f) : new Color(0.2f, 0.7f, 0.3f);
                EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(28), GUILayout.Height(24));

                // 汇总文字
                var textStyle = new GUIStyle(EditorStyles.label);
                textStyle.richText = true;
                textStyle.fontSize = 12;
                textStyle.alignment = TextAnchor.MiddleLeft;

                string warnColor = hasWarning ? "#DD6600" : "#338833";
                string summary;
                if (hasWarning)
                {
summary = $"<color={warnColor}><b>{totalWarnings}</b> 个警告</color>";
                    // 分维度明细
                    var details = new List<string>();
                    if (warnVFX > 0) details.Add($"特效 {warnVFX}");
                    if (warnMat > 0) details.Add($"材质 {warnMat}");
                    if (warnMesh > 0) details.Add($"模型 {warnMesh}");
                    summary += $"  <color=#666666>({string.Join(" / ", details)})</color>";
                }
                else
                {
                    summary = $"<color={warnColor}><b>全部通过</b></color>";
                }
summary += $"    <color=#888888>{totalAll} 条结果</color>";

                EditorGUILayout.LabelField(summary, textStyle, GUILayout.Height(24));
            }
        }
        GUI.backgroundColor = prevBg;
    }

    // ==================================================================================
    //  过滤栏
    // ==================================================================================
    private void DrawFilterBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            // 搜索框
            EditorGUILayout.LabelField("🔍", GUILayout.Width(18));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.MinWidth(100));
            if (!string.IsNullOrEmpty(_searchFilter))
            {
if (GUILayout.Button("✗", GUILayout.Width(22)))
                    _searchFilter = "";
            }

            GUILayout.FlexibleSpace();

            // 仅显示警告开关
            var prevColor = GUI.backgroundColor;
            if (_onlyShowWarnings) GUI.backgroundColor = new Color(1f, 0.85f, 0.5f);
            if (GUILayout.Button(_onlyShowWarnings ? "⚠仅警告" : "📋 全部", EditorStyles.miniButton, GUILayout.Width(65)))
            {
                _onlyShowWarnings = !_onlyShowWarnings;
            }
            GUI.backgroundColor = prevColor;

            // 全部展开/收起
            if (GUILayout.Button("展开", EditorStyles.miniButtonLeft, GUILayout.Width(36)))
            {
                foreach (var key in _foldoutStates.Keys.ToList())
                    _foldoutStates[key] = true;
            }
            if (GUILayout.Button("收起", EditorStyles.miniButtonRight, GUILayout.Width(36)))
            {
                foreach (var key in _foldoutStates.Keys.ToList())
                    _foldoutStates[key] = false;
            }
        }
        EditorGUILayout.Space(2);
    }

    /// <summary>
    /// 绘制 VFX 检查项行：开关 + 影响程度 + 名称 + 阈值（带 tooltip）
    /// </summary>
    private void DrawVFXCheckRow(ref bool toggle, string impact, string label, string tooltip)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            toggle = EditorGUILayout.Toggle(toggle, GUILayout.Width(16));
            DrawImpactLabel(impact);
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.MinWidth(130));
        }
    }

    private void DrawVFXCheckRow(ref bool toggle, string impact, string label, string tooltip, ref int threshold)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            toggle = EditorGUILayout.Toggle(toggle, GUILayout.Width(16));
            DrawImpactLabel(impact);
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(130));
            GUI.enabled = toggle;
            threshold = EditorGUILayout.IntField(new GUIContent("", tooltip), threshold, GUILayout.Width(60));
            GUI.enabled = true;
        }
    }

    private void DrawVFXCheckRow(ref bool toggle, string impact, string label, string tooltip, ref float threshold)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            toggle = EditorGUILayout.Toggle(toggle, GUILayout.Width(16));
            DrawImpactLabel(impact);
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(130));
            GUI.enabled = toggle;
            threshold = EditorGUILayout.FloatField(new GUIContent("", tooltip), threshold, GUILayout.Width(60));
            GUI.enabled = true;
        }
    }

    private void DrawImpactLabel(string impact)
    {
        var style = new GUIStyle(EditorStyles.miniLabel);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        switch (impact)
        {
            case "极高":
                style.normal.textColor = new Color(1f, 0.3f, 0.3f);
                break;
            case "高":
                style.normal.textColor = new Color(1f, 0.7f, 0.2f);
                break;
            case "中":
                style.normal.textColor = new Color(0.4f, 0.75f, 1f);
                break;
            default:
                style.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                break;
        }

        EditorGUILayout.LabelField(new GUIContent($"[{impact}]"), style, GUILayout.Width(36));
    }

    // ==================================================================================
    //  VFX 检查范围面板
    // ==================================================================================
    private void DrawVFXScopePanel()
    {
        _vfxScopeTargets.RemoveAll(obj => obj == null);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("检查范围", EditorStyles.boldLabel, GUILayout.Width(60));
                var hintStyle = new GUIStyle(EditorStyles.miniLabel);
                if (_vfxScopeTargets.Count == 0)
                {
                    hintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                    EditorGUILayout.LabelField("未指定，将检查全场景", hintStyle);
                }
                else
                {
                    hintStyle.normal.textColor = new Color(0.3f, 0.8f, 0.4f);
                    EditorGUILayout.LabelField($"已添加 {_vfxScopeTargets.Count} 个对象", hintStyle);
                }
                GUILayout.FlexibleSpace();
                if (_vfxScopeTargets.Count > 0)
                {
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(40)))
                        _vfxScopeTargets.Clear();
                }
            }

            if (_vfxScopeTargets.Count > 0)
            {
                float listHeight = Mathf.Min(_vfxScopeTargets.Count * 20f, 100f);
                _vfxScopeScrollPos = EditorGUILayout.BeginScrollView(_vfxScopeScrollPos, GUILayout.Height(listHeight));
                int removeIdx = -1;
                for (int i = 0; i < _vfxScopeTargets.Count; i++)
                {
                    var obj = _vfxScopeTargets[i];
                    if (obj == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(6);
                        if (GUILayout.Button(obj.name, EditorStyles.label, GUILayout.MinWidth(100), GUILayout.Height(18)))
                        {
                            Selection.activeGameObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                        GUILayout.FlexibleSpace();
                        int psCount = obj.GetComponentsInChildren<ParticleSystem>(true).Length;
                        EditorGUILayout.LabelField($"{psCount} PS", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight }, GUILayout.Width(50));
                        if (GUILayout.Button("✗", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(16)))
                            removeIdx = i;
                    }
                }
                if (removeIdx >= 0) _vfxScopeTargets.RemoveAt(removeIdx);
                EditorGUILayout.EndScrollView();
            }

            var dropArea = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            var dropStyle = new GUIStyle(EditorStyles.helpBox);
            dropStyle.alignment = TextAnchor.MiddleCenter;
            dropStyle.fontSize = 11;
            dropStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
            bool isDraggingOver = dropArea.Contains(Event.current.mousePosition) && DragAndDrop.objectReferences.Length > 0;
            var prevBg = GUI.backgroundColor;
            if (isDraggingOver) GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 0.8f);
            GUI.Box(dropArea, _vfxScopeTargets.Count == 0 ? "拖入 GameObject 指定检查范围（不添加则检查全场景）" : "+ 继续拖入", dropStyle);
            GUI.backgroundColor = prevBg;
            HandleVFXDropArea(dropArea);
        }
        EditorGUILayout.Space(2);
    }

    private void HandleVFXDropArea(Rect dropArea)
    {
        var evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        if (!dropArea.Contains(evt.mousePosition)) return;
        if (!DragAndDrop.objectReferences.Any(o => o is GameObject)) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && !_vfxScopeTargets.Contains(go))
                    _vfxScopeTargets.Add(go);
            }
        }
        evt.Use();
    }

    /// <summary>
    /// 根据 VFX 检查范围获取组件列表（自动去重，防止嵌套对象导致重复）
    /// </summary>
    private List<T> GetVFXScopedComponents<T>() where T : Component
    {
        _vfxScopeTargets.RemoveAll(obj => obj == null);
        if (_vfxScopeTargets.Count == 0)
            return FindAllComponentsInScene<T>();

        var set = new HashSet<T>();
        foreach (var root in _vfxScopeTargets)
        {
            if (root == null) continue;
            foreach (var comp in root.GetComponentsInChildren<T>(true))
                set.Add(comp);
        }
        return set.ToList();
    }

    // ==================================================================================
    //  VFX 一键修复
    // ==================================================================================

    /// <summary>
    /// 对指定分类执行一键修复
    /// </summary>
    private void FixVFXCategory(string category)
    {
        var targets = _vfxResults.Where(r =>
            r.category == category &&
            (r.msgType == MessageType.Warning || r.msgType == MessageType.Error) &&
            r.targetObj != null).ToList();

        if (targets.Count == 0) return;

        int fixedCount = 0;

        foreach (var result in targets)
        {
            var go = result.targetObj;
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) continue;

            Undo.RecordObject(ps, $"修复粒子特效 - {category}");

            switch (category)
            {
                case "MaxParticles 超标":
                {
                    var main = ps.main;
                    main.maxParticles = _maxParticlesThreshold;
                    fixedCount++;
                    break;
                }
                case "EmissionRate 超标":
                {
                    var emission = ps.emission;
                    emission.rateOverTime = ClampMinMaxCurve(emission.rateOverTime, _emissionRateThreshold);
                    fixedCount++;
                    break;
                }
                case "Burst 超标":
                {
                    var emission = ps.emission;
                    for (int i = 0; i < emission.burstCount; i++)
                    {
                        var burst = emission.GetBurst(i);
                        if (burst.count.constantMax > _burstCountThreshold)
                        {
                            burst.count = new ParticleSystem.MinMaxCurve(_burstCountThreshold);
                            emission.SetBurst(i, burst);
                        }
                    }
                    fixedCount++;
                    break;
                }
                case "Start Lifetime 超标":
                {
                    var main = ps.main;
                    main.startLifetime = ClampMinMaxCurve(main.startLifetime, _startLifetimeThreshold);
                    fixedCount++;
                    break;
                }
                case "粒子 Collision 模块":
                {
                    var col = ps.collision;
                    col.enabled = false;
                    fixedCount++;
                    break;
                }
                case "粒子 Sub Emitters":
                {
                    var sub = ps.subEmitters;
                    sub.enabled = false;
                    fixedCount++;
                    break;
                }
                case "粒子 Lights 模块":
                {
                    var lights = ps.lights;
                    lights.maxLights = _maxLightsThreshold;
                    if (_maxLightsThreshold == 0)
                        lights.enabled = false;
                    fixedCount++;
                    break;
                }
                case "粒子 Mesh 渲染模式":
                {
                    var psr = go.GetComponent<ParticleSystemRenderer>();
                    if (psr != null)
                    {
                        Undo.RecordObject(psr, "修复粒子 Mesh 渲染模式");
                        psr.renderMode = ParticleSystemRenderMode.Billboard;
                    }
                    fixedCount++;
                    break;
                }
                case "粒子 Prewarm":
                {
                    var main = ps.main;
                    main.prewarm = false;
                    fixedCount++;
                    break;
                }
                case "粒子 Trails 拖尾":
                {
                    var trails = ps.trails;
                    trails.enabled = false;
                    fixedCount++;
                    break;
                }
                case "粒子 Noise 模块":
                {
                    var noise = ps.noise;
                    noise.enabled = false;
                    fixedCount++;
                    break;
                }
                case "粒子 Trigger 模块":
                {
                    var trigger = ps.trigger;
                    trigger.enabled = false;
                    fixedCount++;
                    break;
                }
                case "粒子投射阴影":
                {
                    var renderer = go.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        Undo.RecordObject(renderer, "修复粒子投射阴影");
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    fixedCount++;
                    break;
                }
                case "粒子材质 Cull Off":
                {
                    var renderer = go.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat == null) continue;
                            if (mat.HasProperty("_Cull") && (int)mat.GetFloat("_Cull") == 0)
                            {
                                Undo.RecordObject(mat, "修复粒子材质 Cull Off (共享材质，影响所有引用)");
                                mat.SetFloat("_Cull", 2f); // Back
                                EditorUtility.SetDirty(mat);
                            }
                        }
                    }
                    fixedCount++;
                    break;
                }
            }

            EditorUtility.SetDirty(go);
        }

        if (fixedCount > 0)
        {
            Debug.Log($"[场景优化工具] 已修复 {fixedCount} 个「{category}」问题");
            var savedFoldouts = new Dictionary<string, bool>(_foldoutStates);
            CheckVFX();
            foreach (var kv in savedFoldouts)
            {
                if (!_foldoutStates.ContainsKey(kv.Key))
                    _foldoutStates[kv.Key] = kv.Value;
                else
                    _foldoutStates[kv.Key] = kv.Value;
            }
        }
    }

    /// <summary>
    /// 判断某分类是否支持一键修复
    /// </summary>
    private bool CanFixCategory(string category)
    {
        switch (category)
        {
            case "MaxParticles 超标":
            case "EmissionRate 超标":
            case "Burst 超标":
            case "Start Lifetime 超标":
            case "粒子 Collision 模块":
            case "粒子 Sub Emitters":
            case "粒子 Lights 模块":
            case "粒子 Mesh 渲染模式":
            case "粒子 Prewarm":
            case "粒子 Trails 拖尾":
            case "粒子 Noise 模块":
            case "粒子 Trigger 模块":
            case "粒子投射阴影":
            case "粒子材质 Cull Off":
                return true;
            default:
                return false;
        }
    }

    private void CheckVFX()
    {
        _vfxResults.Clear();
        _foldoutStates.Clear();
        _hasCheckedVFX = true;

        var allParticleSystems = GetVFXScopedComponents<ParticleSystem>();
        var allTrailRenderers = GetVFXScopedComponents<TrailRenderer>();
        var allLineRenderers = GetVFXScopedComponents<LineRenderer>();

        // --- 1. 粒子系统数量统计 ---
        bool vfxIsScoped = _vfxScopeTargets.Count > 0;
        string scopeDesc = vfxIsScoped
            ? $"检查范围内共有 {allParticleSystems.Count} 个 ParticleSystem（{string.Join(", ", _vfxScopeTargets.Where(o => o != null).Select(o => o.name))}）"
            : $"场景中共有 {allParticleSystems.Count} 个 ParticleSystem";
        _vfxResults.Add(new CheckResult
        {
            category = "粒子系统总览",
            description = scopeDesc,
            msgType = MessageType.Info
        });

        // --- 2. MaxParticles 检查---
        if (_chkMaxParticles)
        {
            int maxParticleWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                var main = ps.main;
                if (main.maxParticles > _maxParticlesThreshold)
                {
                    maxParticleWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "MaxParticles 超标",
                        description = $"MaxParticles = {main.maxParticles}（标准 {_maxParticlesThreshold}）路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (maxParticleWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "MaxParticles 超标",
                    description = $"✓ 所有粒子系统的 MaxParticles 均在标准 {_maxParticlesThreshold} 以内",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 3. Emission Rate 检查---
        if (_chkEmissionRate)
        {
            int emissionWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                var emission = ps.emission;
                if (!emission.enabled) continue;

                float rate = GetMinMaxCurveMax(emission.rateOverTime);
                if (rate > _emissionRateThreshold)
                {
                    emissionWarnings++;
                    string modeHint = emission.rateOverTime.mode != ParticleSystemCurveMode.Constant ? $" [{emission.rateOverTime.mode}]" : "";
                    _vfxResults.Add(new CheckResult
                    {
                        category = "EmissionRate 超标",
                        description = $"RateOverTime = {rate:F1}{modeHint}（标准 {_emissionRateThreshold}）路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (emissionWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "EmissionRate 超标",
                    description = $"✓ 所有粒子系统的 EmissionRate 均在标准 {_emissionRateThreshold} 以内",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 4. Burst 数量检查 ---
        if (_chkBurst)
        {
            int burstWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                var emission = ps.emission;
                if (!emission.enabled) continue;
                int burstCount = emission.burstCount;
                for (int i = 0; i < burstCount; i++)
                {
                    var burst = emission.GetBurst(i);
                    int maxCount = (int)burst.count.constantMax;
                    if (maxCount > _burstCountThreshold)
                    {
                        burstWarnings++;
                        _vfxResults.Add(new CheckResult
                        {
                            category = "Burst 超标",
                            description = $"Burst[{i}] count = {maxCount}（标准 {_burstCountThreshold}）路径: {GetHierarchyPath(ps.gameObject)}",
                            targetObj = ps.gameObject,
                            msgType = MessageType.Warning
                        });
                    }
                }
            }
            if (burstWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "Burst 超标",
                    description = $"✓ 所有粒子系统的 Burst 数量均在标准 {_burstCountThreshold} 以内",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 5. Start Lifetime 检查 ---
        if (_chkStartLifetime)
        {
            int lifetimeWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                float lifetime = GetMinMaxCurveMax(ps.main.startLifetime);
                if (lifetime > _startLifetimeThreshold)
                {
                    lifetimeWarnings++;
                    string modeHint = ps.main.startLifetime.mode != ParticleSystemCurveMode.Constant ? $" [{ps.main.startLifetime.mode}]" : "";
                    _vfxResults.Add(new CheckResult
                    {
                        category = "Start Lifetime 超标",
                        description = $"StartLifetime = {lifetime:F1}s{modeHint}（标准 {_startLifetimeThreshold}s）路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (lifetimeWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "Start Lifetime 超标",
                    description = $"✓ 所有粒子系统的 StartLifetime 均在标准 {_startLifetimeThreshold}s 以内",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 6. Collision 模块检查 ---
        if (_chkCollision)
        {
            int collisionWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                if (ps.collision.enabled)
                {
                    collisionWarnings++;
                    string quality = ps.collision.quality.ToString();
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子 Collision 模块",
                        description = $"Collision 已启用（Quality: {quality}），路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (collisionWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子 Collision 模块",
                    description = "✓ 所有粒子系统均未启用 Collision 模块",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 7. Sub Emitters 检查 ---
        if (_chkSubEmitters)
        {
            int subEmitterWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                if (ps.subEmitters.enabled && ps.subEmitters.subEmittersCount > 0)
                {
                    subEmitterWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子 Sub Emitters",
                        description = $"Sub Emitters 数量 = {ps.subEmitters.subEmittersCount}，路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (subEmitterWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子 Sub Emitters",
                    description = "✓ 所有粒子系统均未使用 Sub Emitters",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 8. Lights 模块检查 ---
        if (_chkLights)
        {
            int lightsWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                if (ps.lights.enabled)
                {
                    int maxLights = ps.lights.maxLights;
                    lightsWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子 Lights 模块",
                        description = $"Lights 已启用，MaxLights = {maxLights}（建议 ≤{_maxLightsThreshold}），路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = maxLights > _maxLightsThreshold ? MessageType.Error : MessageType.Warning
                    });
                }
            }
            if (lightsWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子 Lights 模块",
                    description = "✓ 所有粒子系统均未启用 Lights 模块",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 9. Mesh 渲染模式检查 ---
        if (_chkMeshMode)
        {
            int meshModeWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (psr != null && psr.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    string meshName = psr.mesh != null ? psr.mesh.name : "(null)";
                    int meshVerts = psr.mesh != null ? psr.mesh.vertexCount : 0;
                    meshModeWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子 Mesh 渲染模式",
                        description = $"使用 Mesh 模式（Mesh: {meshName}, 顶点数: {meshVerts}），路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (meshModeWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子 Mesh 渲染模式",
                    description = "✓ 所有粒子系统均未使用 Mesh 渲染模式",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 10. Prewarm 检查 ---
        if (_chkPrewarm)
        {
            int prewarmWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                if (ps.main.prewarm)
                {
                    prewarmWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子 Prewarm",
                        description = $"Prewarm 已启用，路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (prewarmWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子 Prewarm",
                    description = "✓ 所有粒子系统均未启用 Prewarm",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 11. Trails 拖尾检查 ---
        if (_chkTrails)
        {
            int trailsWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                if (ps.trails.enabled)
                {
                    trailsWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子 Trails 拖尾",
                        description = $"Trails 已启用，路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (trailsWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子 Trails 拖尾",
                    description = "✓ 所有粒子系统均未启用 Trails 拖尾",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 12. Noise 模块检查 ---
        if (_chkNoise)
        {
            int noiseWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                if (ps.noise.enabled)
                {
                    string quality = ps.noise.quality.ToString();
                    noiseWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子 Noise 模块",
                        description = $"Noise 已启用（Quality: {quality}），路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (noiseWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子 Noise 模块",
                    description = "✓ 所有粒子系统均未启用 Noise 模块",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 13. Trigger 模块检查 ---
        if (_chkTrigger)
        {
            int triggerWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                if (ps.trigger.enabled)
                {
                    triggerWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子 Trigger 模块",
                        description = $"Trigger 已启用，路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (triggerWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子 Trigger 模块",
                    description = "✓ 所有粒子系统均未启用 Trigger 模块",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 14. Shadow Casting 检查---
        if (_chkShadowCasting)
        {
            int shadowWarnings = 0;
            foreach (var ps in allParticleSystems)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    shadowWarnings++;
                    _vfxResults.Add(new CheckResult
                    {
                        category = "粒子投射阴影",
                        description = $"CastShadows = {renderer.shadowCastingMode}，路径: {GetHierarchyPath(ps.gameObject)}",
                        targetObj = ps.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (shadowWarnings == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子投射阴影",
                    description = "✓ 所有粒子 Renderer 均未开启投射阴影",
                    msgType = MessageType.Info
                });
            }
        }

        // --- 15. Trail / Line 渲染器检查---
        if (_chkTrailLine)
        {
            _vfxResults.Add(new CheckResult
            {
                category = "Trail/Line 渲染器",
                description = $"场景中共有 {allTrailRenderers.Count} 个 TrailRenderer，{allLineRenderers.Count} 个 LineRenderer",
                msgType = MessageType.Info
            });

            foreach (var trail in allTrailRenderers)
            {
                if (trail.shadowCastingMode != ShadowCastingMode.Off)
                {
                    _vfxResults.Add(new CheckResult
                    {
                        category = "Trail/Line 渲染器",
                        description = $"TrailRenderer 开启了阴影投射，路径: {GetHierarchyPath(trail.gameObject)}",
                        targetObj = trail.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }

            foreach (var line in allLineRenderers)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "Trail/Line 渲染器",
                    description = $"LineRenderer 顶点数 {line.positionCount}，路径: {GetHierarchyPath(line.gameObject)}",
                    targetObj = line.gameObject,
                    msgType = line.positionCount > _lineRendererVertThreshold ? MessageType.Warning : MessageType.Info
                });
            }
        }

        // --- 16. 粒子材质 Cull 检查---
        if (_chkCullOff)
        {
            int cullOffCount = 0;
            foreach (var ps in allParticleSystems)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;

                var mats = renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_Cull"))
                    {
                        int cullValue = (int)mat.GetFloat("_Cull");
                        if (cullValue == 0)
                        {
                            cullOffCount++;
                            _vfxResults.Add(new CheckResult
                            {
                                category = "粒子材质 Cull Off",
                                description = $"材质 \"{mat.name}\" (Shader: {mat.shader.name}) Cull Off，路径: {GetHierarchyPath(ps.gameObject)}",
                                targetObj = ps.gameObject,
                                msgType = MessageType.Warning
                            });
                        }
                    }
                }
            }
            if (cullOffCount == 0)
            {
                _vfxResults.Add(new CheckResult
                {
                    category = "粒子材质 Cull Off",
                    description = "✓ 未发现粒子材质使用 Cull Off",
                    msgType = MessageType.Info
                });
            }
        }

        Debug.Log($"[场景优化工具] 特效检查完成，共 {_vfxResults.Count} 条结果");
        Repaint();
    }

    // ==================================================================================
    //  材质 Tab
    // ==================================================================================

    private void CheckMaterials()
    {
        _materialResults.Clear();
        _foldoutStates.Clear();
        _hasCheckedMat = true;

        var allRenderers = FindAllComponentsInScene<Renderer>();

        // 收集场景中所有材质及其关联的 Renderer
        var matToRenderers = new Dictionary<Material, List<Renderer>>();
        var allMaterials = new HashSet<Material>();
        int nullMatCount = 0;
        var nullMatRenderers = new List<Renderer>();

        foreach (var renderer in allRenderers)
        {
            var mats = renderer.sharedMaterials;

            // ParticleSystemRenderer 特殊处理：Material(slot0) 和 TrailMaterial(slot1) 只要有一个不为空就不算空材质
            bool isParticleRenderer = renderer is ParticleSystemRenderer;
            if (isParticleRenderer)
            {
                bool hasAnyMat = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null) { hasAnyMat = true; break; }
                }
                // 收集非空材质
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    allMaterials.Add(mats[i]);
                    if (!matToRenderers.ContainsKey(mats[i]))
                        matToRenderers[mats[i]] = new List<Renderer>();
                    matToRenderers[mats[i]].Add(renderer);
                }
                // 两个槽位都为空才算空材质引用
                if (!hasAnyMat)
                {
                    nullMatCount++;
                    if (!nullMatRenderers.Contains(renderer))
                        nullMatRenderers.Add(renderer);
                }
                continue;
            }

            // 非粒子 Renderer：任何一个槽位为空都算空材质引用
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                {
                    nullMatCount++;
                    if (!nullMatRenderers.Contains(renderer))
                        nullMatRenderers.Add(renderer);
                    continue;
                }
                allMaterials.Add(mats[i]);
                if (!matToRenderers.ContainsKey(mats[i]))
                    matToRenderers[mats[i]] = new List<Renderer>();
                matToRenderers[mats[i]].Add(renderer);
            }
        }

        // --- 1. 空材质引用检查---
        if (nullMatCount > 0)
        {
            _materialResults.Add(new CheckResult
            {
                category = "空材质引用",
                description = $"⚠ 发现 {nullMatCount} 个空材质引用，涉及 {nullMatRenderers.Count} 个 Renderer",
                msgType = MessageType.Warning
            });
            foreach (var r in nullMatRenderers)
            {
                _materialResults.Add(new CheckResult
                {
                    category = "空材质引用",
                    description = $"路径: {GetHierarchyPath(r.gameObject)}",
                    targetObj = r.gameObject,
                    msgType = MessageType.Warning
                });
            }
        }
        else
        {
            _materialResults.Add(new CheckResult
            {
                category = "空材质引用",
                description = "✓ 无空材质引用",
                msgType = MessageType.Info
            });
        }

        // --- 2. Shader 分类统计 ---
        var shaderGroups = new Dictionary<string, List<Material>>();
        foreach (var mat in allMaterials)
        {
            string shaderName = mat.shader != null ? mat.shader.name : "(null shader)";
            if (!shaderGroups.ContainsKey(shaderName))
                shaderGroups[shaderName] = new List<Material>();
            shaderGroups[shaderName].Add(mat);
        }

        _materialResults.Add(new CheckResult
        {
            category = "Shader 分类统计",
            description = $"场景中共使用 {shaderGroups.Count} 种 Shader，{allMaterials.Count} 个材质",
            msgType = MessageType.Info
        });

        foreach (var kv in shaderGroups.OrderByDescending(x => x.Value.Count))
        {
            _materialResults.Add(new CheckResult
            {
                category = "Shader 分类统计",
                description = $"  [{kv.Value.Count} 个材质] Shader: {kv.Key}",
                msgType = MessageType.None
            });
        }

        // --- 3. 冗余材质检测---
        // 对比相同 Shader 下所有属性完全一致的材质
        int redundantGroupCount = 0;
        foreach (var kv in shaderGroups)
        {
            if (kv.Value.Count <= 1) continue;

            // 对每个材质生成属性哈希
            var hashToMats = new Dictionary<string, List<Material>>();
            foreach (var mat in kv.Value)
            {
                string hash = GetMaterialPropertyHash(mat);
                if (!hashToMats.ContainsKey(hash))
                    hashToMats[hash] = new List<Material>();
                hashToMats[hash].Add(mat);
            }

            foreach (var hkv in hashToMats)
            {
                if (hkv.Value.Count > 1)
                {
                    redundantGroupCount++;
                    string matNames = string.Join(", ", hkv.Value.Select(m => m.name));
                    _materialResults.Add(new CheckResult
                    {
                        category = "冗余材质",
                        description = $"以下 {hkv.Value.Count} 个材质属性完全一致，可合并: {matNames} (Shader: {kv.Key})",
                        msgType = MessageType.Warning
                    });
                }
            }
        }
        if (redundantGroupCount == 0)
        {
            _materialResults.Add(new CheckResult
            {
                category = "冗余材质",
                description = "✓ 未发现冗余材质",
                msgType = MessageType.Info
            });
        }

        // --- 4. 贴图尺寸统计（1024 当量）---
        var textureSet = new HashSet<Texture>();
        foreach (var mat in allMaterials)
        {
            var shader = mat.shader;
            int propCount = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    string propName = ShaderUtil.GetPropertyName(shader, i);
                    var tex = mat.GetTexture(propName);
                    if (tex != null)
                        textureSet.Add(tex);
                }
            }
        }

        // 计算 1024 当量（面积比：tex.w * tex.h / (1024 * 1024)）
        float totalEquivalent = 0f;
        int texCount1024 = 0, texCount2048 = 0, texCount512 = 0, texCount256 = 0, texCountOther = 0;
        var oversizedTextures = new List<string>();

        foreach (var tex in textureSet)
        {
            int w = tex.width;
            int h = tex.height;
            float equiv = (float)(w * h) / (1024f * 1024f);
            totalEquivalent += equiv;

            int maxSide = Mathf.Max(w, h);
            if (maxSide >= 2048) texCount2048++;
            else if (maxSide >= 1024) texCount1024++;
            else if (maxSide >= 512) texCount512++;
            else if (maxSide >= 256) texCount256++;
            else texCountOther++;

            if (maxSide > _textureHighThreshold)
            {
                oversizedTextures.Add($"{tex.name} ({w}x{h})");
            }
        }

        _materialResults.Add(new CheckResult
        {
            category = "贴图尺寸统计",
            description = $"共 {textureSet.Count} 张贴图，总计 {totalEquivalent:F2} 个 1024 当量（按面积换算）",
            msgType = MessageType.Info
        });
        _materialResults.Add(new CheckResult
        {
            category = "贴图尺寸统计",
            description = $"  2048+: {texCount2048} 张 | 1024: {texCount1024} 张 | 512: {texCount512} 张 | 256: {texCount256} 张 | 其他: {texCountOther} 张",
            msgType = MessageType.None
        });

        if (oversizedTextures.Count > 0)
        {
            _materialResults.Add(new CheckResult
            {
                category = "贴图尺寸统计",
                description = $"⚠ {oversizedTextures.Count} 张贴图超出标准 {_textureHighThreshold}:",
                msgType = MessageType.Warning
            });
            foreach (var texInfo in oversizedTextures)
            {
                _materialResults.Add(new CheckResult
                {
                    category = "贴图尺寸统计",
                    description = $"    {texInfo}",
                    msgType = MessageType.None
                });
            }
        }

        // --- 5. GPU Instancing 检查---
        int canInstanceCount = 0;
        foreach (var mat in allMaterials)
        {
            if (mat.shader != null && mat.enableInstancing == false)
            {
                // 检查 Shader 是否支持 GPU Instancing
                bool supportsInstancing = false;
                try
                {
                    // 通过 SerializedObject 检查 shader 是否有 instancing 变体
                    supportsInstancing = mat.shader.name != "Hidden/InternalErrorShader";
                }
                catch { }

                if (supportsInstancing)
                {
                    canInstanceCount++;
                    if (canInstanceCount <= 20) // 最多显示20条
                    {
                        _materialResults.Add(new CheckResult
                        {
                            category = "GPU Instancing",
                            description = $"材质 \"{mat.name}\" (Shader: {mat.shader.name}) 未开启GPU Instancing",
                            msgType = MessageType.Info
                        });
                    }
                }
            }
        }
        if (canInstanceCount > 20)
        {
            _materialResults.Add(new CheckResult
            {
                category = "GPU Instancing",
                description = $"...还有 {canInstanceCount - 20} 个材质未开启GPU Instancing（共 {canInstanceCount} 个）",
                msgType = MessageType.Info
            });
        }
        else if (canInstanceCount == 0)
        {
            _materialResults.Add(new CheckResult
            {
                category = "GPU Instancing",
                description = "✓ 所有材质均已开启GPU Instancing 或不适用",
                msgType = MessageType.Info
            });
        }

Debug.Log($"[场景优化工具] 材质检查完成，共 {_materialResults.Count} 条结果");
        Repaint();
    }

    /// <summary>
    /// 生成材质属性的哈希值，用于冗余材质检测
    /// </summary>
    private string GetMaterialPropertyHash(Material mat)
    {
        var sb = new StringBuilder();
        sb.Append(mat.shader.name).Append("|");

        var shader = mat.shader;
        int propCount = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < propCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);
            var propType = ShaderUtil.GetPropertyType(shader, i);

            sb.Append(propName).Append("=");
            switch (propType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    sb.Append(mat.GetColor(propName).ToString());
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    sb.Append(mat.GetFloat(propName).ToString("F6"));
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    sb.Append(mat.GetVector(propName).ToString());
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    var tex = mat.GetTexture(propName);
                    sb.Append(tex != null ? tex.GetInstanceID().ToString() : "null");
                    var offset = mat.GetTextureOffset(propName);
                    var scale = mat.GetTextureScale(propName);
                    sb.Append($"_o{offset}_s{scale}");
                    break;
            }
            sb.Append("|");
        }

        // 添加 keywords
        var keywords = mat.shaderKeywords;
        Array.Sort(keywords);
        sb.Append("kw:").Append(string.Join(",", keywords));

        // 生成 MD5 哈希
        using (var md5 = MD5.Create())
        {
            byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(data).Replace("-", "");
        }
    }

    // ==================================================================================
    //  模型面数 Tab - 范围选择面板
    // ==================================================================================

    /// <summary>
    /// 清理已被删除的空引用
    /// </summary>
    private void CleanupScopeTargets()
    {
        _meshScopeTargets.RemoveAll(obj => obj == null);
    }

    /// <summary>
    /// 绘制模型检查范围选择面板（拖拽式）
    /// </summary>
    private void DrawMeshScopePanel()
    {
        CleanupScopeTargets();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // 标题行
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("检查范围", EditorStyles.boldLabel, GUILayout.Width(60));

                // 状态提示
                var hintStyle = new GUIStyle(EditorStyles.miniLabel);
                if (_meshScopeTargets.Count == 0)
                {
                    hintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                    EditorGUILayout.LabelField("未指定，将检查全场景", hintStyle);
                }
                else
                {
                    hintStyle.normal.textColor = new Color(0.3f, 0.8f, 0.4f);
EditorGUILayout.LabelField($"已添加 {_meshScopeTargets.Count} 个对象", hintStyle);
                }

                GUILayout.FlexibleSpace();

                // 清空按钮
                if (_meshScopeTargets.Count > 0)
                {
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(40)))
                        _meshScopeTargets.Clear();
                }
            }

            EditorGUILayout.Space(2);

            // 已添加的对象列表
            if (_meshScopeTargets.Count > 0)
            {
                float listHeight = Mathf.Min(_meshScopeTargets.Count * 22f, 150f);
                _scopeScrollPos = EditorGUILayout.BeginScrollView(_scopeScrollPos, GUILayout.Height(listHeight));

                int removeIndex = -1;
                for (int i = 0; i < _meshScopeTargets.Count; i++)
                {
                    var obj = _meshScopeTargets[i];
                    if (obj == null) continue;

                    // 计算该obj 下的总三角面数
                    int objTriCount = CalcTriangleCount(obj);
                    bool overThreshold = objTriCount > _meshScopeWarnThreshold;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(6);

                        // 对象图标 + 名称（可点击定位）
                        var nameStyle = new GUIStyle(EditorStyles.label);
                        nameStyle.richText = true;

                        int descCount = obj.GetComponentsInChildren<Transform>(true).Length - 1;
string displayName = $"{obj.name}  <color=#888888>({descCount} 子对象)</color>";

                        if (GUILayout.Button(displayName, nameStyle, GUILayout.MinWidth(120), GUILayout.Height(18)))
                        {
                            Selection.activeGameObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }

                        GUILayout.FlexibleSpace();

                        // 面数显示（超标准时警告色）
                        var triStyle = new GUIStyle(EditorStyles.miniLabel);
                        triStyle.alignment = TextAnchor.MiddleRight;
                        triStyle.normal.textColor = overThreshold
                            ? new Color(1f, 0.6f, 0.2f)
                            : new Color(0.55f, 0.55f, 0.55f);
                        string triText = overThreshold
                            ? $"⚠ {objTriCount:N0} 面"
                            : $"{objTriCount:N0} 面";
                        EditorGUILayout.LabelField(triText, triStyle, GUILayout.Width(90), GUILayout.Height(18));

                        // 删除按钮
if (GUILayout.Button("✗", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(16)))
                            removeIndex = i;
                    }
                }

                if (removeIndex >= 0)
                    _meshScopeTargets.RemoveAt(removeIndex);

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(2);
            }

            // 拖拽区域
            var dropArea = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            var dropStyle = new GUIStyle(EditorStyles.helpBox);
            dropStyle.alignment = TextAnchor.MiddleCenter;
            dropStyle.fontSize = 11;
            dropStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f);

            // 拖拽悬停高亮
            bool isDraggingOver = dropArea.Contains(Event.current.mousePosition) && DragAndDrop.objectReferences.Length > 0;
            var prevBg = GUI.backgroundColor;
            if (isDraggingOver)
                GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 0.8f);

            GUI.Box(dropArea, _meshScopeTargets.Count == 0 ? "拖入 GameObject 指定检查范围（不添加则检查全场景）" : "+ 继续拖入更多对象", dropStyle);
            GUI.backgroundColor = prevBg;

            // 处理拖拽事件
            HandleDropArea(dropArea);
        }
        EditorGUILayout.Space(2);
    }

    /// <summary>
    /// 处理拖拽区域的拖放事件
    /// </summary>
    private void HandleDropArea(Rect dropArea)
    {
        var evt = Event.current;

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition)) return;

                // 检查是否有 GameObject
                bool hasGameObject = DragAndDrop.objectReferences.Any(o => o is GameObject);
                if (!hasGameObject) return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (var draggedObj in DragAndDrop.objectReferences)
                    {
                        if (draggedObj is GameObject go)
                        {
                            // 避免重复添加
                            if (!_meshScopeTargets.Contains(go))
                                _meshScopeTargets.Add(go);
                        }
                    }
                }

                evt.Use();
                break;
        }
    }

    /// <summary>
    /// 根据检查范围获取需要搜索的根对象列表
    /// 列表为空时返回全场景根对象，否则返回用户拖入的对象
    /// </summary>
    private List<GameObject> GetMeshCheckRoots()
    {
        CleanupScopeTargets();

        if (_meshScopeTargets.Count == 0)
        {
            // 无指定对象：返回场景所有根对象
            var roots = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                roots.AddRange(scene.GetRootGameObjects());
            }
            return roots;
        }
        else
        {
            // 返回用户拖入的对象
            return new List<GameObject>(_meshScopeTargets);
        }
    }

    /// <summary>
    /// 在指定的根对象列表下查找所有指定类型的组件
    /// </summary>
    private List<T> FindComponentsInRoots<T>(List<GameObject> roots) where T : Component
    {
        var result = new List<T>();
        foreach (var root in roots)
        {
            if (root == null) continue;
            result.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return result;
    }

    /// <summary>
    /// 判断一一GameObject 属于哪个根对象
    /// </summary>
    private GameObject GetRootParent(GameObject go)
    {
        var t = go.transform;
        while (t.parent != null)
            t = t.parent;
        return t.gameObject;
    }

    // ==================================================================================
    //  模型面数 Tab
    // ==================================================================================

    private void CheckMesh()
    {
        _meshResults.Clear();
        _foldoutStates.Clear();
        _hasCheckedMesh = true;

        var checkRoots = GetMeshCheckRoots();
        bool isScoped = _meshScopeTargets.Count > 0;

        // 显示检查范围信息
        if (isScoped)
        {
            string scopeNames = string.Join(", ", checkRoots.Select(r => r.name));
            _meshResults.Add(new CheckResult
            {
                category = "检查范围",
                description = $"当前检查范围 {scopeNames}",
                msgType = MessageType.Info
            });
        }

        // --- 1. Mesh 三角面/顶点数统计---
        var meshInfoList = new List<(GameObject go, UnityEngine.Mesh mesh, int triangles, int vertices, string rendererType)>();

        // MeshFilter + MeshRenderer
        var allMeshFilters = FindComponentsInRoots<MeshFilter>(checkRoots);
        foreach (var mf in allMeshFilters)
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            meshInfoList.Add((mf.gameObject, mesh, mesh.triangles.Length / 3, mesh.vertexCount, "MeshRenderer"));
        }

        // SkinnedMeshRenderer
        var allSkinned = FindComponentsInRoots<SkinnedMeshRenderer>(checkRoots);
        foreach (var smr in allSkinned)
        {
            var mesh = smr.sharedMesh;
            if (mesh == null) continue;
            meshInfoList.Add((smr.gameObject, mesh, mesh.triangles.Length / 3, mesh.vertexCount, "SkinnedMesh"));
        }

        // ParticleSystemRenderer 上的 mesh
        var allPSRenderers = FindComponentsInRoots<ParticleSystemRenderer>(checkRoots);
        foreach (var psr in allPSRenderers)
        {
            var mesh = psr.mesh;
            if (mesh == null) continue;
            meshInfoList.Add((psr.gameObject, mesh, mesh.triangles.Length / 3, mesh.vertexCount, "ParticleRenderer"));
        }

        // 按三角面从高到低排序
        meshInfoList.Sort((a, b) => b.triangles.CompareTo(a.triangles));

        int totalTriangles = meshInfoList.Sum(x => x.triangles);
        int totalVertices = meshInfoList.Sum(x => x.vertices);

        _meshResults.Add(new CheckResult
        {
            category = "Mesh 统计总览",
            description = $"{(isScoped ? "选定范围" : "场景")}共 {meshInfoList.Count} 个 Mesh，总三角面: {totalTriangles:N0}，总顶点: {totalVertices:N0}",
            msgType = MessageType.Info
        });

        // --- 1.5 统计总览中按对象分组子行 ---
        if (isScoped && checkRoots.Count > 0)
        {
            foreach (var scopeRoot in checkRoots)
            {
                var items = meshInfoList.Where(x => x.go.transform.IsChildOf(scopeRoot.transform)).ToList();
                int groupTris = items.Sum(x => x.triangles);
                int groupVerts = items.Sum(x => x.vertices);
                _meshResults.Add(new CheckResult
                {
                    category = "Mesh 统计总览",
                    description = $"  ─{scopeRoot.name}：{items.Count} 个 Mesh，三角面: {groupTris:N0}，顶点: {groupVerts:N0}",
                    targetObj = scopeRoot,
                    msgType = MessageType.Info
                });
            }
        }

        // --- 2. 高面数模型标记 ---
        int highPolyCount = 0;

        if (isScoped && checkRoots.Count > 1)
        {
            // 分组模式：先添加总汇总行，再按每个 obj 分子 category
            foreach (var info in meshInfoList)
            {
                if (info.triangles > _highPolyThreshold)
                    highPolyCount++;
            }

            if (highPolyCount == 0)
            {
                _meshResults.Add(new CheckResult
                {
category = "高面数模型",
                    description = $"✓ 所有 Mesh 三角面数均在标准 {_highPolyThreshold:N0} 以内",
                    msgType = MessageType.Info
                });
            }
            else
            {
                _meshResults.Add(new CheckResult
                {
category = "高面数模型",
                    description = $"⚠ 发现 {highPolyCount} 个 Mesh 三角面数超过 {_highPolyThreshold:N0}",
                    msgType = MessageType.Warning
                });
            }

            // 按每个 obj 分别归类
            foreach (var scopeRoot in checkRoots)
            {
string subCategory = $"高面数模型 > {scopeRoot.name}";
                var scopeItems = meshInfoList.Where(x => x.go.transform.IsChildOf(scopeRoot.transform) && x.triangles > _highPolyThreshold).ToList();
                if (scopeItems.Count > 0)
                {
                    foreach (var info in scopeItems)
                    {
                        _meshResults.Add(new CheckResult
                        {
                            category = subCategory,
                            description = $"[{info.rendererType}] 三角面: {info.triangles:N0} 顶点: {info.vertices:N0} | Mesh: {info.mesh.name} | 路径: {GetHierarchyPath(info.go)}",
                            targetObj = info.go,
                            msgType = MessageType.Warning
                        });
                    }
                }
                else
                {
                    _meshResults.Add(new CheckResult
                    {
                        category = subCategory,
                        description = $"✓ 无高面数模型",
                        msgType = MessageType.Info
                    });
                }
            }
        }
        else
        {
            // 非分组模式：所有明细放在同一个 category 下
            int highPolyInsertIndex = _meshResults.Count;
            foreach (var info in meshInfoList)
            {
                if (info.triangles > _highPolyThreshold)
                {
                    highPolyCount++;
                    _meshResults.Add(new CheckResult
                    {
category = "高面数模型",
                        description = $"[{info.rendererType}] 三角面: {info.triangles:N0} 顶点: {info.vertices:N0} | Mesh: {info.mesh.name} | 路径: {GetHierarchyPath(info.go)}",
                        targetObj = info.go,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (highPolyCount == 0)
            {
                _meshResults.Add(new CheckResult
                {
category = "高面数模型",
                    description = $"✓ 所有 Mesh 三角面数均在标准 {_highPolyThreshold:N0} 以内",
                    msgType = MessageType.Info
                });
            }
            else
            {
                _meshResults.Insert(highPolyInsertIndex, new CheckResult
                {
category = "高面数模型",
                    description = $"⚠ 发现 {highPolyCount} 个 Mesh 三角面数超过 {_highPolyThreshold:N0}",
                    msgType = MessageType.Warning
                });
            }
        }

        // --- 3. Read/Write 检查（所有 Renderer 类型）---
        int rwEnabledCount = 0;
        var checkedMeshes = new HashSet<UnityEngine.Mesh>(); // 避免重复检查同一个 Mesh

        // 先统计总数
        foreach (var info in meshInfoList)
        {
            if (checkedMeshes.Contains(info.mesh)) continue;
            checkedMeshes.Add(info.mesh);
            if (info.mesh.isReadable) rwEnabledCount++;
        }

        if (isScoped && checkRoots.Count > 1)
        {
            // 分组模式
            if (rwEnabledCount == 0)
            {
                _meshResults.Add(new CheckResult
                {
                    category = "Read/Write Enabled",
                    description = "✓ 所有 Mesh 均未开启不必要的 Read/Write",
                    msgType = MessageType.Info
                });
            }
            else
            {
                _meshResults.Add(new CheckResult
                {
                    category = "Read/Write Enabled",
                    description = $"⚠共 {rwEnabledCount} 个 Mesh 开启了 Read/Write Enabled（双倍内存占用）",
                    msgType = MessageType.Warning
                });
            }

            foreach (var scopeRoot in checkRoots)
            {
                string subCategory = $"Read/Write Enabled > {scopeRoot.name}";
                var scopeChecked = new HashSet<UnityEngine.Mesh>();
                var scopeRwItems = new List<(GameObject go, UnityEngine.Mesh mesh, int triangles, int vertices, string rendererType)>();
                foreach (var info in meshInfoList.Where(x => x.go.transform.IsChildOf(scopeRoot.transform)))
                {
                    if (scopeChecked.Contains(info.mesh)) continue;
                    scopeChecked.Add(info.mesh);
                    if (info.mesh.isReadable) scopeRwItems.Add(info);
                }

                if (scopeRwItems.Count > 0)
                {
                    foreach (var info in scopeRwItems)
                    {
                        _meshResults.Add(new CheckResult
                        {
                            category = subCategory,
                            description = $"[{info.rendererType}] Mesh \"{info.mesh.name}\" 开启了 Read/Write（{info.triangles:N0} 三角面）| 路径: {GetHierarchyPath(info.go)}",
                            targetObj = info.go,
                            msgType = MessageType.Warning
                        });
                    }
                }
                else
                {
                    _meshResults.Add(new CheckResult
                    {
                        category = subCategory,
                        description = "✓ 无 R/W 问题",
                        msgType = MessageType.Info
                    });
                }
            }
        }
        else
        {
            // 非分组模式
            int rwInsertIndex = _meshResults.Count;
            checkedMeshes.Clear();
            foreach (var info in meshInfoList)
            {
                if (checkedMeshes.Contains(info.mesh)) continue;
                checkedMeshes.Add(info.mesh);

                if (info.mesh.isReadable)
                {
                    _meshResults.Add(new CheckResult
                    {
                        category = "Read/Write Enabled",
                        description = $"[{info.rendererType}] Mesh \"{info.mesh.name}\" 开启了 Read/Write（{info.triangles:N0} 三角面）| 路径: {GetHierarchyPath(info.go)}",
                        targetObj = info.go,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (rwEnabledCount == 0)
            {
                _meshResults.Add(new CheckResult
                {
                    category = "Read/Write Enabled",
                    description = "✓ 所有 Mesh 均未开启不必要的 Read/Write",
                    msgType = MessageType.Info
                });
            }
            else
            {
                _meshResults.Insert(rwInsertIndex, new CheckResult
                {
                    category = "Read/Write Enabled",
                    description = $"⚠共 {rwEnabledCount} 个 Mesh 开启了 Read/Write Enabled（双倍内存占用）",
                    msgType = MessageType.Warning
                });
            }
        }

        // --- 4. 重复 Mesh 检测---
        var meshUsageCount = new Dictionary<UnityEngine.Mesh, int>();
        foreach (var info in meshInfoList)
        {
            if (!meshUsageCount.ContainsKey(info.mesh))
                meshUsageCount[info.mesh] = 0;
            meshUsageCount[info.mesh]++;
        }

        int duplicateGroupCount = 0;
        foreach (var kv in meshUsageCount.OrderByDescending(x => x.Value))
        {
            if (kv.Value > 1 && kv.Key.triangles.Length / 3 > _duplicateMeshMinTri) // 只关注面数较高的重复 Mesh
            {
                duplicateGroupCount++;
                _meshResults.Add(new CheckResult
                {
                    category = "重复 Mesh",
                    description = $"Mesh \"{kv.Key.name}\" 被 {kv.Value} 个对象引用（{kv.Key.triangles.Length / 3:N0} 三角面），可考虑合批优化",
                    msgType = MessageType.Info
                });
            }
        }
        if (duplicateGroupCount == 0)
        {
            _meshResults.Add(new CheckResult
            {
                category = "重复 Mesh",
                description = "✓ 未发现高面数的重复 Mesh 引用",
                msgType = MessageType.Info
            });
        }

        // --- 5. MeshCollider 面数检查---
        var allMeshColliders = FindComponentsInRoots<MeshCollider>(checkRoots);
        int colliderWarnings = 0;

        // 先统计总数
        foreach (var mc in allMeshColliders)
        {
            var mesh = mc.sharedMesh;
            if (mesh == null) continue;
            if (mesh.triangles.Length / 3 > _meshColliderThreshold) colliderWarnings++;
        }

        if (isScoped && checkRoots.Count > 1)
        {
            // 分组模式
            if (colliderWarnings == 0)
            {
                _meshResults.Add(new CheckResult
                {
                    category = "MeshCollider 面数",
                    description = allMeshColliders.Count > 0
                        ? $"✓ 所有 MeshCollider 面数均在标准 {_meshColliderThreshold:N0} 以内（共 {allMeshColliders.Count} 个）"
                        : "✓ 场景中无 MeshCollider",
                    msgType = MessageType.Info
                });
            }
            else
            {
                _meshResults.Add(new CheckResult
                {
                    category = "MeshCollider 面数",
                    description = $"⚠ 发现 {colliderWarnings} 个 MeshCollider 面数超过标准 {_meshColliderThreshold:N0}",
                    msgType = MessageType.Warning
                });
            }

            foreach (var scopeRoot in checkRoots)
            {
                string subCategory = $"MeshCollider 面数 > {scopeRoot.name}";
                var scopeMcItems = new List<MeshCollider>();
                foreach (var mc in allMeshColliders)
                {
                    if (!mc.gameObject.transform.IsChildOf(scopeRoot.transform)) continue;
                    var mesh = mc.sharedMesh;
                    if (mesh == null) continue;
                    if (mesh.triangles.Length / 3 > _meshColliderThreshold)
                        scopeMcItems.Add(mc);
                }

                if (scopeMcItems.Count > 0)
                {
                    foreach (var mc in scopeMcItems)
                    {
                        int tris = mc.sharedMesh.triangles.Length / 3;
                        _meshResults.Add(new CheckResult
                        {
                            category = subCategory,
                            description = $"MeshCollider 三角面: {tris:N0}（标准 {_meshColliderThreshold:N0}）Mesh: {mc.sharedMesh.name} | 路径: {GetHierarchyPath(mc.gameObject)}",
                            targetObj = mc.gameObject,
                            msgType = MessageType.Warning
                        });
                    }
                }
                else
                {
                    _meshResults.Add(new CheckResult
                    {
                        category = subCategory,
description = "✓ 无问题",
                        msgType = MessageType.Info
                    });
                }
            }
        }
        else
        {
            // 非分组模式
            foreach (var mc in allMeshColliders)
            {
                var mesh = mc.sharedMesh;
                if (mesh == null) continue;
                int tris = mesh.triangles.Length / 3;
                if (tris > _meshColliderThreshold)
                {
                    _meshResults.Add(new CheckResult
                    {
                        category = "MeshCollider 面数",
                        description = $"MeshCollider 三角面: {tris:N0}（标准 {_meshColliderThreshold:N0}）Mesh: {mesh.name} | 路径: {GetHierarchyPath(mc.gameObject)}",
                        targetObj = mc.gameObject,
                        msgType = MessageType.Warning
                    });
                }
            }
            if (colliderWarnings == 0)
            {
                _meshResults.Add(new CheckResult
                {
                    category = "MeshCollider 面数",
                    description = allMeshColliders.Count > 0
                        ? $"✓ 所有 MeshCollider 面数均在标准 {_meshColliderThreshold:N0} 以内（共 {allMeshColliders.Count} 个）"
                        : "✓ 场景中无 MeshCollider",
                    msgType = MessageType.Info
                });
            }
        }

        Debug.Log($"[场景优化工具] 模型面数检查完成，共 {_meshResults.Count} 条结果，检查范围 {(isScoped ? string.Join(", ", checkRoots.Select(r => r.name)) : "全场景")}");
        Repaint();
    }

    // ==================================================================================
    //  通用绘制方法
    // ==================================================================================

    /// <summary>
    /// 获取当前 Tab 对应的检查结果列表
    /// </summary>
    private List<CheckResult> GetCurrentResults()
    {
        switch (_currentTab)
        {
            case TabType.VFX: return _vfxResults;
            case TabType.Material: return _materialResults;
            case TabType.Mesh: return _meshResults;
            default: return _vfxResults;
        }
    }

    /// <summary>
    /// 绘制检查结果列表，按 category 分组折叠，支持搜索过滤和仅警告过滤
    /// </summary>
    private void DrawResults(List<CheckResult> results)
    {
        if (results.Count == 0)
        {
            EditorGUILayout.Space(30);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
EditorGUILayout.LabelField("点击顶部 ▶ 按钮开始检查", _headerStyle, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return;
        }

        // 应用过滤
        bool hasSearch = !string.IsNullOrEmpty(_searchFilter);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        string lastCategory = null;
        bool isFolded = false;
        // 父 category 折叠状态追踪（用于子category 判断）
        string currentParentCategory = null;
        bool isParentFolded = false;

        // 先计算每个分类的警告数和总数
        _categoryWarningCount.Clear();
        _categoryTotalCount.Clear();
        foreach (var r in results)
        {
            if (!_categoryTotalCount.ContainsKey(r.category))
            {
                _categoryTotalCount[r.category] = 0;
                _categoryWarningCount[r.category] = 0;
            }
            _categoryTotalCount[r.category]++;
            if (r.msgType == MessageType.Warning || r.msgType == MessageType.Error)
                _categoryWarningCount[r.category]++;
        }

        foreach (var result in results)
        {
            // 搜索过滤
            if (hasSearch && result.description.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0
                && result.category.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            // 仅警告过滤
            if (_onlyShowWarnings && result.msgType != MessageType.Warning && result.msgType != MessageType.Error)
            {
                // 但保留每个分类的第一个汇总条目
                if (result.category == lastCategory) continue;
            }

            // 解析层级 category：判断是否含 " > " 分隔符
            bool isSubCategory = result.category.Contains(" > ");
            string parentCat = isSubCategory ? result.category.Substring(0, result.category.IndexOf(" > ")) : null;
            string subLabel = isSubCategory ? result.category.Substring(result.category.IndexOf(" > ") + 3) : null;

            // 子category：如果父 category 被折叠，跳过
            if (isSubCategory)
            {
                // 更新父折叠状态
                if (parentCat != currentParentCategory)
                {
                    currentParentCategory = parentCat;
                    isParentFolded = _foldoutStates.ContainsKey(parentCat) && !_foldoutStates[parentCat];
                }
                if (isParentFolded) continue;
            }

            // 按 category 分组
            if (result.category != lastCategory)
            {
                lastCategory = result.category;

                // 分类标题带计数
                int warnCnt = _categoryWarningCount.ContainsKey(result.category) ? _categoryWarningCount[result.category] : 0;
                int totalCnt = _categoryTotalCount.ContainsKey(result.category) ? _categoryTotalCount[result.category] : 0;

                // 首次遇到的 category：有警告默认展开，全部通过默认折叠
                if (!_foldoutStates.ContainsKey(result.category))
                {
                    if (isSubCategory)
                    {
                        // 子category：自身有警告就展开
                        _foldoutStates[result.category] = warnCnt > 0;
                    }
                    else
                    {
                        // 一级 category：汇总自身+ 所有子 category 的警告数
                        int totalWarn = warnCnt;
                        string parentKey = result.category + " > ";
                        foreach (var kv in _categoryWarningCount)
                        {
                            if (kv.Key.StartsWith(parentKey))
                                totalWarn += kv.Value;
                        }
                        _foldoutStates[result.category] = totalWarn > 0;
                    }
                }
                string foldLabel;

                if (isSubCategory)
                {
                    // 子 category：显示名用 subLabel，带缩进
                    if (warnCnt > 0)
                        foldLabel = $"{subLabel}  (⚠{warnCnt}/{totalCnt})";
                    else
                        foldLabel = $"{subLabel}  (✓ {totalCnt})";

                    // 子 category tooltip：继承父分类的 tooltip
                    string subTooltip = "";
                    if (parentCat != null && _categoryTooltips.TryGetValue(parentCat, out var parentTip))
                        subTooltip = $"[{parentCat} > {subLabel}]\n{parentTip}";

                    EditorGUILayout.Space(1);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(20); // 二级缩进

                        var prevColor = GUI.contentColor;
                        if (warnCnt > 0)
                            GUI.contentColor = new Color(1f, 0.8f, 0.45f);
                        else
                            GUI.contentColor = new Color(0.55f, 0.85f, 0.55f);

                        var subContent = new GUIContent(foldLabel, subTooltip);
                        _foldoutStates[result.category] = EditorGUILayout.Foldout(
                            _foldoutStates[result.category], subContent, true, EditorStyles.foldout);
                        GUI.contentColor = prevColor;
                    }
                }
                else
                {
                    // 一级 category：包括子 category 的计数汇总
                    // 汇总所有属于此父 category 的子 category 的数据
                    int parentWarnCnt = warnCnt;
                    int parentTotalCnt = totalCnt;
                    string parentKey = result.category + " > ";
                    foreach (var kv in _categoryTotalCount)
                    {
                        if (kv.Key.StartsWith(parentKey))
                        {
                            parentTotalCnt += kv.Value;
                            if (_categoryWarningCount.ContainsKey(kv.Key))
                                parentWarnCnt += _categoryWarningCount[kv.Key];
                        }
                    }

                    if (parentWarnCnt > 0)
                        foldLabel = $"{result.category}  (⚠{parentWarnCnt}/{parentTotalCnt})";
                    else
                        foldLabel = $"{result.category}  (✓ {parentTotalCnt})";

                    EditorGUILayout.Space(3);

                    // 更新父折叠追踪
                    currentParentCategory = result.category;

                    // 获取分类 Tooltip
                    string catTooltip = "";
                    _categoryTooltips.TryGetValue(result.category, out catTooltip);
                    if (catTooltip == null) catTooltip = "";

                    bool showFixBtn = _currentTab == TabType.VFX && parentWarnCnt > 0 && CanFixCategory(result.category);

                    if (showFixBtn)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var prevColor = GUI.contentColor;
                            if (parentWarnCnt > 0)
                                GUI.contentColor = new Color(1f, 0.75f, 0.3f);
                            else
                                GUI.contentColor = new Color(0.5f, 0.9f, 0.5f);

                            var headerContent = new GUIContent(foldLabel, catTooltip);
                            _foldoutStates[result.category] = EditorGUILayout.Foldout(
                                _foldoutStates[result.category], headerContent, true, EditorStyles.foldout);
                            GUI.contentColor = prevColor;

                            var fixBtnColor = GUI.backgroundColor;
                            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.5f);
                            if (GUILayout.Button(new GUIContent($"修复({parentWarnCnt})", $"将此分类所有问题修复为标准值\n支持 Ctrl+Z 撤销"),
                                EditorStyles.miniButton, GUILayout.Width(60)))
                            {
                                FixVFXCategory(result.category);
                            }
                            GUI.backgroundColor = fixBtnColor;
                        }
                    }
                    else
                    {
                        var prevColor = GUI.contentColor;
                        if (parentWarnCnt > 0)
                            GUI.contentColor = new Color(1f, 0.75f, 0.3f);
                        else
                            GUI.contentColor = new Color(0.5f, 0.9f, 0.5f);

                        var headerContent = new GUIContent(foldLabel, catTooltip);
                        _foldoutStates[result.category] = EditorGUILayout.Foldout(
                            _foldoutStates[result.category], headerContent, true, EditorStyles.foldoutHeader);
                        GUI.contentColor = prevColor;
                    }

                    isParentFolded = !_foldoutStates[result.category];
                }

                isFolded = !_foldoutStates[result.category];
            }

            if (isFolded) continue;

            // 计算行缩进：子category 的内容多缩进一些
            float rowIndent = isSubCategory ? 24f : 6f;

            // 行样式 - 警告行有背景色
            bool isWarning = result.msgType == MessageType.Warning || result.msgType == MessageType.Error;

            if (isWarning)
            {
                var bgColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.92f, 0.75f);
                using (new EditorGUILayout.HorizontalScope(_warningRowStyle))
                {
                    GUILayout.Space(rowIndent);
                    DrawResultRow(result);
                }
                GUI.backgroundColor = bgColor;
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(rowIndent);
                    DrawResultRow(result);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 绘制单行结果
    /// </summary>
    private void DrawResultRow(CheckResult result)
    {
        // 先计算 tooltip（图标和描述共用）
        string rowTooltip = result.tooltip;
        if (string.IsNullOrEmpty(rowTooltip))
        {
            string baseCat = result.category.Contains(" > ")
                ? result.category.Substring(0, result.category.IndexOf(" > "))
                : result.category;
            _categoryTooltips.TryGetValue(baseCat, out rowTooltip);
            if (rowTooltip == null) rowTooltip = "";
        }

        // 图标（悬停也显示 tooltip）
        if (result.msgType == MessageType.Warning)
EditorGUILayout.LabelField(new GUIContent("⚠", rowTooltip), GUILayout.Width(18));
        else if (result.msgType == MessageType.Error)
EditorGUILayout.LabelField(new GUIContent("❌", rowTooltip), GUILayout.Width(18));
else if (result.description.StartsWith("✓"))
            EditorGUILayout.LabelField(new GUIContent(" ", rowTooltip), GUILayout.Width(18));
        else
            EditorGUILayout.LabelField(new GUIContent("  ", rowTooltip), GUILayout.Width(18));

        // 描述（可点击的区域，点击也能定位，带悬停 Tooltip）
        var descContent = new GUIContent(result.description, rowTooltip);
        var descRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * Mathf.CeilToInt(result.description.Length / 60f + 0.5f));
        GUI.Label(descRect, descContent, EditorStyles.wordWrappedLabel);

        // 点击描述区域也能定位
        if (result.targetObj != null && Event.current.type == EventType.MouseDown && descRect.Contains(Event.current.mousePosition))
        {
            Selection.activeGameObject = result.targetObj;
            EditorGUIUtility.PingObject(result.targetObj);
            Event.current.Use();
        }

        // 定位按钮
        if (result.targetObj != null)
        {
            if (GUILayout.Button("定位", GUILayout.Width(40), GUILayout.Height(18)))
            {
                Selection.activeGameObject = result.targetObj;
                EditorGUIUtility.PingObject(result.targetObj);
            }
        }
    }

    // ==================================================================================
    //  工具方法
    // ==================================================================================

    /// <summary>
    /// 在当前场景中查找所有指定类型的组件（包括未激活的）
    /// </summary>
    private List<T> FindAllComponentsInScene<T>() where T : Component
    {
        var result = new List<T>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }
        }
        return result;
    }

    /// <summary>
    /// 计算指定 GameObject 及其所有子节点的总三角面数
    /// </summary>
    private int CalcTriangleCount(GameObject go)
    {
        int total = 0;
        // MeshFilter
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh != null)
                total += mf.sharedMesh.triangles.Length / 3;
        }
        // SkinnedMeshRenderer
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh != null)
                total += smr.sharedMesh.triangles.Length / 3;
        }
        // ParticleSystemRenderer
        foreach (var psr in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (psr.mesh != null)
                total += psr.mesh.triangles.Length / 3;
        }
        return total;
    }

    /// <summary>
    /// 获取 GameObject 的完整层级路径
    /// </summary>
    private string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        var t = go.transform;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    /// <summary>
    /// 获取 MinMaxCurve 的最大可能值（兼容所有模式）
    /// </summary>
    private float GetMinMaxCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return curve.constantMax;
            case ParticleSystemCurveMode.Curve:
                return curve.curveMultiplier * GetAnimationCurveMax(curve.curve);
            case ParticleSystemCurveMode.TwoCurves:
                return curve.curveMultiplier * GetAnimationCurveMax(curve.curveMax);
            default:
                return curve.constantMax;
        }
    }

    private float GetAnimationCurveMax(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0) return 0f;
        float max = float.MinValue;
        for (int i = 0; i < curve.length; i++)
        {
            if (curve[i].value > max)
                max = curve[i].value;
        }
        return max;
    }

    /// <summary>
    /// 将 MinMaxCurve 的值 Clamp 到指定上限（保留模式）
    /// Constant/TwoConstants: 直接 clamp 数值
    /// Curve/TwoCurves: 缩放 curveMultiplier 使最大值不超标
    /// </summary>
    private ParticleSystem.MinMaxCurve ClampMinMaxCurve(ParticleSystem.MinMaxCurve curve, float maxValue)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return new ParticleSystem.MinMaxCurve(Mathf.Min(curve.constant, maxValue));
            case ParticleSystemCurveMode.TwoConstants:
            {
                float ratio = curve.constantMax > 0 ? maxValue / curve.constantMax : 1f;
                if (ratio >= 1f) return curve;
                return new ParticleSystem.MinMaxCurve(curve.constantMin * ratio, curve.constantMax * ratio);
            }
            case ParticleSystemCurveMode.Curve:
            {
                float curveMax = GetAnimationCurveMax(curve.curve);
                float actualMax = curve.curveMultiplier * curveMax;
                if (actualMax <= maxValue) return curve;
                curve.curveMultiplier = curveMax > 0 ? maxValue / curveMax : 0f;
                return curve;
            }
            case ParticleSystemCurveMode.TwoCurves:
            {
                float curveMax = GetAnimationCurveMax(curve.curveMax);
                float actualMax = curve.curveMultiplier * curveMax;
                if (actualMax <= maxValue) return curve;
                curve.curveMultiplier = curveMax > 0 ? maxValue / curveMax : 0f;
                return curve;
            }
            default:
                return new ParticleSystem.MinMaxCurve(maxValue);
        }
    }
}
