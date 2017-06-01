using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CryPixivClient.Windows
{
    public partial class PopUp : UserControl
    {
        public enum ArrowPosition
        {
            UpLeft,
            UpRight,
            UpMiddle,
            DownLeft,
            DownRight,
            DownMiddle,
            None
        }

        public enum TransitionType
        {
            UpDown,
            ZoomIn
        }

        public bool IsHidden { get; private set; }
        public event EventHandler<MouseEventArgs> ClickedOnMinimizedArea;

        public PopUp() : this(ArrowPosition.UpLeft) { }
        public PopUp(ArrowPosition position)
        {
            InitializeComponent();

            SetArrow(position);
            SetControls();
        }

        public void AddContent(params UIElement[] elements)
        {
            foreach (var e in elements) _contentGrid.Children.Add(e);
        }
        
        public void SetArrow(ArrowPosition position)
        {
            // default settings
            _arrow.Points = new PointCollection(new Point[] { new Point(0, 0), new Point(40, 0), new Point(20, -25) });
            var altPoints = new PointCollection(new Point[] { new Point(0, 0), new Point(40, 0), new Point(20, 25) });
            Grid.SetColumn(_arrow, 0);

            switch (position)
            {
                case ArrowPosition.UpLeft:
                    _arrow.Margin = new Thickness(0, 26, 120, 0);                    
                    break;
                case ArrowPosition.UpMiddle:
                    _arrow.Margin = new Thickness(0, 26, 1, 0);
                    break;
                case ArrowPosition.UpRight:
                    _arrow.Margin = new Thickness(0, 26, 52, 0);
                    Grid.SetColumn(_arrow, 1);
                    break;
                case ArrowPosition.DownLeft:
                    _arrow.Margin = new Thickness(56, 0, 0, 9);
                    _arrow.Points = altPoints;
                    break;
                case ArrowPosition.DownMiddle:
                    _arrow.Margin = new Thickness(0, 0, 0, 9);
                    _arrow.Points = altPoints;
                    break;
                case ArrowPosition.DownRight:
                    _arrow.Margin = new Thickness(0, 0, 50, 9);
                    _arrow.Points = altPoints;
                    Grid.SetColumn(_arrow, 1);
                    break;
                case ArrowPosition.None:
                    _arrow.Visibility = Visibility.Hidden;
                    break;
            }
        }

        public void SetMinimizeArea(bool isEnabled)
        {
            _minimizeArea.IsEnabled = IsEnabled;
        }

        void SetControls()
        {
            IsHidden = true;
            _arrow.Opacity = 0.0;
            _contentGrid.Opacity = 0.0;
            _contentGrid.Margin = new Thickness(10, 26, 10, 211);
        }

        public void Show(TransitionType transition = TransitionType.UpDown)
        {
            if (IsHidden == false) return;
            this.IsEnabled = true;

            DoubleAnimation dan = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.3));
            _arrow.BeginAnimation(OpacityProperty, dan);
            _contentGrid.BeginAnimation(OpacityProperty, dan);

            if (transition == TransitionType.UpDown)
            {
                ThicknessAnimation tan = new ThicknessAnimation(new Thickness(10, 26, 10, 34), TimeSpan.FromSeconds(0.6));
                tan.EasingFunction = new PowerEase() { Power = 2 };
                _contentGrid.BeginAnimation(MarginProperty, tan);
            }
            else
            {
                _contentGrid.Margin = new Thickness(10, 26, 10, 34);

                _mainGrid.RenderTransformOrigin = new Point(0.5, 0.5);
                scaleTransform.ScaleX = 0.6;
                scaleTransform.ScaleY = 0.6;

                DoubleAnimation dan2 = new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.6));
                dan2.EasingFunction = new PowerEase() { Power = 2 };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, dan2);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, dan2);
            }

            IsHidden = false;
        }
        public void Hide(TransitionType transition = TransitionType.UpDown)
        {
            if (IsHidden) return;
            this.IsEnabled = false;

            DoubleAnimation dan = new DoubleAnimation(0.0, TimeSpan.FromSeconds(0.4));
            _arrow.BeginAnimation(OpacityProperty, dan);
            _contentGrid.BeginAnimation(OpacityProperty, dan);

            if (transition == TransitionType.UpDown)
            {
                ThicknessAnimation tan = new ThicknessAnimation(new Thickness(10, 26, 10, 211), TimeSpan.FromSeconds(0.6));
                tan.EasingFunction = new PowerEase() { Power = 2 };
                _contentGrid.BeginAnimation(MarginProperty, tan);
            }
            else
            {
                DoubleAnimation dan2 = new DoubleAnimation(0.6, TimeSpan.FromSeconds(0.6));
                dan2.EasingFunction = new PowerEase() { Power = 2 };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, dan2);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, dan2);
            }

            IsHidden = true;
        }

        private void _minimizeArea_MouseDown(object sender, MouseEventArgs e)
        {
            ClickedOnMinimizedArea?.Invoke(this, e);
        }
    }
}
