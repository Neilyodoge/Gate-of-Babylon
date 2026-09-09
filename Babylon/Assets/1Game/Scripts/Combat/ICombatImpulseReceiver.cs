using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 可被战斗效果强制位移并打断当前动作的单位。
    /// 未实现此接口的Boss或特殊单位默认免疫受力，不影响其伤害结算。
    /// </summary>
    public interface ICombatImpulseReceiver
    {
        bool TryApplyCombatImpulse(
            Vector3 origin,
            float distance,
            bool interrupt);
    }
}
