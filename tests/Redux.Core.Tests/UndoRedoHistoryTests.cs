using DivinityModManager.AppServices;

namespace Redux.Core.Tests;

internal sealed class UndoRedoHistoryTests
{
	public void UndoAndRedoRestoreTheExpectedState()
	{
		var history = new BoundedUndoRedoHistory<string>();
		history.Record("before", "after");

		RegressionAssert.True(history.TryUndo(out var undone));
		RegressionAssert.Equal("before", undone);
		RegressionAssert.True(history.TryRedo(out var redone));
		RegressionAssert.Equal("after", redone);
	}

	public void ANewEditClearsTheRedoBranch()
	{
		var history = new BoundedUndoRedoHistory<int>();
		history.Record(0, 1);
		RegressionAssert.True(history.TryUndo(out _));

		history.Record(0, 2);

		RegressionAssert.False(history.CanRedo);
		RegressionAssert.True(history.TryUndo(out var state));
		RegressionAssert.Equal(0, state);
	}

	public void HistoryDropsItsOldestEntryAtCapacity()
	{
		var history = new BoundedUndoRedoHistory<int>(2);
		history.Record(0, 1);
		history.Record(1, 2);
		history.Record(2, 3);

		RegressionAssert.True(history.TryUndo(out var second));
		RegressionAssert.Equal(2, second);
		RegressionAssert.True(history.TryUndo(out var first));
		RegressionAssert.Equal(1, first);
		RegressionAssert.False(history.TryUndo(out _));
	}
}
