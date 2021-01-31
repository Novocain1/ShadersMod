uniform float flatFogDensity;
uniform float flatFogStart;
uniform float viewDistance;
uniform float viewDistanceLod0;
uniform float zNear = 0.3;
uniform float zFar = 1500.0;
uniform float shadowIntensity = 1;
uniform vec3 lightPosition;

#if GODRAYS > 0
in vec4 shadowRayStart;
in vec4 shadowLightPos;
#endif

#if SHADOWQUALITY > 0
in float blockBrightness;
in vec4 shadowCoordsFar;
uniform sampler2DShadow shadowMapFar;
uniform float shadowMapWidthInv;
uniform float shadowMapHeightInv;
#endif

#if SHADOWQUALITY > 1
in vec4 shadowCoordsNear;
uniform sampler2DShadow shadowMapNear;
#endif

#define VOLUMETRIC_SSAO_DECLINE 0.5f

float linearDepth(float depthSample)
{
    depthSample = 2.0 * depthSample - 1.0;
    float zLinear = 2.0 * zNear / (zFar + zNear - depthSample * (zFar - zNear));
    return zLinear;
}

vec4 applyFog(vec4 rgbaPixel, float fogWeight) {
	return vec4(mix(rgbaPixel.rgb, rgbaFog.rgb, fogWeight), rgbaPixel.a);
}

float getBrightnessFromShadowMap() {
	#if SHADOWQUALITY > 0

	float totalFar = 0.0;
	if (shadowCoordsFar.z < 0.999 && shadowCoordsFar.w > 0) {
		for (int x = -1; x <= 1; x++) {
			for (int y = -1; y <= 1; y++) {
				float inlight = texture (shadowMapFar, vec3(shadowCoordsFar.xy + vec2(x * shadowMapWidthInv, y * shadowMapHeightInv), shadowCoordsFar.z - 0.0009 + (0.0001 * VSMOD_FARSHADOWOFFSET)));
				totalFar += 1 - inlight;
			}
		}
	}

	totalFar /= 9.0f;

	
	float b = 1.0 - shadowIntensity * totalFar * shadowCoordsFar.w * 0.5;
	#endif
	
	
	#if SHADOWQUALITY > 1
	float totalNear = 0.0;
	if (shadowCoordsNear.z < 0.999 && shadowCoordsNear.w > 0) {
		for (int x = -1; x <= 1; x++) {
			for (int y = -1; y <= 1; y++) {
				float inlight = texture (shadowMapNear, vec3(shadowCoordsNear.xy + vec2(x * shadowMapWidthInv, y * shadowMapHeightInv), shadowCoordsNear.z - 0.0005 + (0.0001 * VSMOD_NEARSHADOWOFFSET)));
				totalNear += 1 - inlight;
			}
		}
	}
	
	totalNear /= 9.0f;

	
	b -=  shadowIntensity * totalNear * shadowCoordsNear.w * 0.5;
	#endif
	
	#if SHADOWQUALITY > 0
	b = clamp(b + blockBrightness, 0, 1);
	return b;
	#endif
	
	return 1.0;
}


float getBrightnessFromNormal(vec3 normal, float normalShadeIntensity, float minNormalShade) {

	// Option 2: Completely hides peter panning, but makes semi sunfacing block sides pretty dark
	float nb = max(minNormalShade, 0.5 + 0.5 * dot(normal, lightPosition));
	
	// Let's also define that diffuse light from the sky provides an additional brightness boost for up facing stuff
	// because the top side of blocks being darker than the sides is uncanny o__O
	float factor = 0.95f;
	
	factor = clamp(factor - 0.35f * VSMOD_OVEREXPOSURE, 0f, 1f);
	
	nb = max(nb, dot(normalize(normal), vec3(0, 1, 0)) * factor);
	
	return mix(1, nb, normalShadeIntensity);
}



vec4 applyFogAndShadow(vec4 rgbaPixel, float fogWeight) {
	float b = getBrightnessFromShadowMap();
	rgbaPixel *= vec4(b, b, b, 1);

	return applyFog(rgbaPixel, fogWeight*1);
}

vec4 applyFogAndShadowWithNormal(vec4 rgbaPixel, float fogWeight, vec3 normal, float normalShadeIntensity, float minNormalShade) {
	float b = getBrightnessFromShadowMap();
	float nb = getBrightnessFromNormal(normal, normalShadeIntensity, minNormalShade);
	
	b = min(b, nb);
	rgbaPixel *= vec4(b, b, b, 1);
	
	return applyFog(rgbaPixel, fogWeight);
}

void applyOverexposure(inout vec4 rgbaPixel, float b, vec3 normal, vec3 worldPos, float fogDensity) {
#if VSMOD_OVEREXPOSURE_ENABLED > 0
	float dot = dot(normal, lightPosition);
	float orientation = dot > 0.05 ? 0.5 + 0.5 * dot : 0.5 * (clamp(dot - 0.025, 0, 0.025) / 0.025);

	float fDensity = max(fogDensity, flatFogDensity);
	if (fDensity < 0.01) {
		float densityModifier = clamp((0.01 - fDensity) * 100, 0, 1);
		float sunHeight = pow(min(max(lightPosition.y*2.5f, 0.0f), 1f), 1f);
		float playerDistance = length(worldPos);
		float distScaling = clamp((300 - playerDistance) / 300, 0, 1);

		float exposure = pow(b, 2) * (0.25 + 0.75 * orientation) * VSMOD_OVEREXPOSURE * sunHeight * distScaling * densityModifier;

		vec3 additional = rgbaPixel.rgb * vec3(1.2, 1.0, 0.7) * exposure;
		rgbaPixel.rgb += additional;
		//rgbaPixel.rgb = vec3(1.0f) - exp(-rgbaPixel.rgb * 1.3);
		rgbaPixel.rgb = min(vec3(1), rgbaPixel.rgb);
	}
#endif
}

vec4 applyOverexposedFogAndShadowFlat(vec4 rgbaPixel, float fogWeight, vec3 normal, vec3 worldPos, float fogDensity) {
	float b = getBrightnessFromShadowMap();
	rgbaPixel *= vec4(b, b, b, 1);
	
	applyOverexposure(rgbaPixel, b, normal, worldPos, fogDensity);

	return applyFog(rgbaPixel, fogWeight*1);
}

vec4 applyOverexposedFogAndShadow(vec4 rgbaPixel, float fogWeight, vec3 normal, float normalShadeIntensity,
	float minNormalShade, vec3 worldPos, float fogDensity) {

	float b = getBrightnessFromShadowMap();
	float nb = getBrightnessFromNormal(normal, normalShadeIntensity, minNormalShade);

	float outB = min(b, nb);
	rgbaPixel *= vec4(outB, outB, outB, 1);
	
	applyOverexposure(rgbaPixel, b, normal, worldPos, fogDensity);

	return applyFog(rgbaPixel, fogWeight);
}

float getFogLevel(float fogMin, float fogDensity, float worldPosY) {
	float depth = gl_FragCoord.z;
	float clampedDepth = min(250, depth);
	float heightDiff = worldPosY - flatFogStart;
	
	//float extraDistanceFog = max(-flatFogDensity * flatFogStart / (160 + heightDiff * 3), 0);   // heightDiff*3 seems to fix distant mountains being supper fogged on most flat fog values
	// ^ this breaks stuff. Also doesn't seem to be needed? Seems to work fine without
	
	float extraDistanceFog = max(-flatFogDensity * clampedDepth * (flatFogStart) / 60, 0); // div 60 was 160 before, at 160 thick flat fog looks broken when looking at trees

	float distanceFog = 1 - 1 / exp(clampedDepth * (fogDensity + extraDistanceFog));
	float flatFog = 1 - 1 / exp(heightDiff * flatFogDensity);
	
	float val = max(flatFog, distanceFog);
	float nearnessToPlayer = clamp((8-depth)/16, 0, 0.8);
	val = max(min(0.04, val), val - nearnessToPlayer);
	
	// Needs to be added after so that underwater fog still gets applied. 
	val += fogMin; 
	
	return clamp(val, 0, 1);
}

float calculateVolumetricScatter(vec3 position) {
#if GODRAYS > 0
	float dither = fract(0.75487765 * gl_FragCoord.x + 0.56984026 * gl_FragCoord.y);
	//float dither = 0;

	const int maxSamples = 6;
	
	vec3 dV = (shadowCoordsFar.xyz-shadowRayStart.xyz)/maxSamples;
	//vec4 shadowLightPosition = shadowMatrix * vec4(lightPosition, 1.0);
	vec3 lightDir = normalize(shadowLightPos.xyz-shadowRayStart.xyz);
	
	vec3 progress = shadowRayStart.xyz + dV*dither;
	
	float vL = 0.0f;
	
	for (int i = 0; i < maxSamples; ++i) {
		
		vL += texture(shadowMapFar, vec3(progress.xy, progress.z - 0.0009));
		progress += dV;
	}
	
	float normalOut = min(1, vL * length(position) / 1000.0f / maxSamples);
	float intensity = dot(normalize(dV), lightDir);
	float phase = 2.5+exp(intensity*3.0)/3.0;
	return min(0.9f, pow(phase * normalOut, VOLUMETRIC_FLATNESS));
#endif
	return 0.0f;
}