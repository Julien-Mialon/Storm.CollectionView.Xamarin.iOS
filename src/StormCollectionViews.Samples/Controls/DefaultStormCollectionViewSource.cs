using System;
using System.Collections.Generic;
using UIKit;

namespace StormCollectionViews
{
	public class DefaultStormCollectionViewSource<TSource, TCell> : IStormCollectionViewSource
		where TCell : UIView, new()
	{
		private readonly IStormCollectionView _collectionView;
		private readonly Action<int, TSource, TCell> _bind;
		private List<TSource> _items;

		public List<TSource> Items
		{
			get => _items;
			set
			{
				if (_items == value)
				{
					return;
				}

				_items = value;
				_collectionView.DataChanged();
			}
		}

		public int Count => _items?.Count ?? 0;

		public DefaultStormCollectionViewSource(IStormCollectionView collectionView, Action<int, TSource, TCell> bind)
		{
			_collectionView = collectionView ?? throw new ArgumentNullException(nameof(collectionView));
			_bind = bind ?? throw new ArgumentNullException(nameof(bind));
		}
		
		public UIView GetCell(int index, ICellFactory factory)
		{
			TCell cell = factory.GetOrCreate<TCell>();

			_bind(index, Items[index], cell);

			return cell;
		}
	}
}