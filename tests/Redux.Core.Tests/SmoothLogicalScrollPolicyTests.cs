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
}
