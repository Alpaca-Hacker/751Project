using System.Collections.Generic;
using UnityEngine;

namespace SoftBody.Scripts.Core
{
    public class BufferManager : System.IDisposable
    {
        private readonly Dictionary<string, ComputeBuffer> _buffers = new();
        private readonly Dictionary<string, int> _bufferSizes = new();
        
        public void CreateBuffer<T>(string name, int count) where T : struct
        {
            if (_buffers.TryGetValue(name, out var buffer))
            {
                Debug.LogWarning($"Buffer {name} already exists, releasing old buffer");
                buffer?.Release();
            }
            
            _buffers[name] = new ComputeBuffer(count, System.Runtime.InteropServices.Marshal.SizeOf<T>());
            _bufferSizes[name] = count;
        }
        
        public ComputeBuffer GetBuffer(string name)
        {
            if (!_buffers.TryGetValue(name, out var buffer))
            {
                Debug.LogError($"Buffer {name} not found!");
                return null;
            }
            return buffer;
        }
        
        public void SetData<T>(string name, List<T> data) where T : struct
        {
            var buffer = GetBuffer(name);
            if (buffer != null && data.Count <= _bufferSizes[name])
            {
                buffer.SetData(data);
            }
            else
            {
                Debug.LogError($"Cannot set data for buffer {name}: size mismatch");
            }
        }
        
        public void SetData<T>(string name, T[] data) where T : struct
        {
            var buffer = GetBuffer(name);
            if (buffer != null && data.Length <= _bufferSizes[name])
            {
                buffer.SetData(data);
            }
        }
        
        public void GetData<T>(string name, T[] outputArray) where T : struct
        {
            var buffer = GetBuffer(name);
            buffer?.GetData(outputArray);
        }
        
        public float GetTotalMemoryUsageMB()
        {
            float totalBytes = 0f;
            foreach (var buffer in _buffers.Values)
            {
                if (buffer != null && buffer.IsValid())
                {
                    totalBytes += buffer.count * buffer.stride;
                }
            }
            return totalBytes / (1024f * 1024f); // Convert to MB
        }
        
        public void Dispose()
        {
            foreach (var buffer in _buffers.Values)
            {
                buffer?.Release();
            }
            _buffers.Clear();
            _bufferSizes.Clear();
        }
    }
}