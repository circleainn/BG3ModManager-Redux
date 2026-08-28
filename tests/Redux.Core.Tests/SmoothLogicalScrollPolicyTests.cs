using DivinityModManager.Util;

namespace Redux.Core.Tests;

public sealed class SmoothLogicalScrollPolicyTests
{
	public void PartialWheelDeltasAccumulateWithoutPrematureScrolling()
	{
		var remainder = 0;

		RegressionAssert.Equal(0, SmoothLogicalScrollPolicy.ConsumeRows(ref remainder, 40, 3));
		RegressionAssert.Equal(40, remainder);
		RegressionAssert.Equal(0, SmoothLogicalScrollPolicy.ConsumeRows(ref remainder, 40, 3));
		RegressionAssert.Equal(80, remainder);
		RegressionAssert.Equal(3, SmoothLogicalScrollPolicy.ConsumeRows(ref remainder, 40, 3));
		RegressionAssert.Equal(0, remainder);
	}

	public void LargeWheelBurstsStayWithinTheAnimationSafetyCap()
	{
		var remainder = 0;

		RegressionAssert.Equal(-3, SmoothLogicalScrollPolicy.ConsumeRows(ref remainder, -960, 20));
		RegressionAssert.Equal(0, remainder);
		RegressionAssert.Equal(3, SmoothLogicalScrollPolicy.ConsumeRows(ref remainder, 120, -1));
	}

	public void SmoothScrollingIsStandardUnlessMotionOrInteractionSuppressesIt()
	{
		RegressionAssert.True(SmoothLogicalScrollPolicy.CanAnimate(false, false, true));
		RegressionAssert.False(SmoothLogicalScrollPolicy.CanAnimate(true, false, true));
		RegressionAssert.False(SmoothLogicalScrollPolicy.CanAnimate(false, true, true));
		RegressionAssert.False(SmoothLogicalScrollPolicy.CanAnimate(false, false, false));
	}

	public void MixedHeightRowsProduceDirectionCorrectCompensation()
	{
		RegressionAssert.Equal(
			120d,
			SmoothLogicalScrollPolicy.CalculateVisualCompensation(-3, [28d, 40d, 52d], 0, 36d));
		RegressionAssert.Equal(
			-120d,
			SmoothLogicalScrollPolicy.CalculateVisualCompensation(3, [28d, 40d, 52d], 0, 36d));
	}

	public void MissingCachedRowsUseAStableFallbackWithoutChangingDirection()
	{
		RegressionAssert.Equal(
			-66d,
			SmoothLogicalScrollPolicy.CalculateVisualCompensation(2, [30d], 1, 36d));
		RegressionAssert.Equal(
			72d,
			SmoothLogicalScrollPolicy.CalculateVisualCompensation(-2, [], 2, double.NaN));
	}

	public void ScrollRangeIsKnownBeforeDeferredLayoutPublishesTheNewOffset()
	{
		RegressionAssert.Equal(0, SmoothLogicalScrollPolicy.ConstrainRowsToScrollableRange(3, 0, 100));
		RegressionAssert.Equal(-3, SmoothLogicalScrollPolicy.ConstrainRowsToScrollableRange(-3, 0, 100));
		RegressionAssert.Equal(2, SmoothLogicalScrollPolicy.ConstrainRowsToScrollableRange(3, 2, 100));
		RegressionAssert.Equal(-1, SmoothLogicalScrollPolicy.ConstrainRowsToScrollableRange(-3, 99, 100));
		RegressionAssert.Equal(0, SmoothLogicalScrollPolicy.ConstrainRowsToScrollableRange(-3, 100, 100));
	}
}
