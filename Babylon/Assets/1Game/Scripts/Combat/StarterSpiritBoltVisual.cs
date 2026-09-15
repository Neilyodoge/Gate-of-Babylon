using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 让灵息弹的循环粒子在对象池每次租用时从头播放。
    /// </summary>
    public sealed class StarterSpiritBoltVisual : MonoBehaviour
    {
        private ParticleSystem[] _particles;

        private void Awake()
        {
            CacheParticles();
        }

        private void OnEnable()
        {
            CacheParticles();
            foreach (ParticleSystem particle in _particles)
            {
                particle.Clear(true);
                particle.Play(true);
            }
        }

        private void OnDisable()
        {
            if (_particles == null)
                return;
            foreach (ParticleSystem particle in _particles)
            {
                particle.Stop(
                    true,
                    ParticleSystemStopBehavior
                        .StopEmittingAndClear);
            }
        }

        private void CacheParticles()
        {
            _particles ??=
                GetComponentsInChildren<ParticleSystem>(true);
        }
    }
}
