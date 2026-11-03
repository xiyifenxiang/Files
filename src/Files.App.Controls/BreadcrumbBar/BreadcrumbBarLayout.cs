// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Foundation;

namespace Files.App.Controls
{
	/// <summary>
	/// Handles layout of <see cref="BreadcrumbBar"/>, collapsing items into an ellipsis button when necessary.
	/// </summary>
	public partial class BreadcrumbBarLayout : NonVirtualizingLayout
	{
		// Fields

		private readonly WeakReference<BreadcrumbBar>? _ownerRef;

		private BreadcrumbBarItem? _ellipsisButton = null;
		private int _measureIndexAfterEllipsis;

		// Properties

		public bool EllipsisIsRendered { get; private set; }
		public int IndexAfterEllipsis { get; private set; }
		public int VisibleItemsCount { get; private set; }

		public BreadcrumbBarLayout(BreadcrumbBar breadcrumb)
		{
			_ownerRef = new(breadcrumb);
		}

		protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
		{
			var accumulatedSize = new Size(0, 0);

			// Items are measured first so the collapse index is based on the current
			// item widths. Measuring after the index was calculated left it using the
			// previous pass' desired sizes, which are stale after navigating or resizing.
			for (int index = 0; index < context.Children.Count; index++)
			{
				if (context.Children[index] is BreadcrumbBarItem breadcrumbItem)
				{
					breadcrumbItem.Measure(availableSize);
					accumulatedSize.Height = Math.Max(accumulatedSize.Height, breadcrumbItem.DesiredSize.Height);
				}
			}

			_measureIndexAfterEllipsis = GetFirstIndexToRender(context, availableSize.Width);
			EllipsisIsRendered = _measureIndexAfterEllipsis is not 0;

			for (int index = _measureIndexAfterEllipsis; index < context.Children.Count; index++)
			{
				if (context.Children[index] is BreadcrumbBarItem breadcrumbItem)
					accumulatedSize.Width += breadcrumbItem.DesiredSize.Width;
			}

			// Get a reference to the ellipsis item
			if (context.Children.Count > 0)
				_ellipsisButton ??= context.Children[0] as BreadcrumbBarItem;

			// Report the full available width instead of only the visible items' width.
			// A collapsed pass used to report 0, which shrank the repeater to zero width;
			// it then had no viewport left to realize items again and stayed empty.
			if (!double.IsInfinity(availableSize.Width))
				accumulatedSize.Width = availableSize.Width;

			return accumulatedSize;
		}

		protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
		{
			double accumulatedWidths = 0d;

			// The repeater fills the available column, so the arranged width is the real
			// available width and can be used to recalculate the collapse index.
			IndexAfterEllipsis = double.IsInfinity(finalSize.Width)
				? _measureIndexAfterEllipsis
				: GetFirstIndexToRender(context, finalSize.Width);

			EllipsisIsRendered = IndexAfterEllipsis is not 0;
			VisibleItemsCount = 0;

			// Go through all items and arrange them
			for (int index = 0; index < context.Children.Count; index++)
			{
				if (context.Children[index] is BreadcrumbBarItem breadcrumbItem)
				{
					if (index < IndexAfterEllipsis)
					{
						// Collapse
						breadcrumbItem.Arrange(new Rect(0, 0, 0, 0));
					}
					else
					{
						// Arrange normally
						breadcrumbItem.Arrange(new Rect(accumulatedWidths, 0, breadcrumbItem.DesiredSize.Width, breadcrumbItem.DesiredSize.Height));

						accumulatedWidths += breadcrumbItem.DesiredSize.Width;

						VisibleItemsCount++;
					}
				}
			}

			if (_ownerRef?.TryGetTarget(out var breadcrumbBar) ?? false)
				breadcrumbBar.OnLayoutUpdated();

			// Keep the width that was assigned to the repeater. Reporting the arranged
			// item widths made the repeater shrink itself (down to 0 when everything
			// was collapsed) and it never regained enough space to realize items again.
			if (double.IsInfinity(finalSize.Width))
				finalSize.Width = accumulatedWidths;

			return finalSize;
		}

		private int GetFirstIndexToRender(NonVirtualizingLayoutContext context, double availableWidth)
		{
			var itemCount = context.Children.Count;
			var accumulatedWidth = 0d;

			// Handle zero or negative available width - hide all items
			if (availableWidth <= 0)
				return itemCount;

			// Go through all items from the last item
			for (int index = itemCount - 1; index >= 0; index--)
			{
				var newAccumulatedWidth = accumulatedWidth + context.Children[index].DesiredSize.Width;
				if (newAccumulatedWidth >= availableWidth)
					return index + 1;

				accumulatedWidth = newAccumulatedWidth;
			}

			return 0;
		}
	}
}
