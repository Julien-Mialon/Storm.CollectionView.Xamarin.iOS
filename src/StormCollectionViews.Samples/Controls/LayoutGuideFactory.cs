using System.Collections.Generic;
using UIKit;

namespace StormCollectionViews
{
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
}