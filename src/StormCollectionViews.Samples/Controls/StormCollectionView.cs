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
		private ICellFactory _factory;
		private IStormCollectionViewSource _source;

		private UIEdgeInsets _contentInset;

		private UIView _container;
		private UILayoutGuide _containerGuide;
		
		private NSLayoutConstraint _topContainerConstraint;
		private NSLayoutConstraint _bottomContainerConstraint;
		private NSLayoutConstraint _leftContainerConstraint;
		private NSLayoutConstraint _rightContainerConstraint;

		private NSLayoutConstraint _firstItemConstraint;
		private NSLayoutConstraint _lastItemConstraint;

		private nfloat[] _sizes;
		private int _minimalDisplayedIndex;
		private int _maximumDisplayedIndex;

		private List<UIView> _cells = new List<UIView>();
		
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

		public StormCollectionView(int estimatedHeight, ICellFactory factory = null)
		{
			_estimatedHeight = estimatedHeight;
			_factory = factory ?? new DefaultCellFactory();

			_container = new UIView
			{
				TranslatesAutoresizingMaskIntoConstraints = false,
				BackgroundColor = UIColor.Red
			};
			_containerGuide = new UILayoutGuide
			{
				Identifier = "ContainerGuide"
			};
			AddSubview(_container);
			_container.AddLayoutGuide(_containerGuide);
			
			
			AddConstraints(new []
			{
				WidthAnchor.ConstraintEqualTo(_container.WidthAnchor),
				CenterXAnchor.ConstraintEqualTo(_container.CenterXAnchor),
				TopAnchor.ConstraintEqualTo(_container.TopAnchor),
				BottomAnchor.ConstraintEqualTo(_container.BottomAnchor)
			});
			_container.AddConstraints(new []
			{
				_topContainerConstraint = _containerGuide.TopAnchor.ConstraintEqualTo(_container.TopAnchor),
				_bottomContainerConstraint = _container.BottomAnchor.ConstraintEqualTo(_containerGuide.BottomAnchor),
				_leftContainerConstraint = _containerGuide.LeftAnchor.ConstraintEqualTo(_container.LeftAnchor),
				_rightContainerConstraint = _container.RightAnchor.ConstraintEqualTo(_containerGuide.RightAnchor),
				_container.CenterXAnchor.ConstraintEqualTo(_containerGuide.CenterXAnchor),
			});
		}

		#region Cells layout
		
		private static (int minIndexToDisplay, int countToDisplay, nfloat aboveHeight, nfloat belowHeight) CalculateDisplayArea(nfloat minOffsetToDisplay, nfloat maxOffsetToDisplay, nfloat[] sizes)
		{
			int index = 0;
			nfloat accumulatedHeight = 0;
			int minIndexToDisplay = 0;
			nfloat aboveHeight = 0;

			for (; index < sizes.Length && accumulatedHeight <= minOffsetToDisplay; accumulatedHeight += sizes[index], index++)
			{
				aboveHeight = accumulatedHeight;
				minIndexToDisplay = index;
			}

			int maxIndexToDisplay = minIndexToDisplay;

			for (; index < sizes.Length && accumulatedHeight <= maxOffsetToDisplay; accumulatedHeight += sizes[index], index++)
			{
				maxIndexToDisplay = index;
			}
			nfloat displayHeight = accumulatedHeight;
			
			for (; index < sizes.Length; accumulatedHeight += sizes[index], index++) { }
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

			(int minIndexToDisplay, int countToDisplay, nfloat topConstraint, nfloat bottomConstraint) = CalculateDisplayArea(minOffsetToDisplay, maxOffsetToDisplay, _sizes);
			
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
				_container.RemoveConstraint(_firstItemConstraint);
			}

			UIView last = _cells.FirstOrDefault();
			for (int cellIndex = _minimalDisplayedIndex - 1; cellIndex >= minIndexToDisplay; cellIndex--)
			{
				UIView cell = GetCell(cellIndex);
				_cells.Insert(0, cell);
				_container.AddSubview(cell);
				_container.AddConstraints(new[]
				{
					_containerGuide.WidthAnchor.ConstraintEqualTo(cell.WidthAnchor),
					_containerGuide.CenterXAnchor.ConstraintEqualTo(cell.CenterXAnchor),
				});

				if (last is null)
				{
					_container.AddConstraint(_lastItemConstraint = _containerGuide.BottomAnchor.ConstraintEqualTo(cell.BottomAnchor));
				}
				else
				{
					_container.AddConstraint(cell.BottomAnchor.ConstraintEqualTo(last.TopAnchor));
				}

				last = cell;
			}

			if (last != null)
			{
				_container.AddConstraint(_firstItemConstraint = _containerGuide.TopAnchor.ConstraintEqualTo(last.TopAnchor));
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
				_container.RemoveConstraint(_firstItemConstraint);
			}

			_cells.RemoveRange(0, countToRemove);

			if (_cells.Count > 0)
			{
				_container.AddConstraint(_firstItemConstraint = _containerGuide.TopAnchor.ConstraintEqualTo(_cells[0].TopAnchor));
			}
		}

		private void AddCellsBelow(int maxIndexToDisplay)
		{
			UIView last = _cells.LastOrDefault();
			for (int cellIndex = _maximumDisplayedIndex; cellIndex < maxIndexToDisplay; cellIndex++)
			{
				UIView cell = GetCell(cellIndex);
				_cells.Add(cell);
				_container.AddSubview(cell);
				_container.AddConstraints(new[]
				{
					_containerGuide.WidthAnchor.ConstraintEqualTo(cell.WidthAnchor),
					_containerGuide.CenterXAnchor.ConstraintEqualTo(cell.CenterXAnchor),
				});

				if (last is null)
				{
					_container.AddConstraint(_firstItemConstraint = _containerGuide.TopAnchor.ConstraintEqualTo(cell.TopAnchor));
				}
				else
				{
					_container.AddConstraint(cell.TopAnchor.ConstraintEqualTo(last.BottomAnchor));
				}

				last = cell;
			}

			if (last != null)
			{
				if (_lastItemConstraint != null)
				{
					_container.RemoveConstraint(_lastItemConstraint);
				}

				_container.AddConstraint(_lastItemConstraint = _containerGuide.BottomAnchor.ConstraintEqualTo(last.BottomAnchor));
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
				_container.RemoveConstraint(_lastItemConstraint);
			}

			_cells.RemoveRange(_cells.Count - countToRemove, countToRemove);

			if (_cells.Count > 0)
			{
				_container.AddConstraint(_lastItemConstraint = _containerGuide.BottomAnchor.ConstraintEqualTo(_cells[_cells.Count - 1].BottomAnchor));
			}
		}
		
		#endregion
		
		private UIView GetCell(int index)
		{
			UIView cell = _source.GetCell(index, _factory);

			cell.TranslatesAutoresizingMaskIntoConstraints = false;
			cell.AddObserver(this, SELECTOR_BOUNDS_SIZE, NSKeyValueObservingOptions.Old, Handle);
			
			return cell;
		}

		private void RemoveCell(UIView cell)
		{
			cell.RemoveFromSuperview();
			cell.RemoveObserver(this, SELECTOR_BOUNDS_SIZE, Handle);
			_factory.Recycle(cell);
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
					_sizes[_minimalDisplayedIndex + i] = _cells[i].Bounds.Height;
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