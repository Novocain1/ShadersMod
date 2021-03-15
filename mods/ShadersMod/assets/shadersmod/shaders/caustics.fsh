
#version 330 core

uniform sampler2D gDepth;
uniform sampler2D gNormal;
uniform sampler2D caustics;
uniform sampler2D gLight;

uniform mat4 invProjectionMatrix;
uniform mat4 invModelViewMatrix;

uniform float dayLight;
uniform vec3 sunPosition;
uniform vec3 playerPos;

in vec2 texcoord;
layout(location=0) out vec4 outStrength;

uniform float fogDensityIn;
uniform float fogMinIn;
uniform vec4 rgbaFog;

uniform float waterFlowCounter;

#include noise3d.ash
#include fogandlight.fsh
#include shadow.fsh
#include deferredfog.fsh

float GetCaustics(vec3 absWorldPos)
{
    vec3 nrm = texture(gNormal, texcoord).xyz;

    vec2 uvA = absWorldPos.xz / 4;

    uvA.x -= waterFlowCounter * 0.1;
    float caustic = texture(caustics, uvA).r;
    return caustic;
}

void main(void)
{
    float underwater = texture(gNormal, texcoord).w;
    
    if (underwater > 0.5) {
        discard;
    }

    float projectedZ = texture(gDepth, texcoord).r;
    vec4 screenPosition = vec4(vec3(texcoord, projectedZ) * 2.0 - 1.0, 1.0);
    screenPosition = invProjectionMatrix * screenPosition;
    screenPosition.xyz /= screenPosition.w;
    screenPosition.w = 1.0;
    vec4 worldPosition = invModelViewMatrix * screenPosition;

    vec3 absWorldPos = worldPosition.xyz + playerPos;
    
    float shadowBrightness = getShadowBrightnessAt(worldPosition) * 0.5 + 0.5;

    float caustic = GetCaustics(absWorldPos);
    
    float fog = 1.0 - getFogLevelDeferred(-screenPosition.z, fogMinIn, fogDensityIn, absWorldPos.y);
    outStrength = vec4((caustic * shadowBrightness * fog + 0.5) - 0.05 * fog);

    vec4 light = texture(gLight, texcoord);

    vec3 myColor = (light.w * 0.1) + light.rgb;

    outStrength.rgb *= myColor;
    
    outStrength.a = 1.0;

    //outStrength = vec4(absWorldPos, 0.0);
}