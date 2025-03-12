using UnityEngine;

public class ShaderIDs
{
    public static readonly int N = Shader.PropertyToID("_N");
    public static readonly int BucketResolution = Shader.PropertyToID("_BucketResolution");
    public static readonly int Bucket = Shader.PropertyToID("_Bucket");
    public static readonly int PositionBuffer = Shader.PropertyToID("_PositionBuffer");
    public static readonly int DomainMin = Shader.PropertyToID("_DomainMin");
    public static readonly int DomainMax = Shader.PropertyToID("_DomainMax");
    public static readonly int MapMin = Shader.PropertyToID("_MapMin");
    public static readonly int MapMax = Shader.PropertyToID("_MapMax");
    public static readonly int TexturePixelSize = Shader.PropertyToID("_TexturePixelSize");
    public static readonly int MetersPerPixel = Shader.PropertyToID("_MetersPerPixel");
    public static readonly int ParticleVolume = Shader.PropertyToID("_ParticleVolume");
    public static readonly int Gravity = Shader.PropertyToID("_Gravity");
    public static readonly int MaxL = Shader.PropertyToID("_MaxL");
    public static readonly int TimeStep = Shader.PropertyToID("_TimeStep");
    public static readonly int VelocityBuffer = Shader.PropertyToID("_VelocityBuffer");
    public static readonly int ShaderParametersBuffer = Shader.PropertyToID("_ShaderParametersBuffer");
    public static readonly int WaterDepthBuffer = Shader.PropertyToID("_WaterDepthBuffer");
    public static readonly int SmoothingLengthBuffer = Shader.PropertyToID("_SmoothingLengthBuffer");
    public static readonly int RoughnessCoeff = Shader.PropertyToID("_RoughnessCoeff");
    public static readonly int ElevationTexture = Shader.PropertyToID("_ElevationTexture");
    public static readonly int Properties = Shader.PropertyToID("_Properties");
    public static readonly int ParticleRadius = Shader.PropertyToID("_ParticleRadius");
}
