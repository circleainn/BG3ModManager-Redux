using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DivinityModManager;

public static class DependencyObjectExtensions
{
	public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject depObj) where T : DependencyObject
	{
		if (depObj != null)
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
				if (child != null && child is T)
				{
					yield return (T)child;
				}

				foreach (T childOfChild in FindVisualChildren<T>(child))
				{
					yield return childOfChild;
				}
			}
		}
	}
	/// <summary>
	/// Returns only the item containers currently owned by an ItemsControl's realized
	/// panel. Virtualized mod lists contain complex row templates, so recursively walking
	/// every descendant to rediscover their top-level containers is unnecessarily costly.
	/// </summary>
	public static IReadOnlyList<T> GetRealizedItemContainers<T>(this ItemsControl owner)
		where T : DependencyObject
	{
		if (owner == null) return Array.Empty<T>();
		var itemsHost = owner.FindVisualChildren<Panel>()
			.FirstOrDefault(panel => ReferenceEquals(ItemsControl.GetItemsOwner(panel), owner));
		if (itemsHost != null) return itemsHost.Children.OfType<T>().ToList();

		return owner.FindVisualChildren<T>()
			.Where(container => ItemsControl.ItemsControlFromItemContainer(container) == owner)
			.ToList();
	}

	public static T FindVisualParent<T>(this DependencyObject depObj) where T : DependencyObject
	{
		if (depObj != null)
		{
			//get parent item
			DependencyObject parentObject = VisualTreeHelper.GetParent(depObj);

			//we've reached the end of the tree
			if (parentObject == null) return null;

			//check if the parent matches the type we're looking for
			T parent = parentObject as T;
			if (parent != null)
				return parent;
			else
				return FindVisualParent<T>(parentObject);
		}
		return null;
	}
}
