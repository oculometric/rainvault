float voronoi_hash(vec3 v)
{
    return fract(sin(dot(v, vec3(201.0f, 123.0f, 304.2f))) * 190493.02095f) * 2.0f - 1.0f;
}

// returns the distance metric for a given position in 3D voronoi nosie
float voronoi(vec3 position, float randomness, out vec3 containing_cell)
{
    vec3 cell = floor(position);
    float closest = 4.0f;
    vec3 closest_cell = vec3(0.0f);
	float max_rand = ceil(randomness);

    for (float z = cell.z - max_rand; z <= cell.z + max_rand; z += 1.0f)
    {
        for (float y = cell.y - max_rand; y <= cell.y + max_rand; y += 1.0f)
        {
            for (float x = cell.x - max_rand; x <= cell.x + max_rand; x += 1.0f)
            {
                vec3 test_cell = vec3(x, y, z);
                test_cell += vec3(voronoi_hash(vec3(x,y,z)), voronoi_hash(vec3(y,z,x)), voronoi_hash(vec3(z,x,y))) * randomness * 0.5f;

                float dist = length(position - test_cell);
                if (dist < closest)
                {
                    closest = dist;
                    closest_cell = vec3(x, y, z);
                }
            }
        }
    }

	containing_cell = closest_cell;
	return closest;
}

float fractal_voronoi(vec3 position, float randomness, int octaves, float lacunarity, float gain)
{
	vec3 cell;
	float value = 0.0f;

	float scale = 1.0f;
	float multiplier = 1.0f;
	float maximum = 0.0f;

	for (int i = 0; i < octaves; i++)
	{
		float dist = voronoi(position * scale, randomness, cell);
		maximum += multiplier;
		value += dist * multiplier;
		scale *= lacunarity;
		multiplier *= gain;
	}

	value /= maximum;

	return value;
}