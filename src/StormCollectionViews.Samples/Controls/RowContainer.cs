using System;
using UIKit;

namespace StormCollectionViews
{
	internal class RowContainer
	{
		public UILayoutGuide Guide { get; }

		public UIView[] Cells { get; }

		public NSLayoutConstraint RowHeightConstraint { get; }

		public RowContainer(UILayoutGuide guide, UIView[] cells, NSLayoutConstraint rowHeightConstraint)
		{
			Guide = guide;
			Cells = cells;
			RowHeightConstraint = rowHeightConstraint;
		}

		public nfloat UpdateHeight()
		{
			nfloat height = 0;
			for (var i = 0; i < Cells.Length; i++)
			{
				nfloat cellHeight = Cells[i].Bounds.Height;
				if (cellHeight > height)
				{
					height = cellHeight;
				}
			}

			if (RowHeightConstraint.Constant != height)
			{
				RowHeightConstraint.Constant = height;
			}

			return height;
		}
	}
}