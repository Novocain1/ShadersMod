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

#if POISSON_SHADOWS > 0
vec2 poissonDisk[16] = vec2[]( 
   vec2( -0.94201624, -0.39906216 ), 
   vec2( 0.94558609, -0.76890725 ), 
   vec2( -0.094184101, -0.92938870 ), 
   vec2( 0.34495938, 0.29387760 ), 
   vec2( -0.91588581, 0.45771432 ), 
   vec2( -0.81544232, -0.87912464 ), 
   vec2( -0.38277543, 0.27676845 ), 
   vec2( 0.97484398, 0.75648379 ), 
   vec2( 0.44323325, -0.97511554 ), 
   vec2( 0.53742981, -0.47373420 ), 
   vec2( -0.26496911, -0.41893023 ), 
   vec2( 0.79197514, 0.19090188 ), 
   vec2( -0.24188840, 0.99706507 ), 
   vec2( -0.81409955, 0.91437590 ), 
   vec2( 0.19984126, 0.78641367 ), 
   vec2( 0.14383161, -0.14100790 ) 
);

float random(vec3 seed, int i){
	vec4 seed4 = vec4(seed,i);
	float dot_product = dot(seed4, vec4(12.9898,78.233,45.164,94.673));
	return fract(sin(dot_product) * 43758.5453);
}

const float poissonSpread = 3200.0;
#endif

float getBrightnessFromShadowMap() {
	#if SHADOWQUALITY > 0
	#if POISSON_SHADOWS > 0
	const float divFactor = 4.0;
	#else
	const float divFactor = 9.0;
	#endif

	float totalFar = 0.0;
	if (shadowCoordsFar.z < 0.999 && shadowCoordsFar.w > 0) {
		#if POISSON_SHADOWS == 0

		for (int x = -1; x <= 1; x++) {
			for (int y = -1; y <= 1; y++) {
				float inlight = texture (shadowMapFar, vec3(shadowCoordsFar.xy + vec2(x * shadowMapWidthInv, y * shadowMapHeightInv), shadowCoordsFar.z - 0.0009));
				totalFar += 1 - inlight;
			}
		}
		
		#else

		for (int i = 0; i < 4; ++i) {
			int index = i;
			float inlight = texture(shadowMapFar, vec3(shadowCoordsFar.xy + poissonDisk[index]/poissonSpread, shadowCoordsFar.z - 0.0009));
			totalFar += 1 - inlight;
		}

		#endif
	}

	totalFar /= divFactor;

	
	float b = 1.0 - shadowIntensity * totalFar * shadowCoordsFar.w * 0.5;
	#endif
	
	
	#if SHADOWQUALITY > 1
	float totalNear = 0.0;
	if (shadowCoordsNear.z < 0.999 && shadowCoordsNear.w > 0) {
		#if POISSON_SHADOWS == 0

		for (int x = -1; x <= 1; x++) {
			for (int y = -1; y <= 1; y++) {
				float inlight = texture (shadowMapNear, vec3(shadowCoordsNear.xy + vec2(x * shadowMapWidthInv, y * shadowMapHeightInv), shadowCoordsNear.z - 0.0005));
				totalNear += 1 - inlight;
			}
		}

		#else

		for (int i = 0; i < 4; ++i) {
			int index = i;
			float inlight = texture(shadowMapNear, vec3(shadowCoordsNear.xy + poissonDisk[index]/poissonSpread, shadowCoordsNear.z - 0.0005));
			totalNear += 1 - inlight;
		}

		#endif
	}
	
	totalNear /= divFactor;

	
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
	nb = max(nb, dot(normalize(normal), vec3(0, 1, 0)) * 0.6);
	
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
	
	return applyFog(rgbaPixel, fogWeight*1);
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

	int maxSamples = 6;
	
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