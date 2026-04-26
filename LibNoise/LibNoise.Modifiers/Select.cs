namespace LibNoise.Modifiers;

public class Select : Math, IModule
{
	private double mEdgeFalloff;

	public IModule ControlModule { get; set; }

	public IModule SourceModule1 { get; set; }

	public IModule SourceModule2 { get; set; }

	public double UpperBound { get; private set; }

	public double LowerBound { get; private set; }

	public double EdgeFalloff
	{
		get
		{
			return mEdgeFalloff;
		}
		set
		{
			double num = UpperBound - LowerBound;
			mEdgeFalloff = ((value > num / 2.0) ? (num / 2.0) : value);
		}
	}

	public Select(IModule control, IModule source1, IModule source2)
	{
		if (control == null || source1 == null || source2 == null)
		{
			throw new ArgumentNullException("Control and source modules must be provided.");
		}
		ControlModule = control;
		SourceModule1 = source1;
		SourceModule2 = source2;
		EdgeFalloff = 0.0;
		LowerBound = -1.0;
		UpperBound = 1.0;
	}

	public double GetValue(double x, double y, double z)
	{
		if (ControlModule == null || SourceModule1 == null || SourceModule2 == null)
		{
			throw new NullReferenceException("Control and source modules must be provided.");
		}
		double value = ControlModule.GetValue(x, y, z);
		if (EdgeFalloff > 0.0)
		{
			if (value < LowerBound - EdgeFalloff)
			{
				return SourceModule1.GetValue(x, y, z);
			}
			if (value < LowerBound + EdgeFalloff)
			{
				double num = LowerBound - EdgeFalloff;
				double num2 = LowerBound + EdgeFalloff;
				double a = SCurve3((value - num) / (num2 - num));
				return LinearInterpolate(SourceModule1.GetValue(x, y, z), SourceModule2.GetValue(x, y, z), a);
			}
			if (value < UpperBound - EdgeFalloff)
			{
				return SourceModule2.GetValue(x, y, z);
			}
			if (value < UpperBound + EdgeFalloff)
			{
				double num3 = UpperBound - EdgeFalloff;
				double num4 = UpperBound + EdgeFalloff;
				double a = SCurve3((value - num3) / (num4 - num3));
				return LinearInterpolate(SourceModule2.GetValue(x, y, z), SourceModule1.GetValue(x, y, z), a);
			}
			return SourceModule1.GetValue(x, y, z);
		}
		if (value < LowerBound || value > UpperBound)
		{
			return SourceModule1.GetValue(x, y, z);
		}
		return SourceModule2.GetValue(x, y, z);
	}

	public void SetBounds(double lower, double upper)
	{
		if (lower > upper)
		{
			throw new ArgumentException("The lower bounds must be lower than the upper bounds.");
		}
		LowerBound = lower;
		UpperBound = upper;
		EdgeFalloff = mEdgeFalloff;
	}
}
