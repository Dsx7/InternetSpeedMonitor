using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using NetPulseTaskbar.Models;

namespace NetPulseTaskbar.Controls
{
    public class SparklineCanvas : FrameworkElement
    {
        public static readonly DependencyProperty DownloadBrushProperty =
            DependencyProperty.Register(nameof(DownloadBrush), typeof(Brush), typeof(SparklineCanvas),
                new FrameworkPropertyMetadata(Brushes.LimeGreen, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UploadBrushProperty =
            DependencyProperty.Register(nameof(UploadBrush), typeof(Brush), typeof(SparklineCanvas),
                new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowAreaFillProperty =
            DependencyProperty.Register(nameof(ShowAreaFill), typeof(bool), typeof(SparklineCanvas),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush DownloadBrush
        {
            get => (Brush)GetValue(DownloadBrushProperty);
            set => SetValue(DownloadBrushProperty, value);
        }

        public Brush UploadBrush
        {
            get => (Brush)GetValue(UploadBrushProperty);
            set => SetValue(UploadBrushProperty, value);
        }

        public bool ShowAreaFill
        {
            get => (bool)GetValue(ShowAreaFillProperty);
            set => SetValue(ShowAreaFillProperty, value);
        }

        private readonly List<double> _downloadHistory = new();
        private readonly List<double> _uploadHistory = new();
        private const int MaxDataPoints = 60;
        private double _maxScale = 1024.0 * 100.0; // Initial 100 KB/s scale

        public void AddSample(double downloadBytesPerSec, double uploadBytesPerSec)
        {
            _downloadHistory.Add(downloadBytesPerSec);
            _uploadHistory.Add(uploadBytesPerSec);

            while (_downloadHistory.Count > MaxDataPoints) _downloadHistory.RemoveAt(0);
            while (_uploadHistory.Count > MaxDataPoints) _uploadHistory.RemoveAt(0);

            // Compute peak for auto-scaling with soft decay
            double currentPeak = 1024.0 * 50.0; // 50 KB/s floor
            for (int i = 0; i < _downloadHistory.Count; i++)
            {
                if (_downloadHistory[i] > currentPeak) currentPeak = _downloadHistory[i];
                if (_uploadHistory[i] > currentPeak) currentPeak = _uploadHistory[i];
            }

            // Smooth scaling
            _maxScale = (_maxScale * 0.9) + (currentPeak * 0.1);
            if (_maxScale < currentPeak) _maxScale = currentPeak;

            InvalidateVisual();
        }

        public void Clear()
        {
            _downloadHistory.Clear();
            _uploadHistory.Clear();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0 || _downloadHistory.Count < 2) return;

            RenderSeries(dc, _downloadHistory, DownloadBrush, width, height, 0.25);
            RenderSeries(dc, _uploadHistory, UploadBrush, width, height, 0.25);
        }

        private void RenderSeries(DrawingContext dc, List<double> values, Brush strokeBrush, double width, double height, double fillOpacity)
        {
            int count = values.Count;
            double stepX = width / (MaxDataPoints - 1);
            double startX = width - ((count - 1) * stepX);

            var lineGeometry = new StreamGeometry();
            var fillGeometry = new StreamGeometry();

            using (var lineCtx = lineGeometry.Open())
            using (var fillCtx = fillGeometry.Open())
            {
                var points = new Point[count];
                for (int i = 0; i < count; i++)
                {
                    double x = startX + (i * stepX);
                    double ratio = Math.Clamp(values[i] / _maxScale, 0.0, 1.0);
                    // Leave 2px margin top and bottom
                    double y = (height - 2) - (ratio * (height - 4));
                    points[i] = new Point(x, y);
                }

                lineCtx.BeginFigure(points[0], false, false);
                lineCtx.PolyLineTo(points, true, true);

                if (ShowAreaFill)
                {
                    fillCtx.BeginFigure(new Point(points[0].X, height), true, true);
                    fillCtx.LineTo(points[0], true, true);
                    fillCtx.PolyLineTo(points, true, true);
                    fillCtx.LineTo(new Point(points[^1].X, height), true, true);
                }
            }

            lineGeometry.Freeze();
            fillGeometry.Freeze();

            if (ShowAreaFill)
            {
                var fillBrush = strokeBrush.Clone();
                fillBrush.Opacity = fillOpacity;
                fillBrush.Freeze();
                dc.DrawGeometry(fillBrush, null, fillGeometry);
            }

            var pen = new Pen(strokeBrush, 1.2);
            pen.Freeze();
            dc.DrawGeometry(null, pen, lineGeometry);
        }
    }
}
