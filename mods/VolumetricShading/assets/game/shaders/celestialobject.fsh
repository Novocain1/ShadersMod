#version 330 core

in vec2 uv;
in vec4 color;
in vec4 rgbaFog;
in float glowLevel;
in vec3 vertexPosition;

layout(location = 0) out vec4 outColor;
layout(location = 1) out vec4 outGlow;
#if SSAOLEVEL > 0
in vec4 fragPosition;
in vec4 gnormal;
layout(location = 2) out vec4 outGNormal;
layout(location = 3) out vec4 outGPosition;
#endif


uniform sampler2D tex;
uniform float extraGodray = 0;
uniform float alphaTest = 0.001;
uniform float fogDensityIn;
uniform float fogMinIn;
uniform float horizonFog;
uniform vec3 sunPosition;
uniform int weirdMathToMakeMoonLookNicer;
uniform float dayLight;

#include dither.fsh
#include fogandlight.fsh
#include skycolor.fsh

void main () {
	vec4 texColor = applyFog(texture(tex, uv) * color, 0);
	if (texColor.a < alphaTest) discard;
	vec4 texGlow = vec4(glowLevel, dayLight*0.2f, 0, texColor.a);
	
	vec4 skyColor = vec4(1);
	vec4 skyGlow = vec4(1);
	float sealevelOffsetFactor = 0.06;
	
	getSkyColorAt(vertexPosition, sunPosition, sealevelOffsetFactor, clamp(dayLight, 0, 1), horizonFog, skyColor, skyGlow);

	vec3 skyPosNorm = normalize(vertexPosition.xyz);
	float fogAmount =getFogAmountForSky(vertexPosition, skyPosNorm, sealevelOffsetFactor, horizonFog);
	texColor = applyFog(texColor, fogAmount);

	outColor = texColor;
	outColor.a = texColor.a;

	

	if (weirdMathToMakeMoonLookNicer > 0) {
		float b = pow((outColor.r+outColor.g+outColor.b) / 2.8, 1.4);
		outColor.rgb *= b;

		float v = max(0, (1-texColor.a) * min(1, texColor.a*20)/3 - fogAmount/2);
		outGlow = vec4(v, v, 0, max(0, 4-5*dayLight));
		
		outColor.rgb = max(outColor.rgb, skyColor.rgb);
	} else {
		
		outGlow = max(texGlow, skyGlow);
		
		outColor.rgb = max(texColor.rgb, skyColor.rgb);
	}

#if SSAOLEVEL > 0
	outGPosition = vec4(fragPosition.xyz, fogAmount + glowLevel);
	outGNormal = vec4(gnormal.xyz, 0);
#endif

}
