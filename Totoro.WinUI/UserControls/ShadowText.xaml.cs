using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Totoro.WinUI.UserControls
{
    public sealed partial class ShadowText : UserControl
    {
        public static DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(ShadowText), new PropertyMetadata(""));
        public static DependencyProperty ShadowColorProperty = DependencyProperty.Register(nameof(ShadowColor), typeof(Color), typeof(ShadowText), new PropertyMetadata(Color.FromArgb(255, 190, 87, 199)));
        public static DependencyProperty ShadowRadiusProperty = DependencyProperty.Register(nameof(ShadowRadius), typeof(float), typeof(ShadowText), new PropertyMetadata(20f));
        public static DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(nameof(ShadowOpacity), typeof(float), typeof(ShadowText), new PropertyMetadata(20f));

        public string Text
        {
            get => (string)this.GetValue(TextProperty);
            set => this.SetValue(TextProperty, value);
        }

        public Color ShadowColor
        {
            get => (Color)this.GetValue(ShadowColorProperty);
            set => this.SetValue(ShadowColorProperty, value);
        }

        public float ShadowRadius
        {
            get => (float)this.GetValue(ShadowRadiusProperty);
            set => this.SetValue(ShadowRadiusProperty, value);
        }

        public float ShadowOpacity
        {
            get => (float)this.GetValue(ShadowOpacityProperty);
            set => this.SetValue(ShadowOpacityProperty, value);
        }

        private void MakeShadow()
        {
            var compositor = ElementCompositionPreview.GetElementVisual(this.Host).Compositor;

            var dropShadow = compositor.CreateDropShadow();
            dropShadow.Color = ShadowColor;
            dropShadow.BlurRadius = this.ShadowRadius;
            dropShadow.Opacity = this.ShadowOpacity;

            var mask = this.TextBlock.GetAlphaMask();
            dropShadow.Mask = mask;

            var spriteVisual = compositor.CreateSpriteVisual();
            spriteVisual.Size = new Vector2((float)this.Host.ActualWidth, (float)this.Host.ActualHeight);
            spriteVisual.Shadow = dropShadow;
            ElementCompositionPreview.SetElementChildVisual(this.Host, spriteVisual);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.MakeShadow();
        }

        public ShadowText()
        {
            this.InitializeComponent();
        }
    }
}
