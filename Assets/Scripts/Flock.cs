using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

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
            private readonly Vector3 Position;
            private readonly Vector3 Heading;
            public BoidData(Vector3 position, Vector3 heading)
            {
                Position = position;
                Heading = heading;
            }
        }

        private static readonly int _BoidDataSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(BoidData));
        /// <summary>
        /// Defines a single flock member.
        /// </summary>
        private struct Boid
        {
            public readonly Transform Transform;
            public Vector3 Heading;

            public Boid(Transform transform, Vector3 heading)
            {
                Transform = transform;
                Heading = heading;
            }
        }
        
        
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

        private List<Boid> _Boids = new List<Boid>();
        private BoidData[] _BoidData;

        public void CreateBoids(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 position = new Vector3()
                {
                    x = transform.position.x + UnityEngine.Random.Range(-20, 20),
                    y = transform.position.x + UnityEngine.Random.Range(-20, 20),
                    z = transform.position.x + UnityEngine.Random.Range(-20, 20)
                };
                _Boids.Add(new Boid(Instantiate(_BoidPrefab, position, Quaternion.identity).transform, Vector3.forward));
            }

            _BoidData = new BoidData[_Boids.Count];
        }
        
        private void Start()
        {
            CreateBoids(InitialBoidCount);
        }

        private void FillBoidDataArray()
        {
            for (var i = 0; i < _BoidData.Length; i++)
            {
                _BoidData[i] = new BoidData(_Boids[i].Transform.position, _Boids[i].Heading);
            }
        }
        
        [SuppressMessage("ReSharper", "UseObjectOrCollectionInitializer")]
        private void UpdateBoidData()
        {
            if (_Boids.Count == 0) return;
            
            var kernelHandle = _BoidCompute.FindKernel("CSMain");
            
            var inputBuffer = new ComputeBuffer(_Boids.Count, _BoidDataSize, 
                ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            
            FillBoidDataArray();
            inputBuffer.SetData(_BoidData);

            
            // _BoidCompute.SetConstantBuffer("Boids", inputBuffer, 0, _Boids.Count);
            _BoidCompute.SetBuffer(kernelHandle, "Boids", inputBuffer);
            _BoidCompute.SetFloat("AlignmentCoefficient", AlignmentCoefficient);
            _BoidCompute.SetFloat("CohesionCoefficient", CohesionCoefficient);
            _BoidCompute.SetFloat("SeparationCoefficient", SeparationCoefficient);
            _BoidCompute.SetFloat("MaxViewDistanceSquared", Mathf.Pow(ViewDistance, 2));
            _BoidCompute.SetInt("BoidCount", _Boids.Count);

            var outputBuffer = new ComputeBuffer(_Boids.Count, 12 /*sizeof(Vector3)*/,
                ComputeBufferType.Structured, ComputeBufferMode.Dynamic);
            _BoidCompute.SetBuffer(kernelHandle, "Result", outputBuffer);
            
            _BoidCompute.Dispatch(kernelHandle, Mathf.CeilToInt((float)_Boids.Count / _ThreadCount), 1, 1);
            inputBuffer.Dispose();

            var newHeadings = new Vector3[_Boids.Count];
            outputBuffer.GetData(newHeadings);
            outputBuffer.Dispose();

            for (var i = 0; i < _Boids.Count; i++)
            {
                _Boids[i] = new Boid(_Boids[i].Transform, newHeadings[i]);
                _Boids[i].Transform.localRotation = Quaternion.LookRotation(newHeadings[i], Vector3.up);
            }
        }

        struct MoveBoidsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<Boid> boids;
            [ReadOnly] public float deltaSpeed;

            public void Execute(int index, TransformAccess transform)
            {
                // transform
            }
        }
        
        private void DoBoidMove()
        {
            // var taa = new TransformAccessArray()
            // var job = new MoveBoidsJob();
            // job.boids.CopyFrom(_Boids.ToArray());
            // job.Schedule()
            float deltaSpeed = BoidSpeed * Time.fixedDeltaTime;
            for (var i = 0; i < _Boids.Count; i++)
            {
                _Boids[i].Transform.localPosition += _Boids[i].Heading * deltaSpeed;
            }
        }
        private void Update()
        {
            UpdateBoidData();
            DoBoidMove();
        }
    }

}