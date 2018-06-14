using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using UIKit;

namespace StormCollectionViews
{
	public class StormCollectionView : UIScrollView, IStormCollectionView
	{
		private const string SELECTOR_BOUNDS_SIZE = "bounds";
		private readonly int _estimatedHeight;
		private readonly int _columnCount;
		private ICellFactory _factory;
		private LayoutGuideFactory _guideFactory = new LayoutGuideFactory();
		private IStormCollectionViewSource _source;

		private UIEdgeInsets _contentInset;

		private UIView _scrollContent;
		private UILayoutGuide _container;

		private NSLayoutConstraint _topContainerConstraint;
		private NSLayoutConstraint _bottomContainerConstraint;
		private NSLayoutConstraint _leftContainerConstraint;
		private NSLayoutConstraint _rightContainerConstraint;

		private NSLayoutConstraint _firstItemConstraint;
		private NSLayoutConstraint _lastItemConstraint;

		private nfloat[] _sizes;
		private int _minimalDisplayedIndex;
		private int _maximumDisplayedIndex;

		private List<RowContainer> _cells = new List<RowContainer>();
		private float _rowInsets;
		private float _columnInsets;

		public IStormCollectionViewSource Source
		{
			get => _source;
			set
			{
				_source = value;
				DataChanged();
			}
		}

		public override UIEdgeInsets ContentInset
		{
			get => _contentInset;
			set
			{
				_contentInset = value;
				UpdateInsets();
			}
		}

		public float RowInsets
		{
			get => _rowInsets;
			set
			{
				_rowInsets = value;
				UpdateInsets();
			}
		}

		public float ColumnInsets
		{
			get => _columnInsets;
			set
			{
				_columnInsets = value;
				UpdateInsets();
			}
		}

		public StormCollectionView(int estimatedHeight, int columnCount = 1, ICellFactory factory = null)
		{
			_estimatedHeight = estimatedHeight;
			_columnCount = columnCount;
			_factory = factory ?? new DefaultCellFactory();

			_scrollContent = new UIView
			{
				TranslatesAutoresizingMaskIntoConstraints = false,
				BackgroundColor = UIColor.Red
			};
			_container = new UILayoutGuide
			{
				Identifier = "ContainerGuide"
			};

			AddSubview(_scrollContent);
			_scrollContent.AddLayoutGuide(_container);


			AddConstraints(new[]
			{
				WidthAnchor.ConstraintEqualTo(_scrollContent.WidthAnchor),
				CenterXAnchor.ConstraintEqualTo(_scrollContent.CenterXAnchor),
				TopAnchor.ConstraintEqualTo(_scrollContent.TopAnchor),
				BottomAnchor.ConstraintEqualTo(_scrollContent.BottomAnchor)
			});
			_scrollContent.AddConstraints(new[]
			{
				_topContainerConstraint = _container.TopAnchor.ConstraintEqualTo(_scrollContent.TopAnchor),
				_bottomContainerConstraint = _scrollContent.BottomAnchor.ConstraintEqualTo(_container.BottomAnchor),
				_leftContainerConstraint = _container.LeftAnchor.ConstraintEqualTo(_scrollContent.LeftAnchor),
				_rightContainerConstraint = _scrollContent.RightAnchor.ConstraintEqualTo(_container.RightAnchor),
				_scrollContent.CenterXAnchor.ConstraintEqualTo(_container.CenterXAnchor),
			});
		}

		#region Cells layout

		private static (int minIndexToDisplay, int countToDisplay, nfloat aboveHeight, nfloat belowHeight) CalculateDisplayArea(nfloat minOffsetToDisplay, nfloat maxOffsetToDisplay, nfloat[] sizes, nfloat rowInset)
		{
			int index = 0;
			nfloat accumulatedHeight = 0;
			int minIndexToDisplay = 0;
			nfloat aboveHeight = 0;

			for (; index < sizes.Length && accumulatedHeight <= minOffsetToDisplay; accumulatedHeight += sizes[index] + rowInset, index++)
			{
				aboveHeight = accumulatedHeight;
				minIndexToDisplay = index;
			}

			if (index == sizes.Length)
			{
				return (minIndexToDisplay, 1, aboveHeight, 0);
			}

			int maxIndexToDisplay = minIndexToDisplay;
			for (; index < sizes.Length && accumulatedHeight <= maxOffsetToDisplay; accumulatedHeight += sizes[index] + rowInset, index++)
			{
				maxIndexToDisplay = index;
			}

			if (index == sizes.Length)
			{
				return (minIndexToDisplay, maxIndexToDisplay - minIndexToDisplay + 1, aboveHeight, 0);
			}

			nfloat endOfDisplayHeight = accumulatedHeight;

			for (; index < sizes.Length; accumulatedHeight += sizes[index] + rowInset, index++)
			{
			}

			nfloat belowHeight = accumulatedHeight - rowInset - endOfDisplayHeight;

			int countToDisplay = maxIndexToDisplay - minIndexToDisplay + 1;
			return (minIndexToDisplay, countToDisplay, aboveHeight, belowHeight);
		}

		public override void LayoutSubviews()
		{
			base.LayoutSubviews();

			IStormCollectionViewSource source = _source;
			if (source is null)
			{
				return;
			}

			nfloat offset = ContentOffset.Y;
			nfloat displayHeight = Frame.Height;
			nfloat maxOffset = ContentSize.Height - displayHeight;

			if (offset < 0) offset = 0;
			if (offset > maxOffset) offset = maxOffset;

			nfloat minOffsetToDisplay = offset - displayHeight * 0.2f;
			if (minOffsetToDisplay < 0) minOffsetToDisplay = 0;

			nfloat maxOffsetToDisplay = offset + displayHeight * 1.2f;

			(int minIndexToDisplay, int countToDisplay, nfloat topConstraint, nfloat bottomConstraint) = CalculateDisplayArea(minOffsetToDisplay, maxOffsetToDisplay, _sizes, _rowInsets);

			_topContainerConstraint.Constant = topConstraint + ContentInset.Top;
			_bottomContainerConstraint.Constant = bottomConstraint + ContentInset.Bottom;

			int totalCellCount = source.Count;
			
			if (_minimalDisplayedIndex < minIndexToDisplay) // can remove the top one 
			{
				RemoveCellsAbove(minIndexToDisplay);
			}
			else if (_minimalDisplayedIndex > minIndexToDisplay) // need to add some on top
			{
				AddCellsAbove(minIndexToDisplay, totalCellCount, source);
			}

			int maxIndexToDisplay = minIndexToDisplay + countToDisplay;
			if (_maximumDisplayedIndex < maxIndexToDisplay) // need to add some below 
			{
				AddCellsBelow(maxIndexToDisplay, totalCellCount, source);
			}
			else if (_maximumDisplayedIndex > maxIndexToDisplay) // need to remove some on bottom
			{
				RemoveCellBelow(maxIndexToDisplay);
			}

			_minimalDisplayedIndex = minIndexToDisplay;
			_maximumDisplayedIndex = maxIndexToDisplay;
		}

		private void AddCellsAbove(int minIndexToDisplay, int totalCellCount, IStormCollectionViewSource source)
		{
			if (_firstItemConstraint != null)
			{
				_scrollContent.RemoveConstraint(_firstItemConstraint);
			}

			RowContainer last = _cells.FirstOrDefault();
			for (int cellIndex = _minimalDisplayedIndex - 1; cellIndex >= minIndexToDisplay; cellIndex--)
			{
				RowContainer cell = CreateRow(cellIndex, totalCellCount, source);
				_cells.Insert(0, cell);

				if (last is null)
				{
					_scrollContent.AddConstraint(_lastItemConstraint = _container.BottomAnchor.ConstraintEqualTo(cell.Guide.BottomAnchor));
				}
				else
				{
					_scrollContent.AddConstraint(last.Guide.TopAnchor.ConstraintEqualTo(cell.Guide.BottomAnchor, _rowInsets));
				}

				last = cell;
			}

			if (last != null)
			{
				_scrollContent.AddConstraint(_firstItemConstraint = _container.TopAnchor.ConstraintEqualTo(last.Guide.TopAnchor));
			}
		}

		private void RemoveCellsAbove(int minIndexToDisplay)
		{
			int countToRemove = minIndexToDisplay - _minimalDisplayedIndex;
			for (int cellIndex = 0; cellIndex < countToRemove; cellIndex++)
			{
				RemoveRow(_cells[cellIndex]);
			}

			if (_firstItemConstraint != null)
			{
				_scrollContent.RemoveConstraint(_firstItemConstraint);
			}

			_cells.RemoveRange(0, countToRemove);

			if (_cells.Count > 0)
			{
				_scrollContent.AddConstraint(_firstItemConstraint = _container.TopAnchor.ConstraintEqualTo(_cells[0].Guide.TopAnchor));
			}
		}

		private void AddCellsBelow(int maxIndexToDisplay, int totalCellCount, IStormCollectionViewSource source)
		{
			RowContainer last = _cells.LastOrDefault();
			for (int cellIndex = _maximumDisplayedIndex; cellIndex < maxIndexToDisplay; cellIndex++)
			{
				RowContainer cell = CreateRow(cellIndex, totalCellCount, source);
				_cells.Add(cell);

				if (last is null)
				{
					_scrollContent.AddConstraint(_firstItemConstraint = _container.TopAnchor.ConstraintEqualTo(cell.Guide.TopAnchor));
				}
				else
				{
					_scrollContent.AddConstraint(cell.Guide.TopAnchor.ConstraintEqualTo(last.Guide.BottomAnchor, _rowInsets));
				}

				last = cell;
			}

			if (last != null)
			{
				if (_lastItemConstraint != null)
				{
					_scrollContent.RemoveConstraint(_lastItemConstraint);
				}

				_scrollContent.AddConstraint(_lastItemConstraint = _container.BottomAnchor.ConstraintEqualTo(last.Guide.BottomAnchor));
			}
		}

		private void RemoveCellBelow(int maxIndexToDisplay)
		{
			int countToRemove = _maximumDisplayedIndex - maxIndexToDisplay;
			for (int cellIndex = _cells.Count - countToRemove; cellIndex < _cells.Count; cellIndex++)
			{
				RemoveRow(_cells[cellIndex]);
			}

			if (_lastItemConstraint != null)
			{
				_scrollContent.RemoveConstraint(_lastItemConstraint);
			}

			_cells.RemoveRange(_cells.Count - countToRemove, countToRemove);

			if (_cells.Count > 0)
			{
				_scrollContent.AddConstraint(_lastItemConstraint = _container.BottomAnchor.ConstraintEqualTo(_cells[_cells.Count - 1].Guide.BottomAnchor));
			}
		}

		#endregion

		private RowContainer CreateRow(int rowIndex, int totalCellCount, IStormCollectionViewSource source)
		{
			UILayoutGuide guide = _guideFactory.Get();
			guide.Identifier = $"Row {rowIndex}";
			_scrollContent.AddLayoutGuide(guide);

			NSLayoutConstraint rowHeightConstraint = guide.HeightAnchor.ConstraintEqualTo(_sizes[rowIndex]);

			_scrollContent.AddConstraints(new[]
			{
				//guide layout (width / centerX)
				_container.WidthAnchor.ConstraintEqualTo(guide.WidthAnchor),
				_container.CenterXAnchor.ConstraintEqualTo(guide.CenterXAnchor),
				rowHeightConstraint
			});

			int cellIndex = rowIndex * _columnCount;
			int cellCount = totalCellCount - cellIndex;
			if (cellCount > _columnCount)
			{
				cellCount = _columnCount;
			}

			UIView[] cells = new UIView[cellCount];
			UIView previous = null;
			for (int i = 0; i < cellCount; ++i)
			{
				UIView cell = source.GetCell(cellIndex + i, _factory);

				cell.TranslatesAutoresizingMaskIntoConstraints = false;
				cell.AddObserver(this, SELECTOR_BOUNDS_SIZE, NSKeyValueObservingOptions.Old, Handle);
				cells[i] = cell;

				_scrollContent.AddSubview(cell);

				_scrollContent.AddConstraints(new[]
				{
					//cell layout in guide
					guide.TopAnchor.ConstraintEqualTo(cell.TopAnchor),
					guide.WidthAnchor.ConstraintEqualTo(cell.WidthAnchor, _columnCount, (_columnCount - 1) * _columnInsets),
				});

				if (previous is null)
				{
					_scrollContent.AddConstraint(guide.LeftAnchor.ConstraintEqualTo(cell.LeftAnchor));
				}
				else
				{
					_scrollContent.AddConstraint(cell.LeftAnchor.ConstraintEqualTo(previous.RightAnchor, _columnInsets));
				}

				previous = cell;
			}

			_scrollContent.AddConstraint(guide.RightAnchor.ConstraintEqualTo(previous.RightAnchor));

			return new RowContainer(guide, cells, rowHeightConstraint);
		}

		private void RemoveRow(RowContainer container)
		{
			_scrollContent.RemoveLayoutGuide(container.Guide);
			_guideFactory.Recycle(container.Guide);

			foreach (UIView cell in container.Cells)
			{
				cell.RemoveFromSuperview();
				cell.RemoveObserver(this, SELECTOR_BOUNDS_SIZE, Handle);
				_factory.Recycle(cell);
			}
		}

		public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
		{
			if (context != Handle)
			{
				base.ObserveValue(keyPath, ofObject, change, context);
				return;
			}

			if (keyPath == SELECTOR_BOUNDS_SIZE)
			{
				for (int i = 0; i < _cells.Count; ++i)
				{
					_sizes[_minimalDisplayedIndex + i] = _cells[i].UpdateHeight();
				}
			}
			else
			{
				throw new ArgumentOutOfRangeException(nameof(keyPath), keyPath, "Unsupported KVO notification keypath");
			}
		}

		private void UpdateInsets()
		{
			_leftContainerConstraint.Constant = ContentInset.Left;
			_rightContainerConstraint.Constant = ContentInset.Right;

			for (var i = 0; i < _scrollContent.Constraints.Length; i++)
			{
				NSLayoutConstraint constraint = _scrollContent.Constraints[i];

				if (constraint.FirstItem is UILayoutGuide &&
				    constraint.FirstAttribute == NSLayoutAttribute.Top &&
				    constraint.SecondItem is UILayoutGuide &&
				    constraint.SecondAttribute == NSLayoutAttribute.Bottom)
				{
					constraint.Constant = _rowInsets;
				}
				
				if (constraint.FirstItem is UIView &&
				    constraint.FirstAttribute == NSLayoutAttribute.Left &&
				    constraint.SecondItem is UIView &&
				    constraint.SecondAttribute == NSLayoutAttribute.Right)
				{
					constraint.Constant = _columnInsets;
				}
				
				if (constraint.FirstItem is UILayoutGuide &&
				    constraint.FirstAttribute == NSLayoutAttribute.Width &&
				    constraint.SecondItem is UIView &&
				    constraint.SecondAttribute == NSLayoutAttribute.Width)
				{
					constraint.Constant = (_columnCount - 1) * _columnInsets;
				}
			}

			SetNeedsLayout();
			LayoutIfNeeded();
		}

		public void DataChanged()
		{
			IStormCollectionViewSource source = _source;
			if (source is null)
			{
				return;
			}

			int totalCellCount = source.Count;
			int rowCount = totalCellCount / _columnCount;
			if (totalCellCount % _columnCount != 0)
			{
				rowCount++;
			}

			_sizes = new nfloat[rowCount];
			for (int i = 0; i < rowCount; i++)
			{
				_sizes[i] = _estimatedHeight;
			}

			SetNeedsLayout();
			LayoutIfNeeded();
		}
	}
}