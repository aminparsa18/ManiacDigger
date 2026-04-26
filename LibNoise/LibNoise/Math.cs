namespace LibNoise;

public class Math
{
	public static readonly double PI = System.Math.PI;

	public static readonly double Sqrt2 = 1.4142135623730951;

	public static readonly double Sqrt3 = 1.7320508075688772;

	public static readonly double DEG_TO_RAD = PI / 180.0;

	public static double GetSmaller(double a, double b)
	{
		if (!(a < b))
		{
			return b;
		}
		return a;
	}

	protected double LinearInterpolate(double n0, double n1, double a)
	{
		return (1.0 - a) * n0 + a * n1;
	}

	protected double SCurve3(double a)
	{
		return a * a * (3.0 - 2.0 * a);
	}

	protected double SCurve5(double a)
	{
		double num = a * a * a;
		double num2 = num * a;
		double num3 = num2 * a;
		return 6.0 * num3 - 15.0 * num2 + 10.0 * num;
	}
}