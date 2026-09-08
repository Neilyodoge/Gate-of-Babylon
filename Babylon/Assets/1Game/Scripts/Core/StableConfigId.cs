using System;

namespace XianTu
{
    /// <summary>
    /// 可跨存档和运行时使用的配置身份。比较规则固定为Ordinal，
    /// 不依赖ScriptableObject实例或本地化显示名。
    /// </summary>
    public readonly struct StableConfigId : IEquatable<StableConfigId>
    {
        public string Value { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public StableConfigId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Stable config ID cannot be empty.",
                    nameof(value));

            Value = value.Trim();
        }

        public bool Equals(StableConfigId other)
        {
            return string.Equals(
                Value,
                other.Value,
                StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StableConfigId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(
            StableConfigId left,
            StableConfigId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            StableConfigId left,
            StableConfigId right)
        {
            return !left.Equals(right);
        }
    }
}
