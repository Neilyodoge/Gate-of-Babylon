import {
  Button,
  H1,
  H2,
  Pill,
  Row,
  Stack,
  Text,
  useCanvasState,
  useHostTheme,
} from "cursor/canvas";

type Region = "外城" | "连接区" | "内城";
type RoomType = "普通" | "降落点" | "事件" | "精英" | "商店" | "Boss";
type Decision = "destroy" | "preserve";
type Phase = "白昼" | "永夜";

type Room = {
  id: string;
  region: Region;
  type: RoomType;
  detail: string;
  x: number;
  y: number;
};

type Edge = {
  from: string;
  to: string;
  eventEffect?: boolean;
};

type EventTemplate = {
  id: string;
  title: string;
  destroyNow: string;
  preserveNow: string;
  destroyNext: string;
  preserveNext: string;
};

const eventTemplates: EventTemplate[] = [
  {
    id: "A1",
    title: "通路事件",
    destroyNow: "强拆侧门，白昼立即开放近路并取得材料",
    preserveNow: "稳定开启，白昼立即开放安全通路",
    destroyNext: "永夜保留破口，并追加警戒或伏击",
    preserveNext: "永夜保留安全捷径",
  },
  {
    id: "B1",
    title: "供能事件",
    destroyNow: "拆取能源核心，白昼立即取得较多材料",
    preserveNow: "重新供能，白昼立即点亮设施并获得战力收益",
    destroyNext: "永夜区域失去供能，改变敌群或材料分布",
    preserveNext: "永夜保留照明或设施支持",
  },
  {
    id: "C1",
    title: "封存事件",
    destroyNow: "释放力量，白昼立即取得材料",
    preserveNow: "完成封存，白昼立即移除危险物",
    destroyNext: "永夜追加高价值遭遇并提高 Boss 压力",
    preserveNext: "永夜降低区域或 Boss 压力",
  },
];

function makeRandom(seed: number) {
  let value = seed >>> 0;
  return () => {
    value += 0x6d2b79f5;
    let t = value;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function generateMap(seed: number) {
  const random = makeRandom(seed);
  const jitter = () => Math.round((random() - 0.5) * 34);
  const spawnRegion: Region = random() < 0.5 ? "外城" : "内城";
  const worldRotation = [0, 90, 180, 270][Math.floor(random() * 4)];
  const landingCandidates = spawnRegion === "外城" ? ["O2", "O4"] : ["I0", "I3"];
  const landingRoomId = landingCandidates[Math.floor(random() * landingCandidates.length)];
  const event = eventTemplates[Math.floor(random() * eventTemplates.length)];
  const eventRoomId = spawnRegion === "外城" ? "I1" : "O1";
  const bossName = spawnRegion === "外城" ? "Boss A" : "Boss B";
  const normalContents = ["敌群＋材料", "机关＋门锁", "情报＋宝箱", "挑战＋奖励", "恢复＋风险"];
  const pickNormal = () => normalContents[Math.floor(random() * normalContents.length)];

  const rooms: Room[] = [
    { id: "O0", region: "外城", type: spawnRegion === "内城" ? "Boss" : "普通", detail: spawnRegion === "内城" ? "Boss B" : "未激活 Boss 地标", x: 105, y: 350 + jitter() },
    { id: "O1", region: "外城", type: "事件", detail: eventRoomId === "O1" ? "主要事件" : "随机事件", x: 255, y: 175 + jitter() },
    { id: "O2", region: "外城", type: landingRoomId === "O2" ? "降落点" : "普通", detail: landingRoomId === "O2" ? "安全候选房＋随机出生点" : pickNormal(), x: 410, y: 155 + jitter() },
    { id: "O3", region: "外城", type: "精英", detail: "高风险奖励", x: 255, y: 525 + jitter() },
    { id: "O4", region: "外城", type: landingRoomId === "O4" ? "降落点" : "普通", detail: landingRoomId === "O4" ? "安全候选房＋随机出生点" : pickNormal(), x: 470, y: 530 + jitter() },
    { id: "C0", region: "连接区", type: "普通", detail: pickNormal(), x: 705, y: 235 + jitter() },
    { id: "C1", region: "连接区", type: "商店", detail: "构筑调整", x: 705, y: 480 + jitter() },
    { id: "I0", region: "内城", type: landingRoomId === "I0" ? "降落点" : "普通", detail: landingRoomId === "I0" ? "安全候选房＋随机出生点" : pickNormal(), x: 885, y: 350 + jitter() },
    { id: "I1", region: "内城", type: "事件", detail: eventRoomId === "I1" ? "主要事件" : "随机事件", x: 1045, y: 175 + jitter() },
    { id: "I2", region: "内城", type: "精英", detail: "高风险奖励", x: 1045, y: 525 + jitter() },
    { id: "I3", region: "内城", type: landingRoomId === "I3" ? "降落点" : "普通", detail: landingRoomId === "I3" ? "安全候选房＋随机出生点" : pickNormal(), x: 1200, y: 105 + jitter() },
    { id: "I4", region: "内城", type: spawnRegion === "外城" ? "Boss" : "普通", detail: spawnRegion === "外城" ? "Boss A" : "未激活 Boss 地标", x: 1340, y: 350 + jitter() },
  ];

  const edges: Edge[] = [
    { from: "O0", to: "O1" },
    { from: "O0", to: "O3" },
    { from: "O1", to: "O2" },
    { from: "O3", to: "O4" },
    { from: "O1", to: "O3" },
    { from: "O2", to: "C0" },
    { from: "O4", to: "C1" },
    { from: "C0", to: "C1" },
    { from: "C0", to: "I0" },
    { from: "C1", to: "I0" },
    { from: "I0", to: "I1" },
    { from: "I0", to: "I2" },
    { from: "I1", to: "I2" },
    { from: "I1", to: "I3" },
    { from: "I2", to: "I4" },
    { from: "I1", to: "I4" },
  ];

  const eventEdgeCandidates: Edge[] = [
    ...(eventRoomId === "O1"
      ? [{ from: "O1", to: "C1" }, { from: "O2", to: "O4" }, { from: "O2", to: "O0" }]
      : [{ from: "I1", to: "C1" }, { from: "I2", to: "I3" }, { from: "I3", to: "I4" }]),
  ];
  const eventEdgeIndex = Math.floor(random() * eventEdgeCandidates.length);

  return {
    rooms,
    edges,
    spawnRegion,
    worldRotation,
    event,
    eventRoomId,
    eventEdge: eventEdgeCandidates[eventEdgeIndex],
    bossName,
  };
}

function roomFill(type: RoomType, theme: ReturnType<typeof useHostTheme>) {
  if (type === "Boss") return theme.accent.primary;
  if (type === "事件") return theme.fill.secondary;
  if (type === "降落点") return theme.fill.primary;
  if (type === "精英") return theme.fill.tertiary;
  return theme.bg.elevated;
}

function MapRoom({
  room,
  event,
  eventRoomId,
  outcomeLabel,
  bossState,
}: {
  room: Room;
  event: EventTemplate;
  eventRoomId: string;
  outcomeLabel?: string;
  bossState?: string;
  key?: string;
}) {
  const theme = useHostTheme();
  const isCausalEvent = room.type === "事件" && room.id === eventRoomId;
  const title = isCausalEvent
    ? outcomeLabel || `事件 ${event.id}`
    : room.type === "Boss"
      ? room.detail
      : room.type === "普通"
      ? room.detail
      : room.type;

  return (
    <g>
      <rect
        x={room.x - 70}
        y={room.y - 32}
        width="140"
        height="64"
        rx="12"
        fill={roomFill(room.type, theme)}
        stroke={room.type === "Boss" || isCausalEvent ? theme.accent.primary : theme.stroke.primary}
        strokeWidth={room.type === "Boss" || isCausalEvent ? "2" : "1"}
      />
      <foreignObject x={room.x - 63} y={room.y - 21} width="126" height="44">
        <div style={{ textAlign: "center" }}>
          <Text size="small" weight="semibold">{title}</Text>
          <div style={{ marginTop: 2 }}>
            <Text size="small" tone="secondary">
              {room.type === "Boss"
                ? `${room.id} · ${bossState || "标准机制"}`
                : room.type === "普通"
                  ? room.id
                  : `${room.id} · ${room.detail}`}
            </Text>
          </div>
        </div>
      </foreignObject>
    </g>
  );
}

export default function RandomMapTemplateCanvas() {
  const theme = useHostTheme();
  const [seed, setSeed] = useCanvasState("random-map-seed", 20260807);
  const [decision, setDecision] = useCanvasState<Decision>("random-map-decision", "destroy");
  const [phase, setPhase] = useCanvasState<Phase>("random-map-phase", "白昼");
  const generated = generateMap(seed);
  const currentTime = phase;
  const roomById = Object.fromEntries(generated.rooms.map((room) => [room.id, room]));
  const immediateEffect =
    decision === "destroy" ? generated.event.destroyNow : generated.event.preserveNow;
  const delayedEffect =
    decision === "destroy" ? generated.event.destroyNext : generated.event.preserveNext;
  const timeModifier =
    currentTime === "白昼"
      ? "白昼阶段：视野清晰、巡逻敌群活跃、加冕秩序仍在运转。"
      : "永夜阶段：亡者设施显露，使用永夜敌池并使白昼结果落位。";
  const eventCategory = generated.event.id.charAt(0);
  const outcomeLabel =
    eventCategory === "A"
      ? decision === "destroy" ? "高风险近路" : "稳定奖励路线"
      : eventCategory === "B"
        ? decision === "destroy" ? "断电区域" : "供能区域"
        : decision === "destroy" ? "追猎精英" : "调查交互";
  const affectedBossId = generated.spawnRegion === "外城" ? "I4" : "O0";
  const bossState =
    eventCategory === "B"
      ? decision === "destroy" ? "护盾关闭" : "护盾保留"
      : "标准机制";
  const displayedEdges: Edge[] =
    eventCategory === "A" && decision === "destroy"
      ? [...generated.edges, { ...generated.eventEdge, eventEffect: true }]
      : generated.edges;

  const refresh = () => {
    setSeed((current) => current + 1);
    setDecision("destroy");
    setPhase("白昼");
  };

  return (
    <Stack gap={20} style={{ padding: 26, maxWidth: 1500, margin: "0 auto" }}>
      <Row justify="space-between" align="start">
        <Stack gap={7}>
          <H1>关卡 A：无暮王城双阶段地图</H1>
          <Text tone="secondary">
            12 个主体房 · 白昼 → 永夜 · 即时反馈 + 反转阶段后果
          </Text>
          <Row gap={7} wrap>
            <Pill active>{currentTime}</Pill>
            <Pill>{`${generated.spawnRegion}出生`}</Pill>
            <Pill>{generated.bossName}</Pill>
            <Pill>{`整图旋转 ${generated.worldRotation}°`}</Pill>
            <Pill>固定精英 2</Pill>
            <Pill>{`Seed ${seed}`}</Pill>
          </Row>
        </Stack>
        <Row gap={8} wrap justify="end">
          <Button variant="primary" onClick={refresh}>刷新地图</Button>
          <Button
            variant="secondary"
            onClick={() => setPhase((current) => current === "白昼" ? "永夜" : "白昼")}
          >
            {phase === "白昼" ? "查看永夜" : "查看白昼"}
          </Button>
          <Button variant={decision === "destroy" ? "secondary" : "ghost"} onClick={() => setDecision("destroy")}>
            选择破坏
          </Button>
          <Button variant={decision === "preserve" ? "secondary" : "ghost"} onClick={() => setDecision("preserve")}>
            选择保留
          </Button>
        </Row>
      </Row>

      <div style={{ overflowX: "auto", border: `1px solid ${theme.stroke.primary}`, borderRadius: 12 }}>
        <svg
          viewBox="0 0 1450 720"
          role="img"
          aria-label="随机生成的内外城关卡地图"
          style={{ width: "100%", minWidth: 1120, display: "block", background: theme.bg.editor }}
        >
          <rect x="20" y="50" width="555" height="640" rx="14" fill={theme.fill.quaternary} />
          <rect x="590" y="50" width="245" height="640" rx="14" fill={theme.bg.elevated} />
          <rect x="850" y="50" width="580" height="640" rx="14" fill={theme.fill.quaternary} />

          <foreignObject x="42" y="66" width="180" height="30">
            <Text weight="semibold">外城 · 5 房</Text>
          </foreignObject>
          <foreignObject x="612" y="66" width="180" height="30">
            <Text weight="semibold">连接区 · 2 房</Text>
          </foreignObject>
          <foreignObject x="872" y="66" width="180" height="30">
            <Text weight="semibold">内城 · 5 房</Text>
          </foreignObject>

          {displayedEdges.map((edge, index) => {
            const from = roomById[edge.from];
            const to = roomById[edge.to];
            return (
              <line
                key={`${edge.from}-${edge.to}-${index}`}
                x1={from.x}
                y1={from.y}
                x2={to.x}
                y2={to.y}
                stroke={edge.eventEffect ? theme.accent.primary : theme.stroke.secondary}
                strokeWidth={edge.eventEffect ? "4" : "3"}
                strokeDasharray={edge.eventEffect ? "3 4" : undefined}
                opacity={edge.eventEffect ? 0.9 : 0.72}
              />
            );
          })}

          {generated.rooms.map((room) => (
            <MapRoom
              key={room.id}
              room={room}
              event={generated.event}
              eventRoomId={generated.eventRoomId}
              outcomeLabel={room.id === generated.eventRoomId ? outcomeLabel : undefined}
              bossState={room.id === affectedBossId ? bossState : "标准机制"}
            />
          ))}
        </svg>
      </div>

      <Stack gap={10}>
        <H2>上图说明</H2>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
            gap: 14,
          }}
        >
          <div style={{ padding: 15, background: theme.bg.elevated, borderRadius: 9 }}>
            <Text weight="semibold">固定骨架</Text>
            <div style={{ marginTop: 7 }}>
              <Text size="small" tone="secondary">
                上图以布局模板的局部坐标展示。实际世界可整体旋转，但 Boss、商店、事件与精英之间的局部相对关系不变。
              </Text>
            </div>
          </div>
          <div style={{ padding: 15, background: theme.bg.elevated, borderRadius: 9 }}>
            <Text weight="semibold">刷新时随机</Text>
            <div style={{ marginTop: 7 }}>
              <Text size="small" tone="secondary">
                “刷新地图”会重抽新循环的出生区、降落房、整图旋转角和微内容；永夜复用当前布局并从原出生区安全端重新降落，永夜通关后重置白昼。
              </Text>
            </div>
          </div>
          <div style={{ padding: 15, background: theme.bg.elevated, borderRadius: 9 }}>
            <Text weight="semibold">首测边界</Text>
            <div style={{ marginTop: 7 }}>
              <Text size="small" tone="secondary">
                事件在白昼立即反馈，并可把最多两个结果提交到永夜；永夜完成后清除。
              </Text>
            </div>
          </div>
        </div>
        <Text size="small" tone="secondary">
          阅读方式：实线是主路线与区域环路，粗短虚线只表示通路事件临时开启的路线；O / C / I 分别代表外城、连接区和内城。
        </Text>
      </Stack>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "1.15fr 0.85fr",
          gap: 18,
          alignItems: "start",
        }}
      >
        <Stack gap={9}>
          <H2>双阶段因果</H2>
          <div
            style={{
              padding: 16,
              borderLeft: `4px solid ${theme.accent.primary}`,
              background: theme.bg.elevated,
              borderRadius: 9,
            }}
          >
            <Text weight="semibold">
              {`事件 ${generated.event.id} · ${generated.event.title}`}
            </Text>
            <div style={{ marginTop: 8 }}>
              <Text>
                {`当前选择：${decision === "destroy" ? "破坏" : "保留"}。即时结果：${immediateEffect}`}
              </Text>
            </div>
            <div style={{ marginTop: 8 }}>
              <Text>{`永夜结果：${delayedEffect}`}</Text>
            </div>
            <div style={{ marginTop: 8 }}>
              <Text size="small">{timeModifier}</Text>
            </div>
            <div style={{ marginTop: 8 }}>
              <Text size="small" tone="secondary">
                白昼 Boss 后提交；永夜从安全端重新降落，结果成功落位并持续显示“上次行动”；永夜通关后清除并重置白昼。
              </Text>
            </div>
          </div>
        </Stack>

        <Stack gap={9}>
          <H2>图例与硬约束</H2>
          <Text size="small">实线：主路线与区域环路　粗短虚线：仅由通路事件产生</Text>
          <Text size="small">外城 5 房 + 连接区 2 房 + 内城 5 房 = 12 房</Text>
          <Text size="small">每阶段单 Boss、双精英、双事件；事件不封锁唯一通路</Text>
          <Text size="small">事件固定上侧、精英固定下侧、商店固定连接区；整图不镜像</Text>
        </Stack>
      </div>

      <Stack gap={12}>
        <Row justify="space-between" align="center">
          <H2>总事件与内容池</H2>
          <Row gap={7} wrap>
            <Pill>3 类事件系统</Pill>
            <Pill>即时 + 永夜结果</Pill>
            <Pill>5 类房间微内容</Pill>
            <Pill>固定双精英</Pill>
          </Row>
        </Row>
        <div style={{ padding: 14, background: theme.fill.tertiary, borderRadius: 9 }}>
          <Text weight="semibold">阶段与事件分工</Text>
          <div style={{ marginTop: 6 }}>
            <Text size="small" tone="secondary">
              事件因果不随阶段改写：白昼先反馈，永夜兑现后果；阶段统一改变环境、敌群和关键模块。
            </Text>
          </div>
        </div>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "1.35fr 0.65fr",
            gap: 20,
            alignItems: "start",
          }}
        >
          <Stack gap={8}>
            <Text weight="semibold">事件池 · 白昼 2 个事件槽，其中 1 个主要事件</Text>
            {eventTemplates.map((event) => (
              <div
                key={event.id}
                style={{
                  display: "grid",
                  gridTemplateColumns: "150px 1fr",
                  gap: 14,
                  padding: "11px 0",
                  borderBottom: `1px solid ${theme.stroke.tertiary}`,
                }}
              >
                <Stack gap={4}>
                  <Text size="small" weight="semibold">{`${event.id} · ${event.title}`}</Text>
                  <Text size="small" tone="secondary">
                    白昼即时 + 永夜后果
                  </Text>
                </Stack>
                <Stack gap={4}>
                  <Text size="small">{`破坏：${event.destroyNow}；${event.destroyNext}`}</Text>
                  <Text size="small" tone="secondary">{`保留：${event.preserveNow}；${event.preserveNext}`}</Text>
                </Stack>
              </div>
            ))}
          </Stack>

          <Stack gap={14}>
            <div>
              <Text weight="semibold">房间微内容</Text>
              <div style={{ marginTop: 8 }}>
                <Text size="small" tone="secondary">
                  战斗＋材料／机关＋门锁／情报＋宝箱／挑战＋奖励／恢复＋风险
                </Text>
              </div>
            </div>
            <div>
              <Text weight="semibold">固定功能房</Text>
              <div style={{ marginTop: 8 }}>
                <Text size="small" tone="secondary">
                  相对随机的安全降落房、连接区商店，以及由出生区决定的 Boss A 或 Boss B。休息点与军械库首测不生成。
                </Text>
              </div>
            </div>
            <div>
              <Text weight="semibold">随机战斗内容</Text>
              <div style={{ marginTop: 8 }}>
                <Text size="small" tone="secondary">
                  外城与内城各固定 1 个基础精英；封存事件可额外追加 1 个故事精英，不替换基础精英。
                </Text>
              </div>
            </div>
            <div>
              <Text weight="semibold">重复进入边界</Text>
              <div style={{ marginTop: 8 }}>
                <Text size="small" tone="secondary">
                  白昼完成后关卡保持永夜；永夜死亡或返回不重打白昼。经基地再次进入时重新构筑。
                </Text>
              </div>
            </div>
          </Stack>
        </div>
      </Stack>

      <Stack gap={10}>
        <H2>收敛后的设计逻辑</H2>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(3, minmax(0, 1fr))", gap: 12 }}>
          <div style={{ padding: 14, border: `1px solid ${theme.stroke.primary}`, borderRadius: 9 }}>
            <Text weight="semibold">首测只留完整闭环</Text>
            <div style={{ marginTop: 6 }}>
              <Text size="small" tone="secondary">
                12 房保留普通战斗、双精英、双事件、商店和单 Boss；出生位置相对随机，关键建筑关系固定在布局局部坐标中，整图可旋转但不镜像。
              </Text>
            </div>
          </div>
          <div style={{ padding: 14, border: `1px solid ${theme.stroke.primary}`, borderRadius: 9 }}>
            <Text weight="semibold">结果延续一阶段</Text>
            <div style={{ marginTop: 6 }}>
              <Text size="small" tone="secondary">
                通路、供能、封存都在白昼立即反馈，最多两个结果在永夜实体落位；永夜通关后清除，并为下一循环重置白昼。
              </Text>
            </div>
          </div>
          <div style={{ padding: 14, border: `1px solid ${theme.accent.primary}`, borderRadius: 9 }}>
            <Text weight="semibold">回顾保持简单</Text>
            <div style={{ marginTop: 6 }}>
              <Text size="small" tone="secondary">
                永夜左下角长期显示“上次行动”；经基地进入时由传送门提示当前永夜及上次变化。
              </Text>
            </div>
          </div>
        </div>
      </Stack>
    </Stack>
  );
}
