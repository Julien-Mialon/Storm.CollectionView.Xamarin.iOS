using System;
using System.Collections.Generic;
using UIKit;

namespace StormCollectionViews
{
	public class DefaultCellFactory : ICellFactory
	{
		private readonly Dictionary<Type, Queue<UIView>> _cells = new Dictionary<Type, Queue<UIView>>();
		
		public TCell GetOrCreate<TCell>() where TCell : UIView, new()
		{
			if(_cells.TryGetValue(typeof(TCell), out var queue) && queue.Count > 0)
			{
				return (TCell)queue.Dequeue();
			}
			
			return new TCell();
		}

		public void Recycle(UIView cell)
		{
			if (!_cells.TryGetValue(cell.GetType(), out var queue))
			{
				_cells.Add(cell.GetType(), queue = new Queue<UIView>());
			}

			queue.Enqueue(cell);
		}
	}
}