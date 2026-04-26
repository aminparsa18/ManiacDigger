namespace LibNoise;

public class RidgedMultifractal : GradientNoiseBasis, IModule
{
	private const int MaxOctaves = 30;

	private int mOctaveCount;

	private double mLacunarity;

	private double[] SpectralWeights = new double[30];

	public double Frequency { get; set; }

	public NoiseQuality NoiseQuality { get; set; }

	public int Seed { get; set; }

	public double Lacunarity
	{
		get
		{
			return mLacunarity;
		}
		set
		{
			mLacunarity = value;
			CalculateSpectralWeights();
		}
	}

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

	public RidgedMultifractal()
	{
		Frequency = 1.0;
		Lacunarity = 2.0;
		OctaveCount = 6;
		NoiseQuality = NoiseQuality.Standard;
		Seed = 0;
	}

	public double GetValue(double x, double y, double z)
	{
		x *= Frequency;
		y *= Frequency;
		z *= Frequency;
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 1.0;
		double num4 = 1.0;
		double num5 = 2.0;
		for (int i = 0; i < OctaveCount; i++)
		{
			long num6 = (Seed + i) & 0x7FFFFFFF;
			num = GradientCoherentNoise(x, y, z, (int)num6, NoiseQuality);
			num = System.Math.Abs(num);
			num = num4 - num;
			num *= num;
			num *= num3;
			num3 = num * num5;
			if (num3 > 1.0)
			{
				num3 = 1.0;
			}
			if (num3 < 0.0)
			{
				num3 = 0.0;
			}
			num2 += num * SpectralWeights[i];
			x *= Lacunarity;
			y *= Lacunarity;
			z *= Lacunarity;
		}
		return num2 * 1.25 - 1.0;
	}

	private void CalculateSpectralWeights()
	{
		double num = 1.0;
		double num2 = 1.0;
		for (int i = 0; i < 30; i++)
		{
			SpectralWeights[i] = System.Math.Pow(num2, 0.0 - num);
			num2 *= mLacunarity;
		}
	}
}
