#version 330 core


uniform sampler2D terrainTex;
uniform vec3 playerpos;
uniform float windWaveCounter;
uniform float waterWaveCounter;
uniform int renderPass;
uniform mat4 modelViewMatrix;
uniform float dropletIntensity = 0;
uniform float windIntensity = 0;

in vec2 uv;
in vec3 worldNormal;
in vec3 fragWorldPos;
in vec4 worldPos;
in vec4 fragPosition;
in vec4 gnormal;
flat in int applyPuddles;
flat in int flags;
flat in int waterFlags;
flat in int shinyOrSkyExposed;

bool shiny = renderPass != 4 && shinyOrSkyExposed > 0;
vec3 coord1 = worldPos.xyz + playerpos;

layout(location = 0) out vec4 outGPosition;
layout(location = 1) out vec4 outGNormal;
layout(location = 2) out vec4 outTint;

#include colormap.fsh
#include commonnoise.fsh


vec3 NormalFromNoise(vec3 pos)
{
    vec3 offset = vec3(1.0 / textureSize(terrainTex, 0).x, 1.0 / textureSize(terrainTex, 0).y, 0.1);
    vec3 posCenter = pos.xyz;
    vec3 posNorth = posCenter - offset.zyx;
    vec3 posEast = posCenter + offset.xzy;

    vec3 vertCenter = vec3(posCenter - 0.5) * gnoise(posCenter);
    vec3 vertNorth = vec3(posNorth - 0.5) * gnoise(posNorth);
    vec3 vertEast = vec3(posEast - 0.5) * gnoise(posEast);

    return normalize(cross(vertCenter - vertNorth, vertCenter - vertEast)) * 0.5 + 0.5;
}
void CommonPrePass(inout float mul)
{
    mul = shiny ? 0.0 : mul;
}

// https://gamedev.stackexchange.com/questions/86530/is-it-possible-to-calculate-the-tbn-matrix-in-the-fragment-shader
mat3 CotangentFrame(vec3 N, vec3 p, vec2 uv) {
    vec3 dp1 = dFdx(p);
    vec3 dp2 = dFdy(p);
    vec2 duv1 = dFdx(uv);
    vec2 duv2 = dFdy(uv);

    vec3 dp2perp = cross(dp2, N);
    vec3 dp1perp = cross(N, dp1);
    vec3 T = dp2perp * duv1.x + dp1perp * duv2.x;
    vec3 B = dp2perp * duv1.y + dp1perp * duv2.y;

    float invmax = inversesqrt(max(dot(T, T), dot(B, B)));
    return transpose(mat3(T * invmax, B * invmax, N));
}

float GenSplashAt(vec3 pos)
{
    vec2 uv = 6.0 * pos.xz;

    float totalNoise = 0;
    for (int i = 0; i < 2; ++i) {
        totalNoise += dropletnoise(uv, waterWaveCounter - (0.1*i));
    }
    return totalNoise;
}

void GenSplash(inout vec3 normalMap)
{
    const vec3 deltaPos = vec3(0.01, 0.0, 0.0);
    
    float val0 = GenSplashAt(fragWorldPos.xyz);
    float val1 = GenSplashAt(fragWorldPos.xyz + deltaPos.xyz);
    float val2 = GenSplashAt(fragWorldPos.xyz - deltaPos.xyz);
    float val3 = GenSplashAt(fragWorldPos.xyz + deltaPos.zyx);
    float val4 = GenSplashAt(fragWorldPos.xyz - deltaPos.zyx);

    float xDelta = ((val1 - val0) + (val0 - val2));
    float zDelta = ((val3 - val0) + (val0 - val4));

    normalMap += vec3(xDelta * 0.5, zDelta * 0.5, 0);
}

void GenDenseSurfacePuddles(inout vec3 normalMap, inout float mul)
{
    vec3 coord2 = coord1 * 4 + 16;

    vec3 noisepos1 = vec3(coord1.xy, coord1.z + waterWaveCounter * 0.7);
    vec3 noisepos2 = vec3(coord2.xy, coord2.z + waterWaveCounter * 0.7);

    float noise1 = gnoise(noisepos1);
    float noise2 = gnoise(noisepos2);

    float noise = max((noise1 - noise2), 0);

    if (applyPuddles > 0 && dropletIntensity > 0.0)
    {
        mul = noise > (0.5 * dropletIntensity) ? 1.0 : noise;
        normalMap.x = dFdx(noise * 8.0);
        normalMap.z = dFdy(noise * 8.0);
        
        GenSplash(normalMap);
    }
}

void GenSeepedPuddles(inout vec3 normalMap, inout float mul)
{
    vec3 coord2 = coord1 * 8 + 16;

    vec3 noisepos1 = vec3(coord1.xy, coord1.z + waterWaveCounter * 0.1);
    vec3 noisepos2 = vec3(coord2.xy, coord2.z + waterWaveCounter * 0.02);

    float noise1 = gnoise(noisepos1);
    float noise2 = gnoise(noisepos2);

    float noise = max((noise1 - noise2), 0);

    if (applyPuddles > 0 && dropletIntensity > 0.0)
    {
        mul = noise > 0.1 * dropletIntensity ? 0.0 : 1.0;

        normalMap = NormalFromNoise(noisepos1);
        
        GenSplash(normalMap);
    }
}

void OpaquePass(inout vec3 normalMap, inout float mul)
{
    GenDenseSurfacePuddles(normalMap, mul);
}

void LiquidPass(inout vec3 normalMap, inout float mul)
{
    bool isLava = (waterFlags & (1<<25)) > 0;
    float div = ((waterFlags & (1<<27)) > 0) ? 90 : 10;
    float wind = ((waterFlags & 0x2000000) == 0) ? 1 : 0;
    vec3 coord2 = coord1 * 4 + 16;
    div /= clamp(windIntensity, 0.1, 0.9);

    vec3 noisepos1 = vec3(coord2.x + windWaveCounter / 6, coord2.y, coord2.z + waterWaveCounter / 12 + wind * windWaveCounter / 6);
    vec3 noisepos2 = vec3(coord2.x - windWaveCounter / 6, coord2.y, coord2.z - waterWaveCounter / 12 - wind * windWaveCounter / 6);
    //div *= 1.0 - clamp(windIntensity, 0.1, 1.0);

    vec3 nmNoise = NormalFromNoise(noisepos1) / div + NormalFromNoise(noisepos2) / div;

    normalMap = isLava ? vec3(1) : nmNoise;
    mul = 0.0;

    if (shinyOrSkyExposed > 0 && dropletIntensity > 0.0) GenSplash(normalMap);
}

void TopsoilPass(inout vec3 normalMap, inout float mul)
{
    GenSeepedPuddles(normalMap, mul);
}

void CommonPostPass(float mul, vec3 worldPos, vec3 normalMap, bool skipTint)
{
    vec4 color = texture(terrainTex, uv);
    mul = color.a < 0.01 ? 1.0 : mul;

    mat3 tbn = transpose(CotangentFrame(worldNormal, worldPos.xyz, uv));
    vec3 worldNormalMap = tbn * normalMap;
    vec3 camNormalMap = (modelViewMatrix * vec4(worldNormalMap, 0.0)).xyz;

	outGPosition = vec4(worldPos, mul);
	outGNormal = vec4(normalize(camNormalMap + gnormal.xyz), mul);
    if (!skipTint) outTint = vec4(getColorMapping(terrainTex).rgb, mul);
}

void main() 
{
    vec3 normalMap = vec3(0.0);
    float mul = 1.0;
    bool skipTint = false;

    CommonPrePass(mul);

    switch (renderPass)
    {
        case 0:
            OpaquePass(normalMap, mul);
            break;
        case 1:
            break;
        case 2:
            break;
        case 3:
            break;
        case 4:
            LiquidPass(normalMap, mul);
            break;
        case 5:
            TopsoilPass(normalMap, mul);
            break;
        case 6:
            break;                       
    }
    
    CommonPostPass(mul, fragPosition.xyz, normalMap, skipTint);
}