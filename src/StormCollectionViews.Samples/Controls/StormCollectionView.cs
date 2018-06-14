using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
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

	public class LayoutGuideFactory
	{
		private readonly Queue<UILayoutGuide> _usables = new Queue<UILayoutGuide>();

		public UILayoutGuide Get()
		{
			if (_usables.Count > 0)
			{
				return _usables.Dequeue();
			}
			
			return new UILayoutGuide();
		}

		public void Recycle(UILayoutGuide guide)
		{
			_usables.Enqueue(guide);
		}
	}
	
	public class StormCollectionView : UIScrollView, IStormCollectionView
	{
		private const string SELECTOR_BOUNDS_SIZE = "bounds";
		private readonly int _estimatedHeight;
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

		public StormCollectionView(int estimatedHeight, ICellFactory factory = null)
		{
			_estimatedHeight = estimatedHeight;
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
			
			
			AddConstraints(new []
			{
				WidthAnchor.ConstraintEqualTo(_scrollContent.WidthAnchor),
				CenterXAnchor.ConstraintEqualTo(_scrollContent.CenterXAnchor),
				TopAnchor.ConstraintEqualTo(_scrollContent.TopAnchor),
				BottomAnchor.ConstraintEqualTo(_scrollContent.BottomAnchor)
			});
			_scrollContent.AddConstraints(new []
			{
				_topContainerConstraint = _container.TopAnchor.ConstraintEqualTo(_scrollContent.TopAnchor),
				_bottomContainerConstraint = _scrollContent.BottomAnchor.ConstraintEqualTo(_container.BottomAnchor),
				_leftContainerConstraint = _container.LeftAnchor.ConstraintEqualTo(_scrollContent.LeftAnchor),
				_rightContainerConstraint = _scrollContent.RightAnchor.ConstraintEqualTo(_container.RightAnchor),
				_scrollContent.CenterXAnchor.ConstraintEqualTo(_container.CenterXAnchor),
			});
		}

		#region Cells layout
		
		private static (int minIndexToDisplay, int countToDisplay, nfloat aboveHeight, nfloat belowHeight) CalculateDisplayArea(nfloat minOffsetToDisplay, nfloat maxOffsetToDisplay, nfloat[] sizes, float rowInset)
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
				accumulatedHeight -= rowInset;
				rowInset = 0;
			}
			
			int maxIndexToDisplay = minIndexToDisplay;

			for (; index < sizes.Length && accumulatedHeight <= maxOffsetToDisplay; accumulatedHeight += sizes[index] + rowInset, index++)
			{
				maxIndexToDisplay = index;
			}
			
			if (index == sizes.Length)
			{
				accumulatedHeight -= rowInset;
				rowInset = 0;
			}
			
			nfloat displayHeight = accumulatedHeight;
			
			for (; index < sizes.Length; accumulatedHeight += sizes[index] + rowInset, index++) { }
			
			if (index == sizes.Length)
			{
				accumulatedHeight -= rowInset;
			}
			
			nfloat belowHeight = accumulatedHeight - displayHeight;

			int countToDisplay = maxIndexToDisplay - minIndexToDisplay + 1;
			System.Diagnostics.Debug.WriteLine($"Display [{minIndexToDisplay} ; {maxIndexToDisplay}] {aboveHeight} -- {belowHeight}");
			return (minIndexToDisplay, countToDisplay, aboveHeight, belowHeight);
		}

		public override void LayoutSubviews()
		{
			base.LayoutSubviews();

			IStormCollectionViewSource source = Source;
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

			if (_minimalDisplayedIndex < minIndexToDisplay) // can remove the top one 
			{
				RemoveCellsAbove(minIndexToDisplay);
			}
			else if (_minimalDisplayedIndex > minIndexToDisplay) // need to add some on top
			{
				AddCellsAbove(minIndexToDisplay);
			}

			int maxIndexToDisplay = minIndexToDisplay + countToDisplay;
			if (_maximumDisplayedIndex < maxIndexToDisplay) // need to add some below 
			{
				AddCellsBelow(maxIndexToDisplay);
			}
			else if (_maximumDisplayedIndex > maxIndexToDisplay) // need to remove some on bottom
			{
				RemoveCellBelow(maxIndexToDisplay);
			}

			_minimalDisplayedIndex = minIndexToDisplay;
			_maximumDisplayedIndex = maxIndexToDisplay;
		}

		private void AddCellsAbove(int minIndexToDisplay)
		{
			if (_firstItemConstraint != null)
			{
				_scrollContent.RemoveConstraint(_firstItemConstraint);
			}

			RowContainer last = _cells.FirstOrDefault();
			for (int cellIndex = _minimalDisplayedIndex - 1; cellIndex >= minIndexToDisplay; cellIndex--)
			{
				RowContainer cell = GetCell(cellIndex);
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
				RemoveCell(_cells[cellIndex]);
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

		private void AddCellsBelow(int maxIndexToDisplay)
		{
			RowContainer last = _cells.LastOrDefault();
			for (int cellIndex = _maximumDisplayedIndex; cellIndex < maxIndexToDisplay; cellIndex++)
			{
				RowContainer cell = GetCell(cellIndex);
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
				RemoveCell(_cells[cellIndex]);
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
		
		private RowContainer GetCell(int index)
		{
			UIView cell = _source.GetCell(index, _factory);

			cell.TranslatesAutoresizingMaskIntoConstraints = false;
			cell.AddObserver(this, SELECTOR_BOUNDS_SIZE, NSKeyValueObservingOptions.Old, Handle);

			UILayoutGuide guide = _guideFactory.Get();
			
			_scrollContent.AddSubview(cell);
			_scrollContent.AddLayoutGuide(guide);
			
			_scrollContent.AddConstraints(new []
			{
				//cell layout in guide
				guide.CenterXAnchor.ConstraintEqualTo(cell.CenterXAnchor),
				guide.CenterYAnchor.ConstraintEqualTo(cell.CenterYAnchor),
				guide.WidthAnchor.ConstraintEqualTo(cell.WidthAnchor),
				guide.HeightAnchor.ConstraintEqualTo(cell.HeightAnchor),
				
				//guide layout (width / centerX)
				_container.WidthAnchor.ConstraintEqualTo(guide.WidthAnchor),
				_container.CenterXAnchor.ConstraintEqualTo(guide.CenterXAnchor),
			});
			
			return new RowContainer(guide, cell);
		}

		private void RemoveCell(RowContainer container)
		{
			container.Cell.RemoveFromSuperview();
			container.Cell.RemoveObserver(this, SELECTOR_BOUNDS_SIZE, Handle);
			
			_scrollContent.RemoveLayoutGuide(container.Guide);
			
			_factory.Recycle(container.Cell);
			_guideFactory.Recycle(container.Guide);
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
					_sizes[_minimalDisplayedIndex + i] = _cells[i].Cell.Bounds.Height;
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
				var constraint = _scrollContent.Constraints[i];

				if (constraint.FirstAttribute == NSLayoutAttribute.Top && constraint.SecondAttribute == NSLayoutAttribute.Bottom)
				{
					constraint.Constant = _rowInsets;
				}
			}
			
			SetNeedsLayout();
			LayoutIfNeeded();
		}

		public void DataChanged()
		{
			if (Source is null)
			{
				return;
			}

			int count = _source.Count;
			_sizes = new nfloat[count];
			for (int i = 0; i < count; i++)
			{
				_sizes[i] = _estimatedHeight;
			}

			SetNeedsLayout();
			LayoutIfNeeded();
		}
	}
}