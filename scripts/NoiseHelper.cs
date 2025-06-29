using Godot;
using System;

public partial class NoiseHelper
{
    public static Vector3 fract(Vector3 v)
    {
        return v - v.Floor();
    }

    public static Vector3 floor(Vector3 v)
    {
        return v.Floor();
    }

    public static float mix(float f1, float f2, float f)
    {
        return Mathf.Lerp(f1, f2, f);
    }

    public Vector3 sin(Vector3 v)
    {
        return new Vector3(Mathf.Sin(v.X), Mathf.Sin(v.Y), Mathf.Sin(v.Z));
    }

    public static float dot(Vector3 v1, Vector3 v2)
    {
        return v1.Dot(v2);
    }

    public static float fbm_random(Vector3 coord)
    {
        float f = Mathf.Sin(dot(coord, new Vector3(12.98f, 78.23f, 35.63f))) * 43758.5f;
        return f - Mathf.Floor(f);
    }

    public static float fbm_noise(Vector3 coord)
    {
        Vector3 flr = floor(coord);
        Vector3 frc = fract(coord);

        float tln = fbm_random(flr + new Vector3(0, 0, 0));
        float trn = fbm_random(flr + new Vector3(1, 0, 0));
        float bln = fbm_random(flr + new Vector3(0, 1, 0));
        float brn = fbm_random(flr + new Vector3(1, 1, 0));
        float tlf = fbm_random(flr + new Vector3(0, 0, 1));
        float trf = fbm_random(flr + new Vector3(1, 0, 1));
        float blf = fbm_random(flr + new Vector3(0, 1, 1));
        float brf = fbm_random(flr + new Vector3(1, 1, 1));

        Vector3 m = frc * frc * (new Vector3(3.0f, 3.0f, 3.0f) - (new Vector3(2.0f, 2.0f, 2.0f) * frc));

        float result =
        mix(
            mix(
                mix(tln, trn, m.X),
                mix(bln, brn, m.X),
                m.Y
            ),
            mix(
                mix(tlf, trf, m.X),
                mix(blf, brf, m.X),
                m.Y
            ),
            m.Z
        );

        return (result * 2.0f) - 1.0f;
    }

    public static float fbm(Vector3 _coord, int _octaves, float _lacunarity, float _gain)
    {
        float amplitude = 1.0f;
        float frequency = 1.0f;

        float max_amplitude = 0.0f;

        float v = 0.0f;

        for (int i = 0; i < _octaves; i++)
        {
            v += fbm_noise(_coord * frequency) * amplitude;
            frequency *= _lacunarity;
            max_amplitude += amplitude;
            amplitude *= _gain;
        }

        v /= max_amplitude;

        return v;
    }
}
