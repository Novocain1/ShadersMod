#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertexPositionIn;
layout(location = 1) in vec2 uvIn;
// rgb = block light, a=sun light level
layout(location = 2) in vec4 rgbaLightIn;
// Bits 0-7: Glow level
// Bits 8-10: Z-Offset
// Bit 11: Wind waving yes/no
// Bit 12: Water waving yes/no
layout(location = 3) in int renderFlags;

layout(location = 4) in vec2 flowVector;

// Bit 0: Should animate yes/no
// Bit 1: Should texture fade yes/no
// Bits 8-15: x-Distance to upper left corner, where 255 = size of the block texture
// Bits 16-24: y-Distance to upper left corner, where 255 = size of the block texture
// Bit 25: Lava yes/no
// Bit 26: Weak foamy yes/no
// Bit 27: Weak Wavy yes/no
layout(location = 5) in int waterFlagsIn;

// Bits 0..7 = season map index
// Bits 8..11 = climate map index
// Bits 12 = Frostable bit
// Bits 13, 14, 15 = free \o/
// Bits 16-23 = temperature
// Bits 24-31 = rainfall
layout(location = 6) in int colormapData;


uniform float waterStillCounter;

uniform vec4 rgbaFogIn;
uniform vec3 rgbaAmbientIn;
uniform float fogDensityIn;
uniform float fogMinIn;
uniform vec3 origin;
uniform mat4 projectionMatrix;
uniform mat4 modelViewMatrix;
uniform vec2 blockTextureSize;

uniform vec3 playerViewVec;
uniform vec3 sunPosRel;

#if GODRAYS > 0
uniform vec3 lightPosition;
#endif

out vec2 flowVectorf;
out vec4 rgba;
out vec4 rgbaFog;
out float fogAmount;
out vec2 uv;
out vec2 uvSize;
out float waterStillCounterOff;
out vec3 fragWorldPos;
out vec3 fragNormal;
out vec3 fWorldPos;

flat out vec2 uvBase;
flat out int waterFlags;
flat out int skyExposed;

#include shadowcoords.vsh
#include fogandlight.vsh
#include vertexwarp.vsh
#include colormap.vsh


void main(void)
{
	vec4 worldPos = vec4(vertexPositionIn + origin, 1.0);
	
	float div = ((waterFlagsIn & (1<<27)) > 0) ? 90 : 10;
	float yBefore = worldPos.y;
	skyExposed = (renderFlags >> 13) & 1;
	
	worldPos = applyLiquidWarping((waterFlagsIn & 0x2000000) == 0, worldPos, div);
	
	vec4 cameraPos = modelViewMatrix * worldPos;
	
	gl_Position = projectionMatrix * cameraPos;
	
	float x = mod(waterStillCounter + length(worldPos.xz + playerpos.xz)/3, 2);

	waterStillCounterOff = smoothstep(0, 1, abs(x - 1));
	if ((waterFlagsIn & 2) == 0) {
		waterStillCounterOff = 1;
	}
	
	fragWorldPos = worldPos.xyz + playerpos;
	fWorldPos = worldPos.xyz;
	
	rgba = applyLight(rgbaAmbientIn, rgbaLightIn, renderFlags, cameraPos);
	rgbaFog = rgbaFogIn;
	fogAmount = getFogLevel(worldPos, fogMinIn, fogDensityIn);
	
	uv = uvIn;
	
	float w= ((waterFlagsIn >> 8) & 0xff) / 255.0 * blockTextureSize.x;
	float h = ((waterFlagsIn >> 16) & 0xff) / 255.0 * blockTextureSize.y;
	uvSize = vec2(w, h);
	uvBase = uv - uvSize;
	
	flowVectorf = flowVector;
	
	waterFlags = waterFlagsIn;
	fragNormal = unpackNormal(renderFlags >> 15);
	
	vec3 eyeFresnel = normalize(vec3(worldPos.x, worldPos.y - 3.5, worldPos.z));
	float bias = 0.01;
	float scale = 1.5;
	float power = 1.6;
	
	if ((renderFlags & 0xff) == 0) { // Don't apply to glowing liquids for now, looks weird on lava (makes it less glowy)
		float fresnel = bias + scale * pow(1 + dot(eyeFresnel, fragNormal), power);	
		rgba.a = min(fresnel, clamp(20 * (1.05 - length(worldPos.xz) / viewDistance) - 5, -1, 1.2));
		
		if (fragNormal.y < 0.5) {
			rgba.a /= 3;
		}
	}
	

	calcShadowMapCoords(modelViewMatrix, worldPos);
	calcColorMapUvs(colormapData, vec4(vertexPositionIn + origin, 1.0) + vec4(playerpos, 1), rgbaLightIn.a, false);
	
#if GODRAYS > 0
	prepareVolumetricLighting(lightPosition);
#endif
	
	// We pretend the decal is closer to the camera to enforce it always being drawn on top
	// Required e.g. when water is besides stairs or slabs
	gl_Position.w += 0.0008 / max(0.1, gl_Position.z);
}