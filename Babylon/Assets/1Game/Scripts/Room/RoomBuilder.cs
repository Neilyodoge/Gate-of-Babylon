using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 房间构建器 —— 用基础几何体（Cube/Plane）动态生成房间
    /// 包含地面、墙壁、柱子、地面装饰线等
    /// </summary>
    public static class RoomBuilder
    {
        // 墙壁高度和厚度
        private const float WALL_HEIGHT = 4f;
        private const float WALL_THICKNESS = 1f;

        // 颜色方案（仙侠风格：深色调 + 紫/青点缀）
        private static readonly Color GROUND_COLOR = new(0.12f, 0.14f, 0.18f);
        private static readonly Color GROUND_LINE_COLOR = new(0.18f, 0.22f, 0.28f, 0.5f);
        private static readonly Color WALL_COLOR = new(0.22f, 0.18f, 0.28f);
        private static readonly Color WALL_TOP_COLOR = new(0.35f, 0.25f, 0.45f);
        private static readonly Color PILLAR_COLOR = new(0.28f, 0.22f, 0.35f);
        private static readonly Color PILLAR_TOP_COLOR = new(0.5f, 0.35f, 0.6f);
        private static readonly Color CORNER_GLOW_COLOR = new(0.3f, 0.6f, 0.9f);

        /// <summary>
        /// 构建房间
        /// </summary>
        /// <param name="parent">房间根节点</param>
        /// <param name="width">房间宽度（X轴）</param>
        /// <param name="depth">房间深度（Z轴）</param>
        /// <param name="roomIndex">房间层数（影响装饰风格）</param>
        /// <returns>房间根 GameObject</returns>
        public static GameObject Build(Transform parent, float width, float depth, int roomIndex)
        {
            var roomRoot = new GameObject("RoomVisuals");
            roomRoot.transform.SetParent(parent, false);
            roomRoot.transform.localPosition = Vector3.zero;

            // 1. 地面
            BuildGround(roomRoot.transform, width, depth);

            // 2. 地面网格线（参考线）
            BuildGroundGrid(roomRoot.transform, width, depth);

            // 3. 四面墙壁
            BuildWalls(roomRoot.transform, width, depth);

            // 4. 四角装饰柱
            BuildCornerPillars(roomRoot.transform, width, depth);

            // 5. 随机障碍物（柱子）—— 层数越高障碍越多
            int obstacleCount = Mathf.Min(roomIndex + 1, 6);
            BuildObstacles(roomRoot.transform, width, depth, obstacleCount, roomIndex);

            // 6. 角落发光标记
            BuildCornerGlows(roomRoot.transform, width, depth);

            return roomRoot;
        }

        /// <summary>构建地面</summary>
        private static void BuildGround(Transform parent, float width, float depth)
        {
            // Unity Plane 默认 10x10 单位，所以 scale 需要除以 10
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);

            // 地面不需要 MeshCollider 以外的碰撞（Plane 自带）
            SetMaterial(ground, GROUND_COLOR);

            // 地面边缘装饰条（稍微亮一点的边框）
            float borderWidth = 0.3f;
            // 前
            CreateDecorStrip(parent, "BorderFront",
                new Vector3(0, 0.01f, depth / 2f - borderWidth / 2f),
                new Vector3(width, 0.02f, borderWidth),
                new Color(0.25f, 0.2f, 0.35f, 0.6f));
            // 后
            CreateDecorStrip(parent, "BorderBack",
                new Vector3(0, 0.01f, -depth / 2f + borderWidth / 2f),
                new Vector3(width, 0.02f, borderWidth),
                new Color(0.25f, 0.2f, 0.35f, 0.6f));
            // 左
            CreateDecorStrip(parent, "BorderLeft",
                new Vector3(-width / 2f + borderWidth / 2f, 0.01f, 0),
                new Vector3(borderWidth, 0.02f, depth),
                new Color(0.25f, 0.2f, 0.35f, 0.6f));
            // 右
            CreateDecorStrip(parent, "BorderRight",
                new Vector3(width / 2f - borderWidth / 2f, 0.01f, 0),
                new Vector3(borderWidth, 0.02f, depth),
                new Color(0.25f, 0.2f, 0.35f, 0.6f));
        }

        /// <summary>构建地面网格线</summary>
        private static void BuildGroundGrid(Transform parent, float width, float depth)
        {
            float lineThickness = 0.05f;
            float lineHeight = 0.02f;
            float spacing = 5f; // 每5单位一条线

            var gridRoot = new GameObject("GridLines");
            gridRoot.transform.SetParent(parent, false);

            // X方向的线（沿Z轴排列）
            for (float z = -depth / 2f + spacing; z < depth / 2f; z += spacing)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "GridLineX";
                line.transform.SetParent(gridRoot.transform, false);
                line.transform.localPosition = new Vector3(0, lineHeight, z);
                line.transform.localScale = new Vector3(width - 2f, lineHeight, lineThickness);
                SetMaterial(line, GROUND_LINE_COLOR);
                // 移除碰撞体，网格线不参与物理
                Object.Destroy(line.GetComponent<Collider>());
            }

            // Z方向的线（沿X轴排列）
            for (float x = -width / 2f + spacing; x < width / 2f; x += spacing)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "GridLineZ";
                line.transform.SetParent(gridRoot.transform, false);
                line.transform.localPosition = new Vector3(x, lineHeight, 0);
                line.transform.localScale = new Vector3(lineThickness, lineHeight, depth - 2f);
                SetMaterial(line, GROUND_LINE_COLOR);
                Object.Destroy(line.GetComponent<Collider>());
            }
        }

        /// <summary>构建四面墙壁</summary>
        private static void BuildWalls(Transform parent, float width, float depth)
        {
            float halfW = width / 2f;
            float halfD = depth / 2f;
            float halfH = WALL_HEIGHT / 2f;

            // 前墙（+Z）
            CreateWall(parent, "WallFront",
                new Vector3(0, halfH, halfD + WALL_THICKNESS / 2f),
                new Vector3(width + WALL_THICKNESS * 2, WALL_HEIGHT, WALL_THICKNESS));

            // 后墙（-Z）
            CreateWall(parent, "WallBack",
                new Vector3(0, halfH, -halfD - WALL_THICKNESS / 2f),
                new Vector3(width + WALL_THICKNESS * 2, WALL_HEIGHT, WALL_THICKNESS));

            // 右墙（+X）
            CreateWall(parent, "WallRight",
                new Vector3(halfW + WALL_THICKNESS / 2f, halfH, 0),
                new Vector3(WALL_THICKNESS, WALL_HEIGHT, depth + WALL_THICKNESS * 2));

            // 左墙（-X）
            CreateWall(parent, "WallLeft",
                new Vector3(-halfW - WALL_THICKNESS / 2f, halfH, 0),
                new Vector3(WALL_THICKNESS, WALL_HEIGHT, depth + WALL_THICKNESS * 2));

            // 墙顶装饰条（亮色）
            float topH = 0.3f;
            CreateDecorStrip(parent, "WallTopFront",
                new Vector3(0, WALL_HEIGHT + topH / 2f, halfD + WALL_THICKNESS / 2f),
                new Vector3(width + WALL_THICKNESS * 2 + 0.2f, topH, WALL_THICKNESS + 0.2f),
                WALL_TOP_COLOR);
            CreateDecorStrip(parent, "WallTopBack",
                new Vector3(0, WALL_HEIGHT + topH / 2f, -halfD - WALL_THICKNESS / 2f),
                new Vector3(width + WALL_THICKNESS * 2 + 0.2f, topH, WALL_THICKNESS + 0.2f),
                WALL_TOP_COLOR);
            CreateDecorStrip(parent, "WallTopRight",
                new Vector3(halfW + WALL_THICKNESS / 2f, WALL_HEIGHT + topH / 2f, 0),
                new Vector3(WALL_THICKNESS + 0.2f, topH, depth + WALL_THICKNESS * 2 + 0.2f),
                WALL_TOP_COLOR);
            CreateDecorStrip(parent, "WallTopLeft",
                new Vector3(-halfW - WALL_THICKNESS / 2f, WALL_HEIGHT + topH / 2f, 0),
                new Vector3(WALL_THICKNESS + 0.2f, topH, depth + WALL_THICKNESS * 2 + 0.2f),
                WALL_TOP_COLOR);
        }

        /// <summary>构建四角装饰柱</summary>
        private static void BuildCornerPillars(Transform parent, float width, float depth)
        {
            float halfW = width / 2f;
            float halfD = depth / 2f;
            float pillarSize = 1.5f;
            float pillarHeight = WALL_HEIGHT + 1f;

            Vector3[] corners = {
                new(halfW, 0, halfD),
                new(-halfW, 0, halfD),
                new(halfW, 0, -halfD),
                new(-halfW, 0, -halfD)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                // 柱体
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = $"CornerPillar_{i}";
                pillar.transform.SetParent(parent, false);
                pillar.transform.localPosition = corners[i] + new Vector3(0, pillarHeight / 2f, 0);
                pillar.transform.localScale = new Vector3(pillarSize, pillarHeight, pillarSize);
                SetMaterial(pillar, PILLAR_COLOR);

                // 柱顶装饰
                var pillarTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillarTop.name = $"PillarTop_{i}";
                pillarTop.transform.SetParent(parent, false);
                pillarTop.transform.localPosition = corners[i] + new Vector3(0, pillarHeight + 0.2f, 0);
                pillarTop.transform.localScale = new Vector3(pillarSize + 0.4f, 0.4f, pillarSize + 0.4f);
                SetMaterial(pillarTop, PILLAR_TOP_COLOR);
                Object.Destroy(pillarTop.GetComponent<Collider>()); // 装饰不需要碰撞
            }
        }

        /// <summary>构建随机障碍物（柱子）</summary>
        private static void BuildObstacles(Transform parent, float width, float depth, int count, int roomIndex)
        {
            float halfW = width / 2f - 3f; // 留出墙边距
            float halfD = depth / 2f - 3f;
            float safeRadius = 4f; // 中心安全区（玩家出生点附近不放障碍）

            // 根据层数选择不同的障碍物颜色
            Color obstacleColor = Color.Lerp(
                new Color(0.25f, 0.2f, 0.3f),
                new Color(0.35f, 0.15f, 0.2f),
                (float)roomIndex / 5f);

            var obstacleRoot = new GameObject("Obstacles");
            obstacleRoot.transform.SetParent(parent, false);

            for (int i = 0; i < count; i++)
            {
                // 随机位置（避开中心安全区）
                Vector3 pos;
                int attempts = 0;
                do
                {
                    pos = new Vector3(
                        Random.Range(-halfW, halfW),
                        0,
                        Random.Range(-halfD, halfD));
                    attempts++;
                } while (pos.magnitude < safeRadius && attempts < 20);

                if (attempts >= 20) continue;

                // 随机选择障碍物类型
                float roll = Random.value;
                if (roll < 0.5f)
                {
                    // 方柱
                    BuildSquarePillar(obstacleRoot.transform, pos, obstacleColor, i);
                }
                else if (roll < 0.8f)
                {
                    // 矮墙/石台
                    BuildLowWall(obstacleRoot.transform, pos, obstacleColor, i);
                }
                else
                {
                    // 圆柱（用多个Cube近似）
                    BuildRoundPillar(obstacleRoot.transform, pos, obstacleColor, i);
                }
            }
        }

        /// <summary>方形柱子障碍</summary>
        private static void BuildSquarePillar(Transform parent, Vector3 pos, Color color, int index)
        {
            float size = Random.Range(1.2f, 2f);
            float height = Random.Range(2.5f, 4f);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = $"Obstacle_Pillar_{index}";
            pillar.transform.SetParent(parent, false);
            pillar.transform.localPosition = pos + new Vector3(0, height / 2f, 0);
            pillar.transform.localScale = new Vector3(size, height, size);
            // 随机旋转45度
            pillar.transform.localRotation = Quaternion.Euler(0, Random.value > 0.5f ? 45f : 0f, 0);
            SetMaterial(pillar, color);

            // 柱顶
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = $"Obstacle_PillarTop_{index}";
            top.transform.SetParent(parent, false);
            top.transform.localPosition = pos + new Vector3(0, height + 0.15f, 0);
            top.transform.localScale = new Vector3(size + 0.3f, 0.3f, size + 0.3f);
            top.transform.localRotation = pillar.transform.localRotation;
            SetMaterial(top, color * 1.3f);
            Object.Destroy(top.GetComponent<Collider>());
        }

        /// <summary>矮墙/石台障碍</summary>
        private static void BuildLowWall(Transform parent, Vector3 pos, Color color, int index)
        {
            float length = Random.Range(3f, 5f);
            float height = Random.Range(1f, 2f);
            float thickness = Random.Range(0.8f, 1.2f);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Obstacle_Wall_{index}";
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = pos + new Vector3(0, height / 2f, 0);
            wall.transform.localScale = new Vector3(length, height, thickness);
            // 随机朝向
            wall.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 180f), 0);
            SetMaterial(wall, color * 0.9f);
        }

        /// <summary>圆柱障碍（用旋转的Cube近似八角柱）</summary>
        private static void BuildRoundPillar(Transform parent, Vector3 pos, Color color, int index)
        {
            float radius = Random.Range(0.8f, 1.3f);
            float height = Random.Range(3f, 5f);

            // 用两个交叉的Cube近似圆柱
            for (int j = 0; j < 2; j++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Obstacle_Round_{index}_{j}";
                cube.transform.SetParent(parent, false);
                cube.transform.localPosition = pos + new Vector3(0, height / 2f, 0);
                cube.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
                cube.transform.localRotation = Quaternion.Euler(0, j * 45f, 0);
                SetMaterial(cube, color * 1.1f);

                // 只保留第一个的碰撞体
                if (j > 0)
                    Object.Destroy(cube.GetComponent<Collider>());
            }
        }

        /// <summary>角落发光标记（小方块，模拟灯光效果）</summary>
        private static void BuildCornerGlows(Transform parent, float width, float depth)
        {
            float halfW = width / 2f - 0.5f;
            float halfD = depth / 2f - 0.5f;

            Vector3[] corners = {
                new(halfW, 0.1f, halfD),
                new(-halfW, 0.1f, halfD),
                new(halfW, 0.1f, -halfD),
                new(-halfW, 0.1f, -halfD)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glow.name = $"CornerGlow_{i}";
                glow.transform.SetParent(parent, false);
                glow.transform.localPosition = corners[i];
                glow.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
                Object.Destroy(glow.GetComponent<Collider>());

                var renderer = glow.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = CORNER_GLOW_COLOR;
                    // 自发光
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", CORNER_GLOW_COLOR * 2f);
                    renderer.material = mat;
                }
            }
        }

        // ========== 工具方法 ==========

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            SetMaterial(wall, WALL_COLOR);
        }

        private static void CreateDecorStrip(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent, false);
            strip.transform.localPosition = pos;
            strip.transform.localScale = scale;
            SetMaterial(strip, color);
            Object.Destroy(strip.GetComponent<Collider>()); // 装饰不需要碰撞
        }

        private static void SetMaterial(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                renderer.material = mat;
            }
        }

        /// <summary>构建房间内陷阱</summary>
        public static void BuildTraps(Transform parent, float width, float depth, int count)
        {
            if (count <= 0) return;

            float halfW = width / 2f - 4f;
            float halfD = depth / 2f - 4f;
            float safeRadius = 5f;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos;
                int attempts = 0;
                do
                {
                    pos = new Vector3(Random.Range(-halfW, halfW), 0, Random.Range(-halfD, halfD));
                    attempts++;
                } while (pos.magnitude < safeRadius && attempts < 20);

                if (attempts >= 20) continue;

                float roll = Random.value;
                if (roll < 0.5f)
                    BuildSpikeTrap(parent, pos, i);
                else
                    BuildFireTrap(parent, pos, i);
            }
        }

        /// <summary>地刺陷阱：周期性弹出尖刺</summary>
        private static void BuildSpikeTrap(Transform parent, Vector3 pos, int index)
        {
            var trapGo = new GameObject($"SpikeTrap_{index}");
            trapGo.transform.SetParent(parent, false);
            trapGo.transform.localPosition = pos;

            // 地面标记（红色方块）
            var base_ = GameObject.CreatePrimitive(PrimitiveType.Cube);
            base_.name = "SpikeBase";
            base_.transform.SetParent(trapGo.transform, false);
            base_.transform.localPosition = new Vector3(0, 0.02f, 0);
            base_.transform.localScale = new Vector3(2f, 0.04f, 2f);
            var baseCol = base_.GetComponent<Collider>();
            if (baseCol != null) Object.Destroy(baseCol);
            SetMaterial(base_, new Color(0.4f, 0.15f, 0.1f));

            // 尖刺（初始隐藏）
            var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spike.name = "Spike";
            spike.transform.SetParent(trapGo.transform, false);
            spike.transform.localPosition = new Vector3(0, -0.5f, 0);
            spike.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            spike.transform.localRotation = Quaternion.Euler(0, 45, 0);
            var spikeCol = spike.GetComponent<Collider>();
            if (spikeCol != null) Object.Destroy(spikeCol);
            SetMaterial(spike, new Color(0.6f, 0.2f, 0.15f));

            // 触发器
            var triggerGo = new GameObject("SpikeTrigger");
            triggerGo.transform.SetParent(trapGo.transform, false);
            triggerGo.transform.localPosition = new Vector3(0, 0.5f, 0);
            var sc = triggerGo.AddComponent<BoxCollider>();
            sc.isTrigger = true;
            sc.size = new Vector3(2f, 1.5f, 2f);
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var trap = trapGo.AddComponent<SpikeTrapBehaviour>();
            trap.Initialize(spike.transform, triggerGo);
        }

        /// <summary>火焰陷阱：持续喷火的区域</summary>
        private static void BuildFireTrap(Transform parent, Vector3 pos, int index)
        {
            var trapGo = new GameObject($"FireTrap_{index}");
            trapGo.transform.SetParent(parent, false);
            trapGo.transform.localPosition = pos;

            // 火焰底座
            var base_ = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            base_.name = "FireBase";
            base_.transform.SetParent(trapGo.transform, false);
            base_.transform.localPosition = new Vector3(0, 0.1f, 0);
            base_.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);
            var baseCol = base_.GetComponent<Collider>();
            if (baseCol != null) Object.Destroy(baseCol);

            var baseRend = base_.GetComponent<Renderer>();
            if (baseRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.3f, 0.15f, 0.05f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.5f, 0.2f, 0.05f));
                baseRend.material = mat;
            }

            // 火焰视觉（半透明柱体）
            var flame = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            flame.name = "Flame";
            flame.transform.SetParent(trapGo.transform, false);
            flame.transform.localPosition = new Vector3(0, 0.8f, 0);
            flame.transform.localScale = new Vector3(1f, 0.8f, 1f);
            var flameCol = flame.GetComponent<Collider>();
            if (flameCol != null) Object.Destroy(flameCol);

            var flameRend = flame.GetComponent<Renderer>();
            if (flameRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetFloat("_Surface", 1);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.color = new Color(1f, 0.4f, 0.1f, 0.4f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.05f) * 3f);
                flameRend.material = mat;
            }

            // 触发器
            var triggerGo = new GameObject("FireTrigger");
            triggerGo.transform.SetParent(trapGo.transform, false);
            triggerGo.transform.localPosition = new Vector3(0, 0.5f, 0);
            var sc = triggerGo.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.2f;
            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var trap = trapGo.AddComponent<FireTrapBehaviour>();
            trap.Initialize(flame.transform);
        }

        /// <summary>
        /// 销毁房间视觉（切换房间时调用）
        /// </summary>
        public static void DestroyRoom(GameObject roomVisuals)
        {
            if (roomVisuals != null)
                Object.Destroy(roomVisuals);
        }
    }

    /// <summary>地刺陷阱行为</summary>
    public class SpikeTrapBehaviour : MonoBehaviour
    {
        private Transform _spike;
        private float _timer;
        private float _interval = 3f;
        private float _activeDuration = 1f;
        private bool _active;
        private float _activeTimer;
        private float _damage = 10f;
        private float _damageCooldown;

        public void Initialize(Transform spike, GameObject trigger)
        {
            _spike = spike;
            _timer = Random.Range(0f, _interval); // 随机初始延迟
        }

        private void Update()
        {
            if (_damageCooldown > 0) _damageCooldown -= Time.deltaTime;

            if (_active)
            {
                _activeTimer -= Time.deltaTime;
                // 尖刺弹出
                if (_spike != null)
                {
                    float targetY = 0.5f;
                    _spike.localPosition = Vector3.Lerp(_spike.localPosition,
                        new Vector3(0, targetY, 0), Time.deltaTime * 15f);
                }
                if (_activeTimer <= 0)
                {
                    _active = false;
                    _timer = _interval;
                }
            }
            else
            {
                _timer -= Time.deltaTime;
                // 尖刺缩回
                if (_spike != null)
                {
                    _spike.localPosition = Vector3.Lerp(_spike.localPosition,
                        new Vector3(0, -0.5f, 0), Time.deltaTime * 10f);
                }
                if (_timer <= 0)
                {
                    _active = true;
                    _activeTimer = _activeDuration;
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!_active || _damageCooldown > 0) return;
            if (other.CompareTag("Player"))
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.OnDamage(_damage, transform.position, gameObject);
                    _damageCooldown = 0.5f;
                }
            }
        }
    }

    /// <summary>火焰陷阱行为</summary>
    public class FireTrapBehaviour : MonoBehaviour
    {
        private Transform _flame;
        private float _damage = 5f;
        private float _damageCooldown;
        private float _timer;

        public void Initialize(Transform flame)
        {
            _flame = flame;
        }

        private void Update()
        {
            if (_damageCooldown > 0) _damageCooldown -= Time.deltaTime;

            // 火焰闪烁动画
            if (_flame != null)
            {
                float scaleY = 0.8f + Mathf.Sin(Time.time * 5f) * 0.2f;
                _flame.localScale = new Vector3(1f, scaleY, 1f);
                _flame.localPosition = new Vector3(0, scaleY, 0);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_damageCooldown > 0) return;
            if (other.CompareTag("Player"))
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.OnDamage(_damage, transform.position, gameObject);
                    _damageCooldown = 0.3f;
                }
            }
        }
    }
}
