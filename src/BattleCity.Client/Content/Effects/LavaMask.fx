#if OPENGL
    #define SV_POSITION POSITION
    #define PS_SHADERMODEL ps_3_0
    #define VS_SHADERMODEL vs_3_0
#else
    #define PS_SHADERMODEL ps_4_0_level_9_1
    #define VS_SHADERMODEL vs_4_0_level_9_1
#endif

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

Texture2D LavaFill;
sampler2D LavaFillSampler = sampler_state
{
    Texture = <LavaFill>;
    AddressU = Clamp;
    AddressV = Clamp;
};

float4x4 MatrixTransform;
float2 SourcePos;
float2 SourceSize;
float FrameIndex;
float FrameCount;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

VertexShaderOutput MainVS(float4 position : POSITION0, float4 color : COLOR0, float2 texCoord : TEXCOORD0)
{
    VertexShaderOutput output;
    output.Position = mul(position, MatrixTransform);
    output.Color = color;
    output.TextureCoordinates = texCoord;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 mask = tex2D(SpriteTextureSampler, input.TextureCoordinates);
    if (mask.a < 0.08)
    {
        discard;
    }

    if (mask.r > 0.90 && mask.g < 0.12 && mask.b > 0.90)
    {
        float2 localUv = (input.TextureCoordinates - SourcePos) / max(SourceSize, float2(0.0001, 0.0001));
        float frames = max(FrameCount, 1.0);
        float2 fillUv = float2(localUv.x, (FrameIndex + saturate(localUv.y)) / frames);
        return tex2D(LavaFillSampler, fillUv) * input.Color;
    }

    return mask * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
