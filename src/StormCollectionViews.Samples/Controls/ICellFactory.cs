using UIKit;

namespace StormCollectionViews
{
	public interface ICellFactory
	{
		TCell GetOrCreate<TCell>() where TCell : UIView, new();

		void Recycle(UIView cell);
	}
}