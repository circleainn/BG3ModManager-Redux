namespace DivinityModManager.Util;

/// <summary>
/// Keeps the experimental smooth-scroll input bounded while WPF continues to
/// own the authoritative, item-based scroll position.
/// </summary>
public static class SmoothLogicalScrollPolicy
{
	public const int WheelDeltaUnit = 120;
	public const int MaximumRowsPerEvent = 3;

	public static bool CanAnimate(
		bool isEnabled,
		bool reduceMotion,
		bool interactionSuppressed,
		bool systemAnimationsEnabled) =>
		isEnabled && !reduceMotion && !interactionSuppressed && systemAnimationsEnabled;

	/// <summary>
	/// Converts wheel input into a small, signed row count. Positive rows scroll
	/// upward, matching WPF's positive MouseWheelEventArgs.Delta direction.
	/// Sub-notch precision is retained, while unusually large bursts are capped.
	/// </summary>
	public static int ConsumeRows(ref int deltaRemainder, int delta, int configuredLines)
	{
		var combined = Math.Clamp((long)deltaRemainder + delta, int.MinValue, int.MaxValue);
		if (configuredLines == 0)
		{
			deltaRemainder = 0;
			return 0;
		}

		var direction = Math.Sign(combined);
		var magnitude = Math.Abs(combined);
		var completeNotches = magnitude / WheelDeltaUnit;
		deltaRemainder = direction * (int)(magnitude % WheelDeltaUnit);
		if (completeNotches == 0) return 0;

		// Windows uses -1 for page scrolling. A page-sized animated jump would
		// reveal virtualization churn, so treat it as the same safe three-row cap.
		var rowsPerNotch = configuredLines < 0
			? MaximumRowsPerEvent
			: Math.Clamp(configuredLines, 1, MaximumRowsPerEvent);
		var requestedRows = Math.Min(
			(long)MaximumRowsPerEvent,
			completeNotches * rowsPerNotch);
		return direction * (int)requestedRows;
	}
}
