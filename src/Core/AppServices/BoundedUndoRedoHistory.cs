namespace DivinityModManager.AppServices;

/// <summary>
/// Keeps a bounded, in-memory history of reversible working-state edits.
/// </summary>
public sealed class BoundedUndoRedoHistory<T>
{
	private sealed record Entry(T Before, T After);

	private readonly int _capacity;
	private readonly List<Entry> _undos = new();
	private readonly Stack<Entry> _redos = new();

	public bool CanUndo => _undos.Count > 0;
	public bool CanRedo => _redos.Count > 0;

	public BoundedUndoRedoHistory(int capacity = 50)
	{
		if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
		_capacity = capacity;
	}

	public void Record(T before, T after)
	{
		_undos.Add(new Entry(before, after));
		if (_undos.Count > _capacity) _undos.RemoveAt(0);
		_redos.Clear();
	}

	public bool TryUndo(out T state)
	{
		if (_undos.Count == 0)
		{
			state = default;
			return false;
		}

		var index = _undos.Count - 1;
		var entry = _undos[index];
		_undos.RemoveAt(index);
		_redos.Push(entry);
		state = entry.Before;
		return true;
	}

	public bool TryRedo(out T state)
	{
		if (_redos.Count == 0)
		{
			state = default;
			return false;
		}

		var entry = _redos.Pop();
		_undos.Add(entry);
		state = entry.After;
		return true;
	}

	public void Clear()
	{
		_undos.Clear();
		_redos.Clear();
	}
}
