
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Kernel
{
    public static class AddressableRef
    {
        private static readonly Dictionary<string, Object> _cache = new();

        public static async Task<T> LoadAsync<T>(string address) where T : Object
        {
            if (string.IsNullOrEmpty(address)) return null;
            if (_cache.TryGetValue(address, out var obj)) return obj as T;

            var handle = Addressables.LoadAssetAsync<T>(address);
            var asset = await handle.Task;
            _cache[address] = asset;
            return asset;
        }
    }
}