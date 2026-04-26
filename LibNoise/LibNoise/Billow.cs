namespace LibNoise;

public class Billow : GradientNoiseBasis, IModule
{
	private const int MaxOctaves = 30;

	private int mOctaveCount;

	public double Frequency { get; set; }

	public double Persistence { get; set; }

	public NoiseQuality NoiseQuality { get; set; }

	public int Seed { get; set; }

	public double Lacunarity { get; set; }

	public int OctaveCount
	{
		get
		{
			return mOctaveCount;
		}
		set
		{
			if (value < 1 || value > 30)
			{
				throw new ArgumentException("Octave count must be greater than zero and less than " + 30);
			}
			mOctaveCount = value;
		}
	}

	public Billow()
	{
		Frequency = 1.0;
		Lacunarity = 2.0;
		OctaveCount = 6;
		Persistence = 0.5;
		NoiseQuality = NoiseQuality.Standard;
		Seed = 0;
	}

	public double GetValue(double x, double y, double z)
	{
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 1.0;
		x *= Frequency;
		y *= Frequency;
		z *= Frequency;
		for (int i = 0; i < OctaveCount; i++)
		{
			long num4 = (Seed + i) & 0xFFFFFFFFu;
			num2 = GradientCoherentNoise(x, y, z, (int)num4, NoiseQuality);
			num2 = 2.0 * System.Math.Abs(num2) - 1.0;
			num += num2 * num3;
			x *= Lacunarity;
			y *= Lacunarity;
			z *= Lacunarity;
			num3 *= Persistence;
		}
		return num + 0.5;
	}
}
