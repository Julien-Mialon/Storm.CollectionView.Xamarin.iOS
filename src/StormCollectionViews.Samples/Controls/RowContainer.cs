using UIKit;

namespace StormCollectionViews
{
	internal class RowContainer
	{
		public UILayoutGuide Guide { get; }

		public UIView Cell { get; }

		public RowContainer(UILayoutGuide guide, UIView cell)
		{
			Guide = guide;
			Cell = cell;
		}
	}
}