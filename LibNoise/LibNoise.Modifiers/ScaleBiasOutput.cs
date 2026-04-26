namespace LibNoise.Modifiers;

public class ScaleBiasOutput : IModule
{
	public double Scale { get; set; }

	public double Bias { get; set; }

	public IModule SourceModule { get; set; }

	public ScaleBiasOutput(IModule sourceModule)
	{
        ArgumentNullException.ThrowIfNull(sourceModule);
        SourceModule = sourceModule;
		Bias = 0.0;
		Scale = 1.0;
	}

	public double GetValue(double x, double y, double z)
	{
		if (SourceModule == null)
		{
			throw new Exception("A source module must be provided.");
		}
		return SourceModule.GetValue(x, y, z) * Scale + Bias;
	}
}
