#version 330 core


uniform sampler2D terrainTex;
uniform vec3 playerpos;
uniform float windWaveCounter;
uniform float waterWaveCounter;
uniform int renderPass;
uniform float dropletIntensity = 0;

in vec2 uv;
in vec4 worldPos;
in vec4 fragPosition;
in vec4 gnormal;
flat in int applyPuddles;
flat in int flags;
flat in int waterFlags;
flat in int shinyOrSkyExposed;

bool shiny = renderPass != 4 && shinyOrSkyExposed > 0;

layout(location = 0) out vec4 outGPosition;
layout(location = 1) out vec4 outGNormal;

#include commonnoise.fsh

void CommonPrePass(inout float mul)
{
    mul = shiny ? 0.0 : mul;
}

void GenSplashAt(vec2 coord, inout vec3 normalMap)
{
    for (int i = 0; i < 2; i++){
        float drop = dropletnoise(coord, dropletIntensity, waterWaveCounter - (0.1 * i));
        normalMap.x -= dFdx(drop);
        normalMap.z -= dFdy(drop);
    }
}

void GenSplash(inout vec3 normalMap)
{
    vec2 coord = 8.0 * (worldPos.xz + playerpos.xz) / (2.0 + normalMap.x/3000.0);
    GenSplashAt(coord, normalMap);
}

void GenPuddles(inout vec3 normalMap, inout float mul)
{
    vec2 coord1 = vec2(worldPos.x + playerpos.x, worldPos.z + playerpos.z);
    vec2 coord2 = coord1 * 4 + 16;

    vec3 noisepos1 = vec3(coord1.x, coord1.y, sin(waterWaveCounter * 0.02));
    vec3 noisepos2 = vec3(coord2.x, coord2.y, cos(waterWaveCounter * 0.01));

    float noise1 = (gnoise(noisepos1));
    float noise2 = (gnoise(noisepos2));

    float noise = max((noise1 - noise2), 0);

    if (applyPuddles > 0 && dropletIntensity > 0.0)
    {
        mul = noise > (0.5 * dropletIntensity) ? 1.0 : noise;
        normalMap.x = dFdx(noise * 8.0);
        normalMap.y = noise;
        normalMap.z = dFdy(noise * 8.0);
        
        GenSplash(normalMap);
    }
}

void LiquidPass(inout vec3 normalMap, inout float mul)
{
    bool isLava = (waterFlags & (1<<25)) > 0;
    float div = ((waterFlags & (1<<27)) > 0) ? 90 : 10;
    float wind = ((waterFlags & 0x2000000) == 0) ? 1 : 0;

    vec2 coord1 = vec2(worldPos.x + playerpos.x, worldPos.z + playerpos.z);
    vec2 coord2 = coord1 * 4 + 16;

    vec3 noisepos1 = vec3(coord1.x - windWaveCounter / 6, coord1.y, waterWaveCounter / 12 + wind * windWaveCounter / 6);
    vec3 noisepos2 = vec3(coord2.x - windWaveCounter / 6, coord2.y, waterWaveCounter / 12 + wind * windWaveCounter / 6);

    float noise1 = (gnoise(noisepos1) / div) + (gnoise(noisepos2) / div);

    float noise = isLava ? 1.0 : noise1;

    normalMap = vec3(-dFdx(noise), normalMap.y, -dFdy(noise));
    mul = normalMap.x;

    if (shinyOrSkyExposed > 0) GenSplash(normalMap);
}

void TopsoilPass(inout vec3 normalMap, inout float mul)
{
    GenPuddles(normalMap, mul);
}

void CommonPostPass(float mul, vec3 worldPos, vec3 normalMap)
{
    vec4 color = texture(terrainTex, uv);
    mul = color.a < 0.01 ? 1.0 : mul;

	outGPosition = vec4(worldPos, mul);
	outGNormal = gnormal + vec4(normalMap, mul);
}

void main() 
{
    vec3 normalMap = vec3(0.0);
    float mul = 1.0;

    CommonPrePass(mul);

    switch (renderPass)
    {
        case 0:
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
    
    CommonPostPass(mul, fragPosition.xyz, normalMap);
}