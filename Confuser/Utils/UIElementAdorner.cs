// Copyright Josh Smith 2/2007
using System;
using System.Collections;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WPF.JoshSmith.Adorners
{
	public class UIElementAdorner : Adorner
	{
		#region Data

		private UIElement child = null;
		private double offsetLeft = 0;
		private double offsetTop = 0;

		#endregion // Data

		#region Constructor

		public UIElementAdorner( UIElement adornedElement, UIElement elem )
			: base( adornedElement )
		{
			if( elem == null )
				throw new ArgumentNullException( "elem" );

			this.child = elem;
			this.AddLogicalChild( elem );
			this.AddVisualChild( elem );
		}

		#endregion // Constructor

		#region Public Interface

		#region GetDesiredTransform

		/// <summary>
		/// Override.
		/// </summary>
		/// <param name="transform"></param>
		/// <returns></returns>
		public override GeneralTransform GetDesiredTransform( GeneralTransform transform )
		{
			GeneralTransformGroup result = new GeneralTransformGroup();
			result.Children.Add( base.GetDesiredTransform( transform ) );
			result.Children.Add( new TranslateTransform( this.offsetLeft, this.offsetTop ) );
			return result;
		}

		#endregion // GetDesiredTransform

		#region OffsetLeft

		/// <summary>
		/// Gets/sets the horizontal offset of the adorner.
		/// </summary>
		public double OffsetLeft
		{
			get { return this.offsetLeft; }
			set
			{
				this.offsetLeft = value;
				UpdateLocation();
			}
		}

		#endregion // OffsetLeft

		#region SetOffsets

		/// <summary>
		/// Updates the location of the adorner in one atomic operation.
		/// </summary>
		/// <param name="left"></param>
		/// <param name="top"></param>
		public void SetOffsets( double left, double top )
		{
			this.offsetLeft = left;
			this.offsetTop = top;
			this.UpdateLocation();
		}

		#endregion // SetOffsets

		#region OffsetTop

		/// <summary>
		/// Gets/sets the vertical offset of the adorner.
		/// </summary>
		public double OffsetTop
		{
			get { return this.offsetTop; }
			set
			{
				this.offsetTop = value;
				UpdateLocation();
			}
		}

		#endregion // OffsetTop

		#endregion // Public Interface

		#region Base Class Overrides

		/// <summary>
		/// Override.
		/// </summary>
		/// <param name="constraint"></param>
		/// <returns></returns>
		protected override Size MeasureOverride( Size constraint )
		{
			this.child.Measure( constraint );
			return this.child.DesiredSize;
		}

		/// <summary>
		/// Override.
		/// </summary>
		/// <param name="finalSize"></param>
		/// <returns></returns>
		protected override Size ArrangeOverride( Size finalSize )
		{
			this.child.Arrange( new Rect( finalSize ) );
			return finalSize;
		}

		/// <summary>
		/// Override.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		protected override Visual GetVisualChild( int index )
		{
			return this.child;
		}

		/// <summary>
		/// Override.
		/// </summary>
		protected override IEnumerator LogicalChildren
		{
			get
			{
				ArrayList list = new ArrayList();
				list.Add( this.child );
				return list.GetEnumerator();
			}
		}

		/// <summary>
		/// Override.  Always returns 1.
		/// </summary>
		protected override int VisualChildrenCount
		{
			get { return 1; }
		}

		#endregion // Base Class Overrides

		#region Private Helpers

		private void UpdateLocation()
		{
			AdornerLayer adornerLayer = this.Parent as AdornerLayer;
			if( adornerLayer != null )
				adornerLayer.Update( this.AdornedElement );
		}

		#endregion // Private Helpers
	}
}