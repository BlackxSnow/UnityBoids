using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.Experimental.GraphView;

namespace Boids
{
    /// <summary>
    /// Dynamically resizing wrapper around Unity's NativeArray<T>
    /// </summary>
    public class NativeList<T> where T : struct
    {
        private NativeArray<T> _Data;
        public NativeArray<T> UnderlyingArray => _Data;
        public int Count { get; private set; }
        private int _Capacity;
        public int Capacity
        {
            get => _Capacity;
            set
            {
                Resize(value);
                _Capacity = value;
            }
        }

        private void Resize(int size)
        {
            _Data.Dispose();
            _Data = new NativeArray<T>(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public void Add(in T value)
        {
            if (Count == _Capacity) Capacity *= 2;
            _Data[Count] = value;
            Count++;
        }

        public T this[int key]
        {
            get
            {
                if (key >= Count) throw new IndexOutOfRangeException();
                return _Data[key];
            }
            set 
            {
                if (key >= Count) throw new IndexOutOfRangeException();
                _Data[key] = value;
            }
        }

        public unsafe void* GetPointer()
        {
            return _Data.GetUnsafePtr();
        }

        public NativeList()
        {
            const int baseCapacity = 8;
            _Data = new NativeArray<T>(baseCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _Capacity = baseCapacity;
        }
        public NativeList(int capacity)
        {
            _Data = new NativeArray<T>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _Capacity = capacity;
        }
    }
}