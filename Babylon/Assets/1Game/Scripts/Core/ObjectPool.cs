using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 简易对象池，用于投射物、特效等频繁创建/销毁的对象
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Queue<GameObject>> _pools = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>从池中获取对象</summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            string key = prefab.name;
            if (_pools.TryGetValue(key, out var queue))
            {
                // 从队列中取出对象，跳过已被销毁的
                while (queue.Count > 0)
                {
                    var obj = queue.Dequeue();
                    if (obj != null)
                    {
                        obj.transform.SetPositionAndRotation(position, rotation);
                        obj.SetActive(true);
                        return obj;
                    }
                    // obj 已被销毁，继续取下一个
                }
            }

            var newObj = Instantiate(prefab, position, rotation, transform);
            newObj.name = key; // 去掉 (Clone) 后缀，方便回收时匹配
            return newObj;
        }

        /// <summary>回收对象到池中</summary>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);
            string key = obj.name;
            if (!_pools.ContainsKey(key))
                _pools[key] = new System.Collections.Generic.Queue<GameObject>();
            _pools[key].Enqueue(obj);
        }

        /// <summary>延迟回收</summary>
        public void Return(GameObject obj, float delay)
        {
            StartCoroutine(ReturnDelayed(obj, delay));
        }

        private System.Collections.IEnumerator ReturnDelayed(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
                Return(obj);
        }

        /// <summary>清空所有池</summary>
        public void ClearAll()
        {
            foreach (var kvp in _pools)
            {
                while (kvp.Value.Count > 0)
                {
                    var obj = kvp.Value.Dequeue();
                    if (obj != null) Destroy(obj);
                }
            }
            _pools.Clear();
        }
    }
}
