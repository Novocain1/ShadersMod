#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertexPositionIn;
layout(location = 1) in vec2 uvIn;
// rgb = block light, a=sun light level
layout(location = 2) in vec4 rgbaLightIn;
layout(location = 3) in int renderFlagsIn;

// Bits 0..7 = season map index
// Bits 8..11 = climate map index
// Bits 12 = Frostable bit
// Bits 13, 14, 15 = free \o/
// Bits 16-23 = temperature
// Bits 24-31 = rainfall
layout(location = 4) in int colormapData;


#if GODRAYS > 0
uniform vec3 lightPosition;
#endif

uniform vec4 rgbaFogIn;
uniform vec3 rgbaAmbientIn;
uniform float fogDensityIn;
uniform float fogMinIn;
uniform vec3 origin;
uniform mat4 projectionMatrix;
uniform mat4 modelViewMatrix;

out vec4 rgba;
out vec4 rgbaFog;
out float fogAmount;
out vec2 uv;
out vec4 worldPos;
out vec3 vertexPos;

flat out int renderFlags;
flat out vec3 normal;
out float normalShadeIntensity;


#include shadowcoords.vsh
#include fogandlight.vsh
#include vertexwarp.vsh
#include colormap.vsh

void main(void)
{
	worldPos = vec4(vertexPositionIn + origin, 1.0);
	
	worldPos = applyVertexWarping(renderFlagsIn, worldPos);

	vec4 cameraPos = modelViewMatrix * worldPos;
	
	gl_Position = projectionMatrix * cameraPos;

	calcShadowMapCoords(modelViewMatrix, worldPos);
	calcColorMapUvs(colormapData, vec4(vertexPositionIn + origin, 1.0) + vec4(playerpos, 1), rgbaLightIn.a, false);
	
	fogAmount = getFogLevel(worldPos, fogMinIn, fogDensityIn);
	uv = uvIn;
	
	rgba = applyLight(rgbaAmbientIn, rgbaLightIn, renderFlagsIn, cameraPos);
	rgba.a = clamp(20 * (1.10 - length(worldPos.xz) / viewDistance) - 5, -1, 1);
	
	rgbaFog = rgbaFogIn;
	
	// Lower 8 bit is glow level
	renderFlags = renderFlagsIn >> 8;  
	
	// Now the lowest 3 bits are used as an unsigned number 
	// to fix Z-Fighting on blocks over certain other blocks. 
	if (renderFlags > 0 && gl_Position.z > 0) {
		gl_Position.w += (renderFlags & 7) * 0.00025 / max(0.1, gl_Position.z);
	}
	
	normal = unpackNormal(renderFlags >> 7);
	normalShadeIntensity = min(1, rgbaLightIn.a * 1.5);

#if GODRAYS > 0
	prepareVolumetricLighting(lightPosition);
#endif
}