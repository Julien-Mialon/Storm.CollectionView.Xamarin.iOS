using System.Linq;
using UIKit;
using static UIKit.NSLayoutAttribute;
using static UIKit.NSLayoutRelation;

namespace StormCollectionViews.Samples.ViewControllers
{
	public class SimpleVerticalCollectionViewController : UIViewController
	{
		public SimpleVerticalCollectionViewController()
		{
			StormCollectionView collectionView = new StormCollectionView(80, columnCount:2)
			{
				ContentInset = new UIEdgeInsets(20, 20, 20, 20),
				RowInsets = 12,
			};
			DefaultStormCollectionViewSource<int, BackgroundCell> source = new DefaultStormCollectionViewSource<int, BackgroundCell>(collectionView, Bind);
			source.Items = Enumerable.Range(0, 20).ToList();
			collectionView.Source = source;
			
			View.BackgroundColor = UIColor.Blue;
			View.AddSubview(collectionView);

			collectionView.TranslatesAutoresizingMaskIntoConstraints = false;
			View.AddConstraints(new []
			{
				NSLayoutConstraint.Create(View, Width, Equal, collectionView, Width, 1, 0), 
				NSLayoutConstraint.Create(View, CenterX, Equal, collectionView, CenterX, 1, 0), 
				NSLayoutConstraint.Create(View, Top, Equal, collectionView, Top, 1, 0), 
				NSLayoutConstraint.Create(View, Bottom, Equal, collectionView, Bottom, 1, 0), 
			});
			
			View.AddGestureRecognizer(new UITapGestureRecognizer(() => collectionView.RowInsets = 24));
		}

		private void Bind(int index, int item, BackgroundCell cell)
		{
			cell.Bind(index, item);
		}
	}

	public sealed class BackgroundCell : UIView
	{
		private static int _createCount = 0;
		private int _createIndex;
		private int _indexInCollection;
		private int _itemIndex;
		private UILabel _contentLabel;
		private NSLayoutConstraint _constraint;
		
		public BackgroundCell()
		{
			_createIndex = _createCount++;
			UIView content = new UIView
			{
				BackgroundColor = UIColor.Purple
			};
			BackgroundColor = UIColor.DarkGray;
			
			AddSubview(content);
			
			_contentLabel = new UILabel
			{
				TextColor = UIColor.White,
				Lines = 0,
				LineBreakMode = UILineBreakMode.WordWrap,
			};
			
			
			content.AddSubview(_contentLabel);

			TranslatesAutoresizingMaskIntoConstraints = false;
			content.TranslatesAutoresizingMaskIntoConstraints = false;
			_contentLabel.TranslatesAutoresizingMaskIntoConstraints = false;
			
			AddConstraints(new []
			{
				NSLayoutConstraint.Create(this, Width, Equal, content, Width, 1, 0), 
				NSLayoutConstraint.Create(this, CenterX, Equal, content, CenterX, 1, 0), 
				NSLayoutConstraint.Create(this, Height, Equal, content, Height, 1, 16), 
				NSLayoutConstraint.Create(this, CenterY, Equal, content, CenterY, 1, 0), 
				_constraint = NSLayoutConstraint.Create(this, Height, Equal, 1, 100), 
				NSLayoutConstraint.Create(content, Width, Equal, _contentLabel, Width, 1, 0), 
				NSLayoutConstraint.Create(content, CenterX, Equal, _contentLabel, CenterX, 1, 0), 
				NSLayoutConstraint.Create(content, CenterY, Equal, _contentLabel, CenterY, 1, 0),
			});
		}

		public void Bind(int indexInCollection, int itemIndex)
		{
			_indexInCollection = indexInCollection;
			_itemIndex = itemIndex;

			_contentLabel.Text = $"Create: {_createIndex:D2} / Collection: {_indexInCollection:D2} / Source: {_itemIndex:D2}";

			_constraint.Constant = 100 + itemIndex * 5;
		}
	}
}