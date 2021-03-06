﻿using Newtonsoft.Json.Linq;
using ReactNative.Touch;
using ReactNative.UIManager.Annotations;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Media3D;

namespace ReactNative.UIManager
{
    /// <summary>
    /// Base class that should be suitable for the majority of subclasses of <see cref="IViewManager"/>.
    /// It provides support for base view properties such as opacity, etc.
    /// </summary>
    /// <typeparam name="TFrameworkElement">Type of framework element.</typeparam>
    /// <typeparam name="TLayoutShadowNode">Type of shadow node.</typeparam>
    public abstract class BaseViewManager<TFrameworkElement, TLayoutShadowNode> :
            ViewManager<TFrameworkElement, TLayoutShadowNode>
        where TFrameworkElement : FrameworkElement
        where TLayoutShadowNode : LayoutShadowNode
    {
        private readonly IDictionary<TFrameworkElement, DimensionBoundProperties> _dimensionBoundProperties =
            new Dictionary<TFrameworkElement, DimensionBoundProperties>();

        /// <summary>
        /// Set's the  <typeparamref name="TFrameworkElement"/> styling layout 
        /// properties, based on the <see cref="JObject"/> map.
        /// </summary>
        /// <param name="view">The view instance.</param>
        /// <param name="transforms">The list of transforms.</param>
        [ReactProp("transform")]
        public void SetTransform(TFrameworkElement view, JArray transforms)
        {
            if (transforms == null)
            {
                var dimensionBoundProperties = GetDimensionBoundProperties(view);
                if (dimensionBoundProperties?.MatrixTransform != null)
                {
                    dimensionBoundProperties.MatrixTransform = null;
                    ResetProjectionMatrix(view);
                    ResetRenderTransform(view);
                }
            }
            else
            {
                var dimensionBoundProperties = GetOrCreateDimensionBoundProperties(view);
                dimensionBoundProperties.MatrixTransform = transforms;
                var dimensions = GetDimensions(view);
                SetProjectionMatrix(view, dimensions, transforms);
            }
        }

        /// <summary>
        /// Sets the opacity of the <typeparamref name="TFrameworkElement"/>.
        /// </summary>
        /// <param name="view">The view instance.</param>
        /// <param name="opacity">The opacity value.</param>
        [ReactProp("opacity", DefaultDouble = 1.0)]
        public void SetOpacity(TFrameworkElement view, double opacity)
        {
            view.Opacity = opacity;
        }

        /// <summary>
        /// Sets the overflow property for the <typeparamref name="TFrameworkElement"/>.
        /// </summary>
        /// <param name="view">The view instance.</param>
        /// <param name="overflow">The overflow value.</param>
        [ReactProp("overflow")]
        public void SetOverflow(TFrameworkElement view, string overflow)
        {
            if (overflow == "hidden")
            {
                var dimensionBoundProperties = GetOrCreateDimensionBoundProperties(view);
                dimensionBoundProperties.OverflowHidden = true;
                var dimensions = GetDimensions(view);
                SetOverflowHidden(view, dimensions);
            }
            else
            {
                var dimensionBoundProperties = GetDimensionBoundProperties(view);
                if (dimensionBoundProperties != null && dimensionBoundProperties.OverflowHidden)
                {
                    dimensionBoundProperties.OverflowHidden = false;
                    SetOverflowVisible(view);
                }
            }
        }

        /// <summary>
        /// Sets the z-index of the element.
        /// </summary>
        /// <param name="view">The view instance.</param>
        /// <param name="zIndex">The z-index.</param>
        [ReactProp("zIndex")]
        public void SetZIndex(TFrameworkElement view, int zIndex)
        {
            Canvas.SetZIndex(view, zIndex);
        }

        /// <summary>
        /// Sets the accessibility label of the element.
        /// </summary>
        /// <param name="view">The view instance.</param>
        /// <param name="label">The label.</param>
        [ReactProp("accessibilityLabel")]
        public void SetAccessibilityLabel(TFrameworkElement view, string label)
        {
            AutomationProperties.SetName(view, label ?? "");
        }

        /// <summary>
        /// Sets the accessibility live region.
        /// </summary>
        /// <param name="view">The view instance.</param>
        /// <param name="liveRegion">The live region.</param>
        [ReactProp("accessibilityLiveRegion")]
        public void SetAccessibilityLiveRegion(TFrameworkElement view, string liveRegion)
        {
            var liveSetting = AutomationLiveSetting.Off;
            switch (liveRegion)
            {
                case "polite":
                    liveSetting = AutomationLiveSetting.Polite;
                    break;
                case "assertive":
                    liveSetting = AutomationLiveSetting.Assertive;
                    break;
            }

            AutomationProperties.SetLiveSetting(view, liveSetting);
        }

        /// <summary>
        /// Sets the test ID, i.e., the automation ID.
        /// </summary>
        /// <param name="view">The view instance.</param>
        /// <param name="testId">The test ID.</param>
        [ReactProp("testID")]
        public void SetTestId(TFrameworkElement view, string testId)
        {
            AutomationProperties.SetAutomationId(view, testId ?? "");
        }

        /// <summary>
        /// Called when view is detached from view hierarchy and allows for 
        /// additional cleanup by the <see cref="IViewManager"/> subclass.
        /// </summary>
        /// <param name="reactContext">The React context.</param>
        /// <param name="view">The view.</param>
        /// <remarks>
        /// Be sure to call this base class method to register for pointer 
        /// entered and pointer exited events.
        /// </remarks>
        public override void OnDropViewInstance(ThemedReactContext reactContext, TFrameworkElement view)
        {
            view.PointerEntered -= OnPointerEntered;
            view.PointerExited -= OnPointerExited;
            _dimensionBoundProperties.Remove(view);
        }

        /// <summary>
        /// Sets the dimensions of the view.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="dimensions">The dimensions.</param>
        public override void SetDimensions(TFrameworkElement view, Dimensions dimensions)
        {
            var dimensionBoundProperties = GetDimensionBoundProperties(view);
            var matrixTransform = dimensionBoundProperties?.MatrixTransform;
            var overflowHidden = dimensionBoundProperties?.OverflowHidden ?? false;
            if (matrixTransform != null)
            {
                SetProjectionMatrix(view, dimensions, matrixTransform);
            }

            if (overflowHidden)
            {
                SetOverflowHidden(view, dimensions);
            }

            base.SetDimensions(view, dimensions);
        }

        /// <summary>
        /// Subclasses can override this method to install custom event 
        /// emitters on the given view.
        /// </summary>
        /// <param name="reactContext">The React context.</param>
        /// <param name="view">The view instance.</param>
        /// <remarks>
        /// Consider overriding this method if your view needs to emit events
        /// besides basic touch events to JavaScript (e.g., scroll events).
        /// 
        /// Make sure you call the base implementation to ensure base pointer
        /// event handlers are subscribed.
        /// </remarks>
        protected override void AddEventEmitters(ThemedReactContext reactContext, TFrameworkElement view)
        {
            view.PointerEntered += OnPointerEntered;
            view.PointerExited += OnPointerExited;
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var view = (TFrameworkElement)sender;
            TouchHandler.OnPointerEntered(view, e);
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            var view = (TFrameworkElement)sender;
            TouchHandler.OnPointerExited(view, e);
        }

        private DimensionBoundProperties GetDimensionBoundProperties(TFrameworkElement view)
        {
            DimensionBoundProperties properties;
            if (!_dimensionBoundProperties.TryGetValue(view, out properties))
            {
                properties = null;
            }

            return properties;
        }

        private DimensionBoundProperties GetOrCreateDimensionBoundProperties(TFrameworkElement view)
        {
            DimensionBoundProperties properties;
            if (!_dimensionBoundProperties.TryGetValue(view, out properties))
            {
                properties = new DimensionBoundProperties();
                _dimensionBoundProperties.Add(view, properties);
            }

            return properties;
        }

        private static void SetProjectionMatrix(TFrameworkElement view, Dimensions dimensions, JArray transforms)
        {
            var transformMatrix = TransformHelper.ProcessTransform(transforms);

            var translateMatrix = Matrix3D.Identity;
            var translateBackMatrix = Matrix3D.Identity;
            if (!double.IsNaN(dimensions.Width))
            {
                translateMatrix.OffsetX = -dimensions.Width / 2;
                translateBackMatrix.OffsetX = dimensions.Width / 2;
            }

            if (!double.IsNaN(dimensions.Height))
            {
                translateMatrix.OffsetY = -dimensions.Height / 2;
                translateBackMatrix.OffsetY = dimensions.Height / 2;
            }

            var projectionMatrix = translateMatrix * transformMatrix * translateBackMatrix;
            ApplyProjection(view, projectionMatrix);
        }

        private static void ApplyProjection(TFrameworkElement view, Matrix3D projectionMatrix)
        {
            if (IsSimpleTranslationOnly(projectionMatrix))
            {
                ResetProjectionMatrix(view);
                var transform = EnsureMatrixTransform(view);
                var matrix = transform.Matrix;
                matrix.OffsetX = projectionMatrix.OffsetX;
                matrix.OffsetY = projectionMatrix.OffsetY;
                transform.Matrix = matrix;
            }
            else
            {
                ResetRenderTransform(view);
                var projection = EnsureProjection(view);
                projection.ProjectionMatrix = projectionMatrix;
            }
        }

        private static bool IsSimpleTranslationOnly(Matrix3D matrix)
        {
            // Matrix3D is a struct and passed-by-value. As such, we can modify
            // the values in the matrix without affecting the caller.
            matrix.OffsetX = matrix.OffsetY = 0;
            return matrix.IsIdentity;
        }

        private static void ResetProjectionMatrix(TFrameworkElement view)
        {
            var projection = view.Projection;
            var matrixProjection = projection as Matrix3DProjection;
            if (projection != null && matrixProjection == null)
            {
                throw new InvalidOperationException("Unknown projection set on framework element.");
            }

            view.Projection = null;
        }

        private static void ResetRenderTransform(TFrameworkElement view)
        {
            var transform = view.RenderTransform;
            var matrixTransform = transform as MatrixTransform;
            if (transform != null && matrixTransform == null)
            {
                throw new InvalidOperationException("Unknown transform set on framework element.");
            }

            view.RenderTransform = null;
        }

        private static MatrixTransform EnsureMatrixTransform(FrameworkElement view)
        {
            var transform = view.RenderTransform;
            var matrixTransform = transform as MatrixTransform;
            if (transform != null && matrixTransform == null)
            {
                throw new InvalidOperationException("Unknown transform set on framework element.");
            }

            if (matrixTransform == null)
            {
                matrixTransform = new MatrixTransform();
                view.RenderTransform = matrixTransform;
            }

            return matrixTransform;
        }

        private static Matrix3DProjection EnsureProjection(FrameworkElement view)
        {
            var projection = view.Projection;
            var matrixProjection = projection as Matrix3DProjection;
            if (projection != null && matrixProjection == null)
            {
                throw new InvalidOperationException("Unknown projection set on framework element.");
            }

            if (matrixProjection == null)
            {
                matrixProjection = new Matrix3DProjection();
                view.Projection = matrixProjection;
            }

            return matrixProjection;
        }

        private static void SetOverflowHidden(TFrameworkElement element, Dimensions dimensions)
        {
            if (double.IsNaN(dimensions.Width) || double.IsNaN(dimensions.Height))
            {
                element.Clip = null;
            }
            else
            {
                element.Clip = new RectangleGeometry
                {
                    Rect = new Rect
                    {
                        X = 0,
                        Y = 0,
                        Width = dimensions.Width,
                        Height = dimensions.Height,
                    },
                };
            }
        }

        private static void SetOverflowVisible(TFrameworkElement element)
        {
            element.Clip = null;
        }

        class DimensionBoundProperties
        {
            public bool OverflowHidden { get; set; }

            public JArray MatrixTransform { get; set; }
        }
    }
}
