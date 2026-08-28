namespace DivinityModManager.Util;

/// <summary>
/// Keeps smooth-scroll input bounded while WPF continues to
/// own the authoritative, item-based scroll position.
/// </summary>
public static class SmoothLogicalScrollPolicy
{
	public const int WheelDeltaUnit = 120;
	public const int MaximumRowsPerEvent = 3;

	public static bool CanAnimate(
		bool reduceMotion,
		bool interactionSuppressed,
		bool systemAnimationsEnabled) =>
		!reduceMotion && !interactionSuppressed && systemAnimationsEnabled;

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

	/// <summary>
	/// Returns the render translation that visually bridges an item-based scroll.
	/// Downward scrolling moves the newly laid-out panel down over its former position;
	/// upward scrolling uses the inverse translation. Missing cached rows use the
	/// caller's representative height without forcing a synchronous layout pass.
	/// </summary>
	public static double CalculateVisualCompensation(
		int rows,
		IReadOnlyList<double> measuredHeights,
		int missingCount,
		double fallbackHeight)
	{
		if (rows == 0) return 0;

		var safeFallback = double.IsFinite(fallbackHeight) && fallbackHeight > 0
			? fallbackHeight
			: 36d;
		var travel = Math.Max(0, missingCount) * safeFallback;
		if (measuredHeights != null)
		{
			foreach (var height in measuredHeights)
				travel += double.IsFinite(height) && height > 0 ? height : safeFallback;
		}
		return -Math.Sign(rows) * travel;
	}

	/// <summary>
	/// Limits a signed logical-row request to the rows available above or below
	/// the current viewport. This can be evaluated before WPF's deferred layout
	/// publishes the new vertical offset.
	/// </summary>
	public static int ConstrainRowsToScrollableRange(
		int rows,
		double verticalOffset,
		double scrollableHeight)
	{
		if (rows == 0 ||
			!double.IsFinite(verticalOffset) ||
			!double.IsFinite(scrollableHeight) ||
			scrollableHeight <= 0) return 0;

		var safeOffset = Math.Clamp(verticalOffset, 0, scrollableHeight);
		var available = rows > 0
			? safeOffset
			: scrollableHeight - safeOffset;
		var availableRows = (int)Math.Min(
			int.MaxValue,
			Math.Ceiling(Math.Max(0, available) - 0.0001d));
		return Math.Sign(rows) * Math.Min(Math.Abs(rows), availableRows);
	}
}
