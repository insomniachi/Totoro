using System.Reactive.Linq;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection.Vlc
{
    internal sealed partial class MediaPlayer : ReactiveObject, INativeMediaPlayer
    {
        private Application? _application;
        private Window? _mainWindow;
        private Slider? _slider;

        [Reactive] public double PositionPercent { get; set; }
        [Reactive] TimeSpan Duration { get; set; }

        public IObservable<TimeSpan> PositionChanged { get; }
        public IObservable<TimeSpan> DurationChanged { get; }

        public MediaPlayer()
        {
            PositionChanged = this.WhenAnyValue(x => x.PositionPercent)
                .Select(x => TimeSpan.FromSeconds(Duration.TotalSeconds * (x / 10)));

            DurationChanged = this.WhenAnyValue(x => x.Duration);
        }

        public string GetTitle()
        {
            if (_mainWindow is null)
            {
                return "";
            }

            var title = _mainWindow.Title;
            return title.Replace("- Vlc media player", string.Empty).Trim();
        }

        public void Initialize(string fileName)
        {
            _application = Application.Launch("", fileName);
            InitializeInternal();
        }

        public void Initialize(int processId)
        {
            _application = Application.Attach(processId);
            InitializeInternal();
        }

        private void InitializeInternal()
        {
            _mainWindow = _application!.GetMainWindow(new UIA3Automation());
            GetDuration();
            GetSlider();
        }

        private void GetDuration()
        {
            foreach (var item in _mainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)))
            {
                var match = DurationRegex().Match(item.Name);
                if (match.Success && item.Patterns.LegacyIAccessible.Pattern.Description.Value.Contains("Total"))
                {
                    var parts = item.Name.Split(':');

                    if(parts.Length == 3)
                    {
                        Duration = new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                    }
                    else
                    {
                        Duration = new TimeSpan(0, int.Parse(parts[0]), int.Parse(parts[1]));
                    }
                }
            }
        }

        private void GetSlider()
        {
            foreach (var item in _mainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.Slider)).Select(x => x.AsSlider()))
            {
                if (!item.Patterns.LegacyIAccessible.Pattern.Description.Value.Contains('%'))
                {
                    _slider = item;
                    var property = item.Patterns.Value.Pattern.PropertyIds.Value;
                    var handler = item.RegisterPropertyChangedEvent(TreeScope.Element, (ae, _, _) =>
                    {
                        PositionPercent = ae.AsSlider().Value;
                    }, property);
                }
            }
        }

        [GeneratedRegex(@"(\d)+:(\d)+")]
        private static partial Regex DurationRegex();
    }
}
