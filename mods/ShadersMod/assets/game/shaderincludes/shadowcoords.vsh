#if SHADOWQUALITY > 0
uniform float shadowRangeFar;
uniform mat4 toShadowMapSpaceMatrixFar;
out vec4 shadowCoordsFar;
#endif
#if SHADOWQUALITY > 1
uniform float shadowRangeNear;
uniform mat4 toShadowMapSpaceMatrixNear;
out vec4 shadowCoordsNear;
#endif

#if GODRAYS > 0
out vec4 shadowRayStart;
out vec4 shadowLightPos;
#endif

const float transitionDistance = 10.0;


void calcShadowMapCoords(mat4 modelviewMat, vec4 worldPos) {
	float nearSub = 0;
#if SHADOWQUALITY > 0
	float len = length(worldPos);
#endif

#if SHADOWQUALITY > 1
	// Near map
	shadowCoordsNear = toShadowMapSpaceMatrixNear * worldPos;
		
	float distanceNear = clamp(
		max(max(0, 0.03 - shadowCoordsNear.x) * 100, max(0, shadowCoordsNear.x - 0.97) * 100) +
		max(max(0, 0.03 - shadowCoordsNear.y) * 100, max(0, shadowCoordsNear.y - 0.97) * 100) +
		max(0, shadowCoordsNear.z - 0.98) * 100 +
		max(0, len / shadowRangeNear - 0.15) 
	, 0, 1);
	
	nearSub = shadowCoordsNear.w = clamp(1.0 - distanceNear, 0.0, 1.0);

#endif
#if SHADOWQUALITY > 0
	// Far map
	shadowCoordsFar = toShadowMapSpaceMatrixFar * worldPos;
	
	float distanceFar = clamp(
		max(max(0, 0.03 - shadowCoordsFar.x) * 10, max(0, shadowCoordsFar.x - 0.97) * 10) +
		max(max(0, 0.03 - shadowCoordsFar.y) * 10, max(0, shadowCoordsFar.y - 0.97) * 10) +
		max(0, shadowCoordsFar.z - 0.98) * 10 +
		max(0, len / shadowRangeFar - 0.15) 
	, 0, 1);

	distanceFar = distanceFar * 2 - 0.5;
	
	shadowCoordsFar.w = max(0, clamp(1.0 - distanceFar, 0.0, 1.0) - nearSub);
	
	//shadowCoordsFar.w = max(0, 1.0 - nearSub);

#endif
}

void prepareVolumetricLighting(vec3 lightPosition) {
#if GODRAYS > 0
	shadowRayStart = toShadowMapSpaceMatrixFar * vec4(0.0, 0.0, 0.0, 1.0);
	shadowLightPos = toShadowMapSpaceMatrixFar * vec4(lightPosition, 1.0);
#endif
}