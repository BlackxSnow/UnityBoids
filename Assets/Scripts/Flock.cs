using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;
using Yangrc.OpenGLAsyncReadback;

namespace Boids
{
    public class Flock : MonoBehaviour
    {
        /// <summary>
        /// Number of threads assigned to _BoidCompute shader.
        /// </summary>
        private const int _ThreadCount = 512;
        /// <summary>
        /// Data structure for compute input.
        /// </summary>
        private struct BoidData
        {
            public Vector3 Position;
            public Vector3 Heading;
            public BoidData(Vector3 position, Vector3 heading)
            {
                Position = position;
                Heading = heading;
            }
        }
        private static readonly int _BoidDataSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(BoidData));


        private static List<Vector3> _CollisionRayDirections = new List<Vector3>();
        private static bool _IsInitialised = false;

        public static async Task Initialise()
        {
            
        }

        [Header("Resources")] 
        [SerializeField]
        private ComputeShader _BoidCompute;
        [SerializeField]
        private GameObject _BoidPrefab;
        
        [Header("Flocking parameters")] 
        public float ViewDistance = 3;
        public float AlignmentCoefficient = 1;
        public float CohesionCoefficient = 1;
        public float SeparationCoefficient = 1;

        /// <summary>
        /// Quantity of boids to spawn
        /// </summary>
        [Header("Other")] 
        public int InitialBoidCount = 256;
        /// <summary>
        /// Distance from the flock object that the boids may wander.
        /// </summary>
        public int MaxWanderRange = 32;

        public float BoidSpeed = 3;
        

        private TransformAccessArray _BoidTransforms;
        private NativeList<BoidData> _BoidData;
        
        private Vector3[] _GPUDataReadArray;
        

        public void CreateBoids(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Vector3 position = new Vector3()
                {
                    x = transform.position.x + UnityEngine.Random.Range(-20, 20),
                    y = transform.position.x + UnityEngine.Random.Range(-20, 20),
                    z = transform.position.x + UnityEngine.Random.Range(-20, 20)
                };
                _BoidTransforms.Add(Instantiate(_BoidPrefab, position, Quaternion.identity).transform);
                _BoidData.Add(new BoidData(_BoidTransforms[i].position, Vector3.forward));
            }

            if (_GPUDataReadArray.Length < _BoidData.Capacity)
            {
                _GPUDataReadArray = new Vector3[_BoidData.Capacity];
            }
        }
        
        private void Start()
        {
            _BoidTransforms = new TransformAccessArray(InitialBoidCount);
            _BoidData = new NativeList<BoidData>(InitialBoidCount);
            _GPUDataReadArray = new Vector3[InitialBoidCount];
            CreateBoids(InitialBoidCount);
        }
        
        [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
        private void UpdateBoids()
        {
            if (_BoidData.Count == 0) return;
            
            var kernelHandle = _BoidCompute.FindKernel("CSMain");
            
            var inputBuffer = new ComputeBuffer(_BoidData.Count, _BoidDataSize, 
                ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            
            
            inputBuffer.SetData(_BoidData.UnderlyingArray);

            
            // _BoidCompute.SetConstantBuffer("Boids", inputBuffer, 0, _Boids.Count);
            _BoidCompute.SetBuffer(kernelHandle, "Boids", inputBuffer);
            _BoidCompute.SetFloat("AlignmentCoefficient", AlignmentCoefficient);
            _BoidCompute.SetFloat("CohesionCoefficient", CohesionCoefficient);
            _BoidCompute.SetFloat("SeparationCoefficient", SeparationCoefficient);
            _BoidCompute.SetFloat("MaxViewDistanceSquared", Mathf.Pow(ViewDistance, 2));
            _BoidCompute.SetFloat("MaxWanderDistanceSquared", Mathf.Pow(MaxWanderRange, 2));
            _BoidCompute.SetVector("FlockOrigin", transform.position);
            _BoidCompute.SetInt("BoidCount", _BoidData.Count);

            var outputBuffer = new ComputeBuffer(_BoidData.Count, 12 /*sizeof(Vector3)*/,
                ComputeBufferType.Structured, ComputeBufferMode.Dynamic);
            _BoidCompute.SetBuffer(kernelHandle, "Result", outputBuffer);
            
            _BoidCompute.Dispatch(kernelHandle, Mathf.CeilToInt((float)_BoidData.Count / _ThreadCount), 1, 1);
            inputBuffer.Dispose();

            var newHeadings = new NativeArray<Vector3>(_BoidData.Count, Allocator.TempJob);
            outputBuffer.GetData(_GPUDataReadArray, 0, 0, _BoidData.Count);
            newHeadings.CopyFrom(_GPUDataReadArray);

            outputBuffer.Dispose();
            
            UpdateBoidData(newHeadings);
        }

        
        private unsafe struct BoidTransformJob : IJobParallelForTransform
        {
            public NativeArray<Vector3> Headings;
            public float DeltaSpeed;
            [NativeDisableUnsafePtrRestriction]
            public BoidData* Data;
            
            public void Execute(int index, TransformAccess transform)
            {
                transform.localRotation = Quaternion.LookRotation(Headings[index], Vector3.up);
                Data[index].Heading = (transform.rotation * Vector3.forward);
                transform.localPosition += Data[index].Heading * DeltaSpeed;
                Data[index].Position = transform.position;
            }
        }
        
        private unsafe void UpdateBoidData(in NativeArray<Vector3> headings)
        {
            var jobStruct = new BoidTransformJob()
            {
                Headings = headings,
                Data = (BoidData*)_BoidData.GetPointer(),
                DeltaSpeed = BoidSpeed * Time.fixedDeltaTime
            };
            var jobHandle = jobStruct.Schedule(_BoidTransforms);
            jobHandle.Complete();
            headings.Dispose();
        }
        
        private void Update()
        {
            UpdateBoids();
        }
    }

}