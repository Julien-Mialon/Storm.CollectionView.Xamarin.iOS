using UIKit;

namespace StormCollectionViews
{
	public interface IStormCollectionViewSource
	{
		int Count { get; }

		UIView GetCell(int index, ICellFactory factory);
	}
}