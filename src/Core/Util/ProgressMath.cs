namespace DivinityModManager.Util;

public static class ProgressMath
{
	public static double CalculatePhaseStep(int totalItems, int phasesPerItem) =>
		1d / ((double)Math.Max(1, totalItems) * Math.Max(1, phasesPerItem));

	public static double AddClamped(double currentValue, double increment) =>
		Math.Clamp(currentValue + increment, 0d, 1d);
}
