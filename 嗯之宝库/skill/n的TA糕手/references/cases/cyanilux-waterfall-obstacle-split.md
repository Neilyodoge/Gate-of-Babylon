# VFX-14 · Cyanilux 瀑布遇障碍怎样读成分流

**制作决定。** 一张薄瀑布从斜前方看，若球体只在水幕上留下圆孔，先让缺口从接触点沿水流向下延伸，在灰模里确认水幕分成两股，再加窄泡沫边和滚动纹理。这样先解决水是否绕开障碍，再解决水怎样流。

## 画面观察：圆孔与分流

**Cyanilux，[Waterfall Shader Breakdown](https://www.cyanilux.com/tutorials/waterfall-shader-breakdown/)，2023-05-29。** 实际查看作者的三张静帧：[Scene1.png](https://www.cyanilux.com/tutorials/waterfall-shader-breakdown/Scene1.png)在灰色水幕上留一个近圆孔，球下方仍是一整片；[Scene2.png](https://www.cyanilux.com/tutorials/waterfall-shader-breakdown/Scene2.png)让孔从球附近贯通到水幕下沿，分开的左右轮廓即使无颜色也能辨认。[Preview.png](https://www.cyanilux.com/tutorials/waterfall-shader-breakdown/Preview.png)中，浅青亮边沿着内侧缺口和外轮廓，纵向明暗纹拉出下落方向，暗背景使中间的开口更易读。适用于这种可看见分流轮廓的镜头；接近亮底或更远视距需重验。[2019 年作者原片](https://twitter.com/Cyanilux/status/1190253359703482369)是原发表入口，本轮未播放，不据静帧判断运动速度、移动时的跟随或恢复时序。

## 作者实现与边界

- [Interaction](https://www.cyanilux.com/tutorials/waterfall-shader-breakdown/#shader-graph---interaction)用片元世界坐标减球心得到 `q`。普通球形距离是 `length(q) - r`；开缝时在求长度前改为 `q.y = max(q.y, 0)`，使球心下方同一横截面继续通过同一个裁剪判断。先以距离零点划分保留区，由 Alpha Clip 裁掉内部；再用略外移的距离阈值给未裁掉的一侧着色，亮边贴住缺口。
- [Sine](https://www.cyanilux.com/tutorials/waterfall-shader-breakdown/#shader-graph---sine)用纵向 UV 与时间生成摆动，沿世界切线偏移参与计算的位置，并另裁水幕外侧；[Noise Foam](https://www.cyanilux.com/tutorials/waterfall-shader-breakdown/#shader-graph---noise-foam)再用滚动噪声和顶部、底部区域蒙版处理泡沫。作者为 2023 年拆解重建了 Shader Graph，不能据此还原 2019 年方案的完整节点图。

**算例与适用范围。** 暂关摆动，令相对位置 `q=(0.4,-1.2,0.3)`、半径 `r=0.6`。普通球形距离是 `1.3-0.6=0.7`，仍画水；钳住 Y 后是 `0.5-0.6=-0.1`，该点被裁，球下游在静态截面仍有开缝。这里裁的是薄水幕片元。若水幕倾斜且实际流向不是世界 `-Y`，先用单位下游方向 `u` 求 `s = dot(q,u)`，再试以 `length(q-u*max(s,0))-r` 定义缺口；这只是候选推广，仍需验证曲面与转弯处。若分流不可一直贯通，可另做有长度限制的版本。

**最小对照（未执行）。** 固定近景相机、瀑布网格、球位置与半径。先关摆动及泡沫，只切换普通球形与下游延伸，检查接触点和下沿轮廓；再用同一条窄泡沫边对照亮背景和玩法视距，最后单独恢复扰动看边缘有无跳动。已核对作者灰模 A/B、成图与正文；未看连续原片，未在本地复现、做玩家测试或测目标设备。
