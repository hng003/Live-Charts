﻿//The MIT License(MIT)

//copyright(c) 2016 Alberto Rodriguez

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using LiveCharts.Defaults;

namespace LiveCharts.Charts
{
    public abstract class ChartCore
    {

        #region Contructors
        protected ChartCore(IChartView view, IChartUpdater updater)
        {
            View = view;
            Updater = updater;
            DrawMargin = new CoreRectangle();
            DrawMargin.SetHeight += view.SetDrawMarginHeight;
            DrawMargin.SetWidth += view.SetDrawMarginWidth;
            DrawMargin.SetTop += view.SetDrawMarginTop;
            DrawMargin.SetLeft += view.SetDrawMarginLeft;
        }

        static ChartCore()
        {
            Configurations = new SeriesConfigurations();
        }

        #endregion

        #region Properties 

        public static SeriesConfigurations Configurations { get; set; }
        public bool SeriesInitialized { get; set; }
        public IChartView View { get; set; }
        public IChartUpdater Updater { get; set; }
        public CoreSize ChartControlSize { get; set; }
        public CoreRectangle DrawMargin { get; set; }
        public bool HasUnitaryPoints { get; set; }
        
        public List<AxisCore> AxisX { get; set; }
        public List<AxisCore> AxisY { get; set; }

        public CoreLimit Value1CoreLimit { get; set; }
        public CoreLimit Value2CoreLimit { get; set; }
        public CoreLimit Value3CoreLimit { get; set; }

        public int CurrentColorIndex { get; set; }

        public AxisTags PivotZoomingAxis { get; set; }
        public CorePoint PanOrigin { get; set; }

        private bool IsDragging { get; set; }

        private bool IsZooming
        {
            get
            {
                var animationsSpeed = View.DisableAnimations ? 0 : View.AnimationsSpeed.TotalMilliseconds;
                return (DateTime.Now - RequestedZoomAt).TotalMilliseconds < animationsSpeed;
            }
        }

        private DateTime RequestedZoomAt { get; set; }

        #endregion

        #region Public Methods

        public virtual void PrepareAxes()
        {

            for (var index = 0; index < AxisX.Count; index++)
            {
                var xi = AxisX[index];

                xi.MaxLimit = xi.MaxValue ??
                              View.Series.Where(series => series.Values != null && series.ScalesXAt == index)
                                  .Select(series => series.Values.XLimit.Max).DefaultIfEmpty(0).Max();
                xi.MinLimit = xi.MinValue ??
                              View.Series.Where(series => series.Values != null && series.ScalesXAt == index)
                                  .Select(series => series.Values.XLimit.Min).DefaultIfEmpty(0).Min();
            }

            for (var index = 0; index < AxisY.Count; index++)
            {
                var yi = AxisY[index];

                yi.MaxLimit = yi.MaxValue ??
                             View.Series.Where(series => series.Values != null && series.ScalesYAt == index)
                                  .Select(series => series.Values.YLimit.Max).DefaultIfEmpty(0).Max();
                yi.MinLimit = yi.MinValue ??
                              View.Series.Where(series => series.Values != null && series.ScalesYAt == index)
                                  .Select(series => series.Values.YLimit.Min).DefaultIfEmpty(0).Min();
            }

            PivotZoomingAxis = AxisTags.X;
        }

        public void CalculateComponentsAndMargin()
        {
            var curSize = new CoreRectangle(0, 0, ChartControlSize.Width, ChartControlSize.Height);

            curSize = PlaceLegend(curSize);

            double t = curSize.Top,
                b = 0d,
                bm = 0d,
                l = curSize.Left,
                r = 0d;

            foreach (var yi in AxisY)
            {
                var titleSize = yi.View.UpdateTitle(this, -90d);
                var biggest = yi.PrepareChart(AxisTags.Y, this);

                var x = curSize.Left;
                var merged = yi.IsMerged ? 0 : biggest.Width + 2;
                if (yi.Position == AxisPosition.LeftBottom)
                {
                    yi.View.SetTitleLeft(x);
                    yi.View.LabelsReference = x + titleSize.Height + merged;
                    curSize.Left = curSize.Left + titleSize.Height + merged;
                    curSize.Width -= (titleSize.Height + merged);
                }
                else
                {
                    yi.View.SetTitleLeft(x + curSize.Width - titleSize.Height);
                    yi.View.LabelsReference = x + curSize.Width - titleSize.Height - merged;
                    curSize.Width -= (titleSize.Height + merged);
                }

                var top = yi.IsMerged ? 0 : biggest.Height*.5;
                if (t < top) t = top;

                var bot = yi.IsMerged ? 0 : biggest.Height*.5;
                if (b < bot) b = bot;

                if (yi.IsMerged && bm < biggest.Height)
                    bm = biggest.Height;
            }

            if (t > 0)
            {
                curSize.Top = t;
                curSize.Height -= t;
            }
            if (b > 0 && !(AxisX.Count > 0))
                curSize.Height = curSize.Height - b;

            foreach (var xi in AxisX)
            {
                var titleSize = xi.View.UpdateTitle(this);
                var biggest = xi.PrepareChart(AxisTags.X, this);
                var top = curSize.Top;
                var merged = xi.IsMerged ? 0 : biggest.Height;
                if (xi.Position == AxisPosition.LeftBottom)
                {
                    xi.View.SetTitleTop(top + curSize.Height - titleSize.Height);
                    xi.View.LabelsReference = top + b - (xi.IsMerged ? bm : 0) +
                                         (curSize.Height - (titleSize.Height + merged + b)) -
                                         (xi.IsMerged ? b : 0);
                    curSize.Height -= (titleSize.Height + merged + b);
                }
                else
                {
                    xi.View.SetTitleTop(top - t);
                    xi.View.LabelsReference = (top - t) + titleSize.Height + (xi.IsMerged ? bm : 0);
                    curSize.Top = curSize.Top + titleSize.Height + merged;
                    curSize.Height -= (titleSize.Height + merged);
                }

                var left = xi.IsMerged ? 0 : biggest.Width*.5;
                if (l < left) l = left;

                var right = xi.IsMerged ? 0 : biggest.Width*.5;
                if (r < right) r = right;
            }

            if (curSize.Left < l)
            {
                var cor = l - curSize.Left;
                curSize.Left = l;
                curSize.Width -= cor;
                foreach (var yi in AxisY.Where(x => x.Position == AxisPosition.LeftBottom))
                {
                    yi.View.SetTitleLeft(yi.View.GetTitleLeft() + cor);
                    yi.View.LabelsReference += cor;
                }
            }
            var rp = ChartControlSize.Width - curSize.Left - curSize.Width;
            if (r > rp)
            {
                var cor = r - rp;
                curSize.Width -= cor;
                foreach (var yi in AxisY.Where(x => x.Position == AxisPosition.RightTop))
                {
                    yi.View.SetTitleLeft(yi.View.GetTitleLeft() - cor);
                    yi.View.LabelsReference -= cor;
                }
            }

            DrawMargin.Top = curSize.Top;
            DrawMargin.Left = curSize.Left;
            DrawMargin.Width = curSize.Width;
            DrawMargin.Height = curSize.Height;

            for (var index = 0; index < AxisY.Count; index++)
            {
                var yi = AxisY[index];
                if (HasUnitaryPoints)
                    yi.View.UnitWidth = ChartFunctions.GetUnitWidth(AxisTags.Y, this, index);
                yi.UpdateSeparators(AxisTags.Y, this, index);
                yi.View.SetTitleTop(curSize.Top + curSize.Height*.5 + yi.View.GetLabelSize().Width*.5);
            }

            for (var index = 0; index < AxisX.Count; index++)
            {
                var xi = AxisX[index];
                if (HasUnitaryPoints)
                    xi.View.UnitWidth = ChartFunctions.GetUnitWidth(AxisTags.X, this, index);
                xi.UpdateSeparators(AxisTags.X, this, index);
                xi.View.SetTitleLeft(curSize.Left + curSize.Width*.5 - xi.View.GetLabelSize().Width*.5);
            }
        }

        public CoreRectangle PlaceLegend(CoreRectangle drawMargin)
        {
            var legendSize = View.LoadLegend();

            const int padding = 10;

            switch (View.LegendLocation)
            {
                case LegendLocation.None:
                    View.HideLegend();
                    break;
                case LegendLocation.Top:
                    var top = new CorePoint(ChartControlSize.Width * .5 - legendSize.Width * .5, 0);
                    var y = drawMargin.Top;
                    drawMargin.Top = y + top.Y + legendSize.Height + padding;
                    drawMargin.Height -= legendSize.Height - padding;
                    View.ShowLegend(top);
                    break;
                case LegendLocation.Bottom:
                    var bot = new CorePoint(ChartControlSize.Width*.5 - legendSize.Width*.5,
                        ChartControlSize.Height - legendSize.Height);
                    drawMargin.Height -= legendSize.Height;
                    View.ShowLegend(new CorePoint(bot.X, ChartControlSize.Height - legendSize.Height));
                    break;
                case LegendLocation.Left:
                    drawMargin.Left = drawMargin.Left + legendSize.Width;
                    View.ShowLegend(new CorePoint(0, ChartControlSize.Height*.5 - legendSize.Height*.5));
                    break;
                case LegendLocation.Right:
                    drawMargin.Width -= legendSize.Width + padding;
                    View.ShowLegend(new CorePoint(ChartControlSize.Width - legendSize.Width,
                        ChartControlSize.Height*.5 - legendSize.Height*.5));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return drawMargin;
        }

        public void ZoomIn(CorePoint pivot)
        {
            View.HideTooltop();

            if (IsZooming) return;

            RequestedZoomAt = DateTime.Now;

            if (View.Zoom == ZoomingOptions.X || View.Zoom == ZoomingOptions.Xy)
            {
                foreach (var xi in AxisX)
                {
                    var max = xi.MaxValue ?? xi.MaxLimit;
                    var min = xi.MinValue ?? xi.MinLimit;
                    var l = max - min;
                    var rMin = (pivot.X - min) / l;
                    var rMax = 1 - rMin;

                    xi.MinValue = min + rMin * xi.S;
                    xi.MaxValue = max - rMax * xi.S;
                }
            }
            else
            {
                foreach (var xi in AxisX)
                {
                    xi.MinValue = null;
                    xi.MaxValue = null;
                }
            }

            if (View.Zoom == ZoomingOptions.Y || View.Zoom == ZoomingOptions.Xy)
            {
                foreach (var yi in AxisY)
                {
                    var max = yi.MaxValue ?? yi.MaxLimit;
                    var min = yi.MinValue ?? yi.MinLimit;
                    var l = max - min;
                    var rMin = (pivot.Y - min) / l;
                    var rMax = 1 - rMin;

                    yi.MinValue = min + rMin * yi.S;
                    yi.MaxValue = max - rMax * yi.S;
                }
            }
            else
            {
                foreach (var yi in AxisY)
                {
                    yi.MinValue = null;
                    yi.MaxValue = null;
                }
            }

            Updater.Run();
        }

        public void ZoomOut(CorePoint pivot)
        {
            View.HideTooltop();

            if (IsZooming) return;

            RequestedZoomAt = DateTime.Now;

            var dataPivot = new CorePoint(
                ChartFunctions.FromDrawMargin(pivot.X, AxisTags.X, this),
                ChartFunctions.FromDrawMargin(pivot.Y, AxisTags.Y, this));

            if (View.Zoom == ZoomingOptions.X || View.Zoom == ZoomingOptions.Xy)
            {
                foreach (var xi in AxisX)
                {
                    var max = xi.MaxValue ?? xi.MaxLimit;
                    var min = xi.MinValue ?? xi.MinLimit;
                    var l = max - min;
                    var rMin = (dataPivot.X - min) / l;
                    var rMax = 1 - rMin;

                    xi.MinValue = min - rMin * xi.S;
                    xi.MaxValue = max + rMax * xi.S;
                }
            }

            if (View.Zoom == ZoomingOptions.Y || View.Zoom == ZoomingOptions.Xy)
            {
                foreach (var yi in AxisY)
                {
                    var max = yi.MaxValue ?? yi.MaxLimit;
                    var min = yi.MinValue ?? yi.MinLimit;
                    var l = max - min;
                    var rMin = (dataPivot.Y - min) / l;
                    var rMax = 1 - rMin;

                    yi.MinValue = min - rMin * yi.S;
                    yi.MaxValue = max + rMax * yi.S;
                }
            }

            Updater.Run();
        }

        public void ClearZoom()
        {
            foreach (var xi in AxisX)
            {
                xi.MinValue = null;
                xi.MaxValue = null;
            }

            foreach (var yi in AxisY)
            {
                yi.MinValue = null;
                yi.MaxValue = null;
            }

            Updater.Run();
        }
        #endregion
    }
}