using System;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using Unity.VisualScripting;
using UnityEngine;

public class Simulation : MonoBehaviour
{
    private const int metersPerPixel = 30;
    private const int NumThreads = 1024;
    private const int MaxParticlesPerPixel = 8;

    #region Auxiliary Structures
    private struct MeshProperties
    {
        // ReSharper disable once NotAccessedField.Local
        public Matrix4x4 Mat;
        // ReSharper disable once NotAccessedField.Local
        public Vector4 Color;

        public static int Size()
        {
            return
                sizeof(float) * 4 * 4 + // matrix;
                sizeof(float) * 4;      // color;
        }
    }

    struct ShaderParameters
    {
        public float maxL;
        public float timeStep;
    }
    #endregion

    #region Public

    [Header("Initialization")]
    public GameObject mapGameObject;
    public Texture2D elevationTexture; // !: m
    [Range(1, 10)] public int particlesPerPixel;

    [Header("Parameters")]
    public float totalVolume;
    [Range(0.01f, 1f)] public float lInitialConstant;
    [Range(1f, 15f)] public float gravity = 9.8f; // !: m/s²
    [Range(0f, 1f)] public float cfl;
    [Range(0f, 1f)] public float roughnessCoeff; // !: s/m^1/3
    [Range(0f, 1f)] public float forceMagnitude;

    [Header("Rendering")]
    public Material particleMaterial;

    #endregion

    #region Private

    // Shaders
    ComputeShader _bucketShader, _parametersShader, _posVelShader, _updateMeshPropertiesShader;
    private int _threadGroups;

    // Bucket
    private ComputeBuffer _bucketBuffer;

    // Dam
    private CreateDam createDam;
    private bool simulationInitialized;
    private Bounds domain;
    private Bounds mapDomain;

    // Particles
    private ComputeBuffer _positionBuffer; // !: m
    private ComputeBuffer _velocityBuffer; // !: m
    private ComputeBuffer _waterDepthBuffer; // !: m
    private ComputeBuffer _smoothingLengthBuffer; // !: m
    private float texturePixelSize;
    private int n;
    private float v;
    private int elevationTexResolution;

    // Simulation
    private float timeStep;
    private float cutoffDistance;
    private float maxL;
    private int bucketResolution;

    // Rendering
    private Mesh mesh;
    // Rendering
    private ComputeBuffer _particleMeshPropertiesBuffer, _particleArgsBuffer;
    private Bounds _bounds;

    #endregion
    #region Unity

    void Start()
    {
        mesh = GenerateCircleMesh.GetMesh();
        createDam = mapGameObject.GetComponent<CreateDam>();
        domain = new(Vector2.zero, elevationTexture.width * metersPerPixel * Vector2.one);

        cutoffDistance = metersPerPixel / particlesPerPixel;

        elevationTexResolution = elevationTexture.width;
    }

    void Update()
    {
        if (createDam.CreatedDam && !simulationInitialized)
        {
            InitParticles();
            InitShaders();
            simulationInitialized = true;
        }

        if (simulationInitialized)
        {
            BucketGeneration();
            UpdateParameters();
            UpdatePosVel();
            UpdateMeshProperties();

            Graphics.DrawMeshInstancedIndirect(mesh, 0, particleMaterial, _bounds, _particleArgsBuffer);
        }
    }

    void OnDestroy()
    {
        _bucketBuffer?.Release();
        _positionBuffer?.Release();
        _velocityBuffer?.Release();
        _waterDepthBuffer?.Release();
        _smoothingLengthBuffer?.Release();
        _particleMeshPropertiesBuffer?.Release();
        _particleArgsBuffer?.Release();
    }

    #endregion

    #region Initializations

    private void InitShaders()
    {
        _bucketShader = Resources.Load<ComputeShader>("Bucket");
        _parametersShader = Resources.Load<ComputeShader>("Parameters");
        _posVelShader = Resources.Load<ComputeShader>("PosVel");
        _updateMeshPropertiesShader = Resources.Load<ComputeShader>("UpdateMeshProperties");

        _threadGroups = Mathf.CeilToInt((float)n / NumThreads);
    }

    private void InitParticles()
    {
        texturePixelSize = createDam.GetPixelSize();

        var selectedPixels = createDam.GetSelectedPixels();
        List<Vector2> positionList = new();

        mapDomain = new(mapGameObject.transform.position, mapGameObject.transform.localScale);

        for (int x = 0; x < elevationTexResolution; x++)
        {
            for (int y = 0; y < elevationTexResolution; y++)
            {
                if (!selectedPixels[x, y]) continue;

                int startI = x == 0 || particlesPerPixel == 1 || !selectedPixels[x - 1, y] ? 0 : 1;
                int startJ = y == 0 || particlesPerPixel == 1 || !selectedPixels[x, y - 1] ? 0 : 1;

                for (int i = startI; i <= particlesPerPixel; i++)
                {
                    for (int j = startJ; j <= particlesPerPixel; j++)
                    {
                        var position = new Vector2(
                            x * texturePixelSize + i * texturePixelSize / particlesPerPixel,
                            y * texturePixelSize + j * texturePixelSize / particlesPerPixel
                        );

                        positionList.Add((Vector2)domain.min + position / mapDomain.size * domain.size);
                    }
                }
            }
        }

        Vector2[] positions = positionList.ToArray();

        n = positions.Length;
        v = totalVolume / n;

        Debug.Log($"Particle Count: {n}\nParticle Volume: {v}");

        float[] h = new float[n];
        float[] l = new float[n];

        maxL = metersPerPixel / particlesPerPixel;

        _bounds = new Bounds(mapGameObject.transform.position, Vector3.one * 100f);

        uint[] args = { 0, 0, 0, 0, 0 };
        args[0] = mesh.GetIndexCount(0);
        args[1] = (uint)n;
        args[2] = mesh.GetIndexStart(0);
        args[3] = mesh.GetBaseVertex(0);

        _particleArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _particleArgsBuffer.SetData(args);

        _particleMeshPropertiesBuffer = new ComputeBuffer(n, MeshProperties.Size());
        MeshProperties[] properties = new MeshProperties[n];

        for (int i = 0; i < n; i++)
        {
            h[i] = -1;
            l[i] = maxL;

            Vector2 mappedPosition = (positions[i] - (Vector2)domain.min) / domain.size * mapDomain.size + (Vector2)mapDomain.min;

            properties[i] = new MeshProperties
            {
                Mat = Matrix4x4.TRS(mappedPosition, Quaternion.Euler(-90, 0, 0), texturePixelSize / particlesPerPixel * 0.5f * Vector3.one),
                Color = Color.blue
            };
        }

        _particleMeshPropertiesBuffer.SetData(properties);

        particleMaterial.SetBuffer(ShaderIDs.Properties, _particleMeshPropertiesBuffer);

        _positionBuffer = new(n, sizeof(float) * 2);
        _positionBuffer.SetData(positions);

        _velocityBuffer = new(n, sizeof(float) * 2);

        _waterDepthBuffer = new(n, sizeof(float));
        _waterDepthBuffer.SetData(h);

        _smoothingLengthBuffer = new(n, sizeof(float));
        _smoothingLengthBuffer.SetData(l);
    }

    #endregion

    #region Shaders

    private void BucketGeneration()
    {
        bucketResolution = Mathf.CeilToInt(domain.size.x / (2 * maxL));

        // Initialize bucket buffer with maximum possible particles per cell
        var totalBucketSize = bucketResolution * bucketResolution * MaxParticlesPerPixel;
        _bucketBuffer?.Release();
        _bucketBuffer = new ComputeBuffer(totalBucketSize, sizeof(uint));

        // Set shader parameters
        _bucketShader.SetVector(ShaderIDs.DomainMax, domain.max);
        _bucketShader.SetVector(ShaderIDs.DomainMin, domain.min);
        _bucketShader.SetInt(ShaderIDs.N, n);
        _bucketShader.SetInt(ShaderIDs.BucketResolution, bucketResolution);

        // Clear bucket buffer with particle count as empty marker
        _bucketShader.SetBuffer(1, ShaderIDs.Bucket, _bucketBuffer);
        var bucketThreadGroups = Mathf.CeilToInt((float)bucketResolution / 32);

        _bucketShader.Dispatch(1, bucketThreadGroups, bucketThreadGroups, 1);

        // Generate Bucket
        _bucketShader.SetBuffer(0, ShaderIDs.Bucket, _bucketBuffer);
        _bucketShader.SetBuffer(0, ShaderIDs.PositionBuffer, _positionBuffer);

        _bucketShader.Dispatch(0, _threadGroups, 1, 1);
    }

    private void UpdateParameters()
    {
        // Set shader parameters
        _parametersShader.SetVector(ShaderIDs.DomainMax, domain.max);
        _parametersShader.SetVector(ShaderIDs.DomainMin, domain.min);
        _parametersShader.SetFloat(ShaderIDs.TexturePixelSize, texturePixelSize);
        _parametersShader.SetFloat(ShaderIDs.MetersPerPixel, metersPerPixel);
        _parametersShader.SetFloat(ShaderIDs.ParticleVolume, v);
        _parametersShader.SetFloat(ShaderIDs.Gravity, gravity);
        _parametersShader.SetInt(ShaderIDs.N, n);
        _parametersShader.SetInt(ShaderIDs.BucketResolution, bucketResolution);

        ShaderParameters[] data = new ShaderParameters[1];
        data[0] = new ShaderParameters { maxL = 0.0f, timeStep = float.MaxValue };

        ComputeBuffer shaderParametersBuffer = new(1, sizeof(float) * 2);
        shaderParametersBuffer.SetData(data);

        _parametersShader.SetBuffer(0, ShaderIDs.Bucket, _bucketBuffer);
        _parametersShader.SetBuffer(0, ShaderIDs.PositionBuffer, _positionBuffer);
        _parametersShader.SetBuffer(0, ShaderIDs.VelocityBuffer, _velocityBuffer);
        _parametersShader.SetBuffer(0, ShaderIDs.WaterDepthBuffer, _waterDepthBuffer);
        _parametersShader.SetBuffer(0, ShaderIDs.SmoothingLengthBuffer, _smoothingLengthBuffer);
        _parametersShader.SetBuffer(0, ShaderIDs.ShaderParametersBuffer, shaderParametersBuffer);

        _parametersShader.Dispatch(0, _threadGroups, 1, 1);

        shaderParametersBuffer.GetData(data);
        maxL = data[0].maxL;
        if (!float.IsInfinity(data[0].timeStep))
            timeStep = cfl * data[0].timeStep;

        shaderParametersBuffer.Release();
    }

    private void UpdatePosVel()
    {
        // Set shader parameters
        _posVelShader.SetVector(ShaderIDs.DomainMax, domain.max);
        _posVelShader.SetVector(ShaderIDs.DomainMin, domain.min);
        _posVelShader.SetFloat(ShaderIDs.TexturePixelSize, texturePixelSize);
        _posVelShader.SetFloat(ShaderIDs.MetersPerPixel, metersPerPixel);
        _posVelShader.SetFloat(ShaderIDs.ParticleVolume, v);
        _posVelShader.SetFloat(ShaderIDs.Gravity, gravity);
        _posVelShader.SetFloat(ShaderIDs.RoughnessCoeff, roughnessCoeff);
        _posVelShader.SetFloat(ShaderIDs.TimeStep, timeStep);
        _posVelShader.SetInt(ShaderIDs.N, n);
        _posVelShader.SetInt(ShaderIDs.BucketResolution, bucketResolution);

        _posVelShader.SetBuffer(0, ShaderIDs.Bucket, _bucketBuffer);
        _posVelShader.SetBuffer(0, ShaderIDs.PositionBuffer, _positionBuffer);
        _posVelShader.SetBuffer(0, ShaderIDs.VelocityBuffer, _velocityBuffer);
        _posVelShader.SetBuffer(0, ShaderIDs.WaterDepthBuffer, _waterDepthBuffer);
        _posVelShader.SetBuffer(0, ShaderIDs.SmoothingLengthBuffer, _smoothingLengthBuffer);
        _posVelShader.SetTexture(0, ShaderIDs.ElevationTexture, elevationTexture);

        _posVelShader.Dispatch(0, _threadGroups, 1, 1);
    }

    private void UpdateMeshProperties()
    {
        _updateMeshPropertiesShader.SetBuffer(0, ShaderIDs.PositionBuffer, _positionBuffer);
        _updateMeshPropertiesShader.SetBuffer(0, ShaderIDs.WaterDepthBuffer, _waterDepthBuffer);
        _updateMeshPropertiesShader.SetBuffer(0, ShaderIDs.Properties, _particleMeshPropertiesBuffer);

        _updateMeshPropertiesShader.SetInt(ShaderIDs.N, n);
        _updateMeshPropertiesShader.SetVector(ShaderIDs.DomainMax, domain.max);
        _updateMeshPropertiesShader.SetVector(ShaderIDs.DomainMin, domain.min);
        _updateMeshPropertiesShader.SetVector(ShaderIDs.MapMax, mapDomain.max);
        _updateMeshPropertiesShader.SetVector(ShaderIDs.MapMin, mapDomain.min);

        _updateMeshPropertiesShader.Dispatch(0, _threadGroups, 1, 1);
    }

    #endregion
}
