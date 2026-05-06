using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace WiinUSoft
{
    /// <summary>
    /// A Canvas which manages dragging of the UIElements it contains.
    /// </summary>
    public class DragCanvas : Canvas
    {
        private UIElement elementBeingDragged;
        private global::Windows.Foundation.Point origCursorLocation;
        private double origHorizOffset, origVertOffset;
        private bool modifyLeftOffset, modifyTopOffset;
        private bool isDragInProgress;
        private Microsoft.UI.Xaml.Input.Pointer _currentPointer;

        static DragCanvas()
        {
            AllowDraggingProperty = DependencyProperty.Register(
                "AllowDragging", typeof(bool), typeof(DragCanvas), new PropertyMetadata(true));
            AllowDragOutOfViewProperty = DependencyProperty.Register(
                "AllowDragOutOfView", typeof(bool), typeof(DragCanvas), new PropertyMetadata(false));
            CanBeDraggedProperty = DependencyProperty.RegisterAttached(
                "CanBeDragged", typeof(bool), typeof(DragCanvas), new PropertyMetadata(true));
        }

        public DragCanvas()
        {
            PointerPressed += DragCanvas_PointerPressed;
            PointerMoved += DragCanvas_PointerMoved;
            PointerReleased += DragCanvas_PointerReleased;
        }

        public static readonly DependencyProperty CanBeDraggedProperty;
        public static bool GetCanBeDragged(UIElement uiElement) => uiElement == null ? false : (bool)uiElement.GetValue(CanBeDraggedProperty);
        public static void SetCanBeDragged(UIElement uiElement, bool value) { if (uiElement != null) uiElement.SetValue(CanBeDraggedProperty, value); }

        public static readonly DependencyProperty AllowDraggingProperty;
        public bool AllowDragging
        {
            get { return (bool)GetValue(AllowDraggingProperty); }
            set { SetValue(AllowDraggingProperty, value); }
        }

        public static readonly DependencyProperty AllowDragOutOfViewProperty;
        public bool AllowDragOutOfView
        {
            get { return (bool)GetValue(AllowDragOutOfViewProperty); }
            set { SetValue(AllowDragOutOfViewProperty, value); }
        }

        public void BringToFront(UIElement element) => UpdateZOrder(element, true);
        public void SendToBack(UIElement element) => UpdateZOrder(element, false);

        public UIElement ElementBeingDragged
        {
            get => AllowDragging ? elementBeingDragged : null;
            protected set
            {
                if (elementBeingDragged != null)
                    elementBeingDragged.ReleasePointerCaptures();

                if (!AllowDragging)
                    elementBeingDragged = null;
                else if (GetCanBeDragged(value))
                {
                    elementBeingDragged = value;
                    if (_currentPointer != null)
                        elementBeingDragged.CapturePointer(_currentPointer);
                }
                else
                    elementBeingDragged = null;
            }
        }

        public UIElement FindCanvasChild(DependencyObject depObj)
        {
            while (depObj != null)
            {
                if (depObj is UIElement elem && base.Children.Contains(elem))
                    break;
                depObj = VisualTreeHelper.GetParent(depObj);
            }
            return depObj as UIElement;
        }

        private void DragCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            isDragInProgress = false;
            _currentPointer = e.Pointer;
            origCursorLocation = e.GetCurrentPoint(this).Position;
            ElementBeingDragged = FindCanvasChild(e.OriginalSource as DependencyObject);

            if (ElementBeingDragged == null) return;

            double left = Canvas.GetLeft(ElementBeingDragged);
            double top = Canvas.GetTop(ElementBeingDragged);
            origHorizOffset = double.IsNaN(left) ? 0 : left;
            origVertOffset = double.IsNaN(top) ? 0 : top;
            modifyLeftOffset = true;
            modifyTopOffset = true;
            e.Handled = true;
            isDragInProgress = true;
        }

        private void DragCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (ElementBeingDragged == null || !isDragInProgress) return;

            var cursorLocation = e.GetCurrentPoint(this).Position;
            double newH, newV;

            if (modifyLeftOffset) newH = origHorizOffset + (cursorLocation.X - origCursorLocation.X);
            else newH = origHorizOffset - (cursorLocation.X - origCursorLocation.X);
            if (modifyTopOffset) newV = origVertOffset + (cursorLocation.Y - origCursorLocation.Y);
            else newV = origVertOffset - (cursorLocation.Y - origCursorLocation.Y);

            if (!AllowDragOutOfView)
            {
                var elemRect = CalculateDragElementRect(newH, newV);
                if (elemRect.Left < 0) newH = modifyLeftOffset ? 0 : ActualWidth - elemRect.Width;
                else if (elemRect.Right > ActualWidth) newH = modifyLeftOffset ? ActualWidth - elemRect.Width : 0;
                if (elemRect.Top < 0) newV = modifyTopOffset ? 0 : ActualHeight - elemRect.Height;
                else if (elemRect.Bottom > ActualHeight) newV = modifyTopOffset ? ActualHeight - elemRect.Height : 0;
            }

            Canvas.SetLeft(ElementBeingDragged, newH);
            Canvas.SetTop(ElementBeingDragged, newV);
        }

        private void DragCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ElementBeingDragged = null;
        }

        private global::Windows.Foundation.Rect CalculateDragElementRect(double newH, double newV)
        {
            if (ElementBeingDragged == null) throw new InvalidOperationException("ElementBeingDragged is null.");
            double w = ElementBeingDragged.ActualSize.X;
            double h = ElementBeingDragged.ActualSize.Y;
            double x = modifyLeftOffset ? newH : ActualWidth - newH - w;
            double y = modifyTopOffset ? newV : ActualHeight - newV - h;
            return new global::Windows.Foundation.Rect(x, y, w, h);
        }

        private static double ResolveOffset(double side1, double side2, out bool useSide1)
        {
            useSide1 = true;
            if (double.IsNaN(side1))
            {
                if (double.IsNaN(side2)) return 0;
                useSide1 = false;
                return side2;
            }
            return side1;
        }

        private void UpdateZOrder(UIElement element, bool bringToFront)
        {
            if (element == null) throw new ArgumentNullException("element");
            if (!base.Children.Contains(element)) throw new ArgumentException("Must be a child element of the Canvas.", "element");

            int elementNewZIndex = -1;
            if (bringToFront)
            {
                foreach (UIElement elem in base.Children)
                    if (elem.Visibility != Visibility.Collapsed)
                        ++elementNewZIndex;
            }
            else elementNewZIndex = 0;

            int offset = (elementNewZIndex == 0) ? +1 : -1;
            int elementCurrentZIndex = Canvas.GetZIndex(element);

            foreach (UIElement childElement in base.Children)
            {
                if (childElement == element)
                    Canvas.SetZIndex(element, elementNewZIndex);
                else
                {
                    int zIndex = Canvas.GetZIndex(childElement);
                    if (bringToFront && elementCurrentZIndex < zIndex ||
                        !bringToFront && zIndex < elementCurrentZIndex)
                        Canvas.SetZIndex(childElement, zIndex + offset);
                }
            }
        }
    }
}
