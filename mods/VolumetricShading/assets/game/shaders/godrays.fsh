#version 330 core

uniform sampler2D inputTexture;
uniform sampler2D glowParts;

in vec2 texCoord;
in vec3 sunPosScreen;
in float iGlobalTime;
in float direction;
in vec3 color;

out vec4 outColor;


#include printvalues.fsh

vec4 applyVolumetricLighting(in vec2 uv) {
	float vgr = texture(glowParts, uv).g;
	
	vec3 vgrC = color*1.05*VOLUMETRIC_INTENSITY*vgr;
	return vec4(vgrC, 1.0);
}


void main(void) {
	//outColor = applyGodRays(texCoord, nSunPos);
	outColor = applyVolumetricLighting(texCoord);
	//outColor = texture(inputTexture, texCoord);
	outColor.a=1;
}