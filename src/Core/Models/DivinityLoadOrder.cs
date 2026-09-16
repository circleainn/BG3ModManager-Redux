using DynamicData;

using System.Runtime.Serialization;

namespace DivinityModManager.Models;

[DataContract]
public class DivinityLoadOrderEntry
{
	[DataMember]
	public string UUID { get; set; }

	[DataMember]
	public string Name { get; set; }
	public bool Missing { get; set; }

	public DivinityLoadOrderEntry Clone()
	{
		return new DivinityLoadOrderEntry() { Name = this.Name, UUID = this.UUID, Missing = this.Missing };
	}
}

[DataContract]
public class DivinityLoadOrder : ReactiveObject
{
	private string _lastName;

	[Reactive] public string Name { get; set; }
	[Reactive] public string FilePath { get; set; }
	[Reactive] public DateTime LastModifiedDate { get; set; }

	[Reactive] public bool IsModSettings { get; set; }

	/// <summary>
	/// This is an order from a non-standard order file (info .json, .txt, .tsv).
	/// </summary>
	[Reactive] public bool IsDecipheredOrder { get; set; }

	private readonly ObservableAsPropertyHelper<string> _lastModified;

	public string LastModified => _lastModified.Value;

	[DataMember]
	public List<DivinityLoadOrderEntry> Order { get; set; } = new List<DivinityLoadOrderEntry>();

	/// <summary>
	/// Redux presentation data for the active side of this saved order. A null value
	/// identifies an order written before separators became order-specific.
	/// </summary>
	[DataMember(EmitDefaultValue = false)]
	public List<ModListVisualDividerData> VisualDividers { get; set; }

	public void Add(DivinityModData mod, bool force = false)
	{
		try
		{
			if (Order != null && mod != null)
			{
				if (force)
				{
					Order.Add(mod.ToOrderEntry());
				}
				else
				{
					if (Order.Count > 0)
					{
						bool alreadyInOrder = false;
						foreach (var x in Order)
						{
							if (x != null && x.UUID == mod.UUID)
							{
								alreadyInOrder = true;
								break;
							}
						}
						if (!alreadyInOrder)
						{
							Order.Add(mod.ToOrderEntry());
						}
					}
					else
					{
						Order.Add(mod.ToOrderEntry());
					}
				}
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error adding mod to order:\n{ex}");
		}
	}

	public void Add(DivinityProfileActiveModData mod, bool force = false, bool isMissing = false)
	{
		try
		{
			if (Order != null && mod != null)
			{
				if (force)
				{
					Order.Add(new DivinityLoadOrderEntry()
					{
						UUID = mod.UUID,
						Name = mod.Name,
						Missing = isMissing
					});
				}
				else
				{
					if (Order.Count > 0)
					{
						bool alreadyInOrder = false;
						foreach (var x in Order)
						{
							if (x != null && x.UUID == mod.UUID)
							{
								alreadyInOrder = true;
								break;
							}
						}
						if (!alreadyInOrder)
						{
							Order.Add(new DivinityLoadOrderEntry()
							{
								UUID = mod.UUID,
								Name = mod.Name,
								Missing = isMissing
							});
						}
					}
					else
					{
						Order.Add(new DivinityLoadOrderEntry()
						{
							UUID = mod.UUID,
							Name = mod.Name,
							Missing = isMissing
						});
					}
				}
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error adding mod to order:\n{ex}");
		}
	}

	public void Add(IDivinityModData mod, bool force = false)
	{
		try
		{
			if (Order != null && mod != null)
			{
				if (force)
				{
					Order.Add(new DivinityLoadOrderEntry
					{
						UUID = mod.UUID,
						Name = mod.Name,
					});
				}
				else
				{
					if (Order.Count > 0)
					{
						bool alreadyInOrder = false;
						foreach (var x in Order)
						{
							if (x != null && x.UUID == mod.UUID)
							{
								alreadyInOrder = true;
								break;
							}
						}
						if (!alreadyInOrder)
						{
							Order.Add(new DivinityLoadOrderEntry
							{
								UUID = mod.UUID,
								Name = mod.Name,
							});
						}
					}
					else
					{
						Order.Add(new DivinityLoadOrderEntry
						{
							UUID = mod.UUID,
							Name = mod.Name,
						});
					}
				}
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error adding mod to order:\n{ex}");
		}
	}

	public void AddRange(IEnumerable<DivinityModData> mods, bool replace = false)
	{
		foreach (var mod in mods)
		{
			Add(mod, replace);
		}
	}

	public void AddRange(IEnumerable<IDivinityModData> mods, bool replace = false)
	{
		foreach (var mod in mods)
		{
			Add(mod, replace);
		}
	}

	public void Remove(DivinityModData mod)
	{
		try
		{
			if (Order != null && Order.Count > 0 && mod != null)
			{
				DivinityLoadOrderEntry entry = null;
				foreach (var x in Order)
				{
					if (x != null && x.UUID == mod.UUID)
					{
						entry = x;
						break;
					}
				}
				if (entry != null) Order.Remove(entry);
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error removing mod from order:\n{ex}");
		}
	}

	public void Update(DivinityModData mod)
	{
		try
		{
			if (Order != null && Order.Count > 0)
			{
				var existing = Order.FirstOrDefault(x => x.UUID == mod.UUID);
				if (existing != null)
				{
					existing.UUID = mod.UUID;
					existing.Name = mod.Name;
					existing.Missing = false;
				}
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error removing mod from order:\n{ex}");
		}
	}

	public void RemoveRange(IEnumerable<DivinityModData> mods)
	{
		if (Order.Count > 0 && mods != null)
		{
			foreach (var mod in mods)
			{
				Remove(mod);
			}
		}
	}

	public void Sort(Comparison<DivinityLoadOrderEntry> comparison)
	{
		try
		{
			if (Order.Count > 1)
			{
				Order.Sort(comparison);
			}
		}
		catch (Exception ex)
		{
			DivinityApp.Log($"Error sorting order:\n{ex}");
		}
	}

	public void SetOrder(IEnumerable<DivinityLoadOrderEntry> nextOrder)
	{
		Order.Clear();
		Order.AddRange(nextOrder);
	}

	public void SetOrder(DivinityLoadOrder nextOrder)
	{
		Order.Clear();
		Order.AddRange(nextOrder.Order);
	}

	private static List<ModListVisualDividerData> CloneVisualDividers(
		IEnumerable<ModListVisualDividerData> dividers) => dividers?.Select(divider => new ModListVisualDividerData
		{
			Id = divider.Id,
			Title = divider.Title,
			Color = divider.Color,
			IconId = divider.IconId,
			Description = divider.Description,
			IsActiveList = true,
			Position = divider.Position,
			IsCollapsed = divider.IsCollapsed,
			HideLine = divider.HideLine,
			IsGlobal = divider.IsGlobal,
			MemberModUuids = divider.MemberModUuids?.ToList()
		}).ToList();

	public bool OrderEquals(IEnumerable<string> orderList)
	{
		if (Order.Count > 0)
		{
			return Order.Select(x => x.UUID).SequenceEqual(orderList);
		}
		return false;
	}

	public DivinityLoadOrder Clone()
	{
		return new DivinityLoadOrder()
		{
			Name = this.Name,
			Order = this.Order.ToList(),
			VisualDividers = CloneVisualDividers(this.VisualDividers),
			LastModifiedDate = this.LastModifiedDate
		};
	}

	public DivinityLoadOrder()
	{
		this.WhenAnyValue(x => x.Name, (name) => !String.IsNullOrEmpty(name) && name != _lastName).Subscribe(_ =>
		{
			DivinityApp.Events.OnOrderNameChanged(_lastName, Name);
			_lastName = Name;
		});
		_lastModified = this.WhenAnyValue(x => x.LastModifiedDate).Select(x => x.ToString("g")).ToProperty(this, nameof(LastModified));
	}
}
