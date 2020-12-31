vec2 droplethash3(vec2 p)
{
    vec2 q = vec2(dot(p,vec2(127.1,311.7)), dot(p,vec2(269.5,183.3)));
	return fract(sin(q)*43758.5453);
}

float dropletnoise(in vec2 x, float dropletIntensity, float waterWaveCounter)
{
	if (dropletIntensity < 0.001) return 0.;
	
    x *= dropletIntensity;
    
    vec2 p = floor(x);
    vec2 f = fract(x);
    
		
	float va = 0.0;
    for( int j=-1; j<=1; j++ )
    for( int i=-1; i<=1; i++ )
    {
        vec2 g = vec2(float(i), float(j));
		vec2 o = droplethash3(p + g);
		vec2 r = ((g - f) + o.xy) / dropletIntensity;
		float d = sqrt(dot(r,r));
        
        float a = max(cos(d - waterWaveCounter * 2.7 + (o.x + o.y) * 5.0), 0.);
        a = smoothstep(0.99, 0.999, a);
        
	    float ripple = mix(a, 0., d);
        va += max(ripple, 0.);
    }
	
    return va;
}

vec3 ghash(vec3 p)
{
	p = vec3( dot(p,vec3(127.1,311.7, 74.7)),
			  dot(p,vec3(269.5,183.3,246.1)),
			  dot(p,vec3(113.5,271.9,124.6)));

	return -1.0 + 2.0*fract(sin(p)*43758.5453123);
}

float gnoise(in vec3 p)
{
    vec3 i = floor( p );
    vec3 f = fract( p );
	
	vec3 u = f*f*(3.0-2.0*f);

    return 0.7 * mix( mix( mix( dot( ghash( i + vec3(0.0,0.0,0.0) ), f - vec3(0.0,0.0,0.0) ), 
                          dot( ghash( i + vec3(1.0,0.0,0.0) ), f - vec3(1.0,0.0,0.0) ), u.x),
                     mix( dot( ghash( i + vec3(0.0,1.0,0.0) ), f - vec3(0.0,1.0,0.0) ), 
                          dot( ghash( i + vec3(1.0,1.0,0.0) ), f - vec3(1.0,1.0,0.0) ), u.x), u.y),
                mix( mix( dot( ghash( i + vec3(0.0,0.0,1.0) ), f - vec3(0.0,0.0,1.0) ), 
                          dot( ghash( i + vec3(1.0,0.0,1.0) ), f - vec3(1.0,0.0,1.0) ), u.x),
                     mix( dot( ghash( i + vec3(0.0,1.0,1.0) ), f - vec3(0.0,1.0,1.0) ), 
                          dot( ghash( i + vec3(1.0,1.0,1.0) ), f - vec3(1.0,1.0,1.0) ), u.x), u.y), u.z );
}