using System.Diagnostics;
using System.Reactive.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using ReactiveUI;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.MpcHc
{
    internal sealed partial class MediaPlayer : ReactiveObject, INativeMediaPlayer, IHavePosition, ICanLaunch
    {
        private string? _customTitle;
        private MpvHcInterface _interface = null!;

        public IObservable<TimeSpan> PositionChanged { get; private set; } = null!;
        public IObservable<TimeSpan> DurationChanged { get; private set; } = null!;
        public IObservable<string> TitleChanged { get; private set; } = null!;
        public Process? Process { get; private set; }


        public Task Launch(string title, string url)
        {
            _customTitle = title;
            var app = Application.Launch(ConfigManager<Config>.Current.FileName, $"{url} /fullscreen");
            InitializeInternal(Process.GetProcessById(app.ProcessId), true);
            return Task.CompletedTask;
        }

        public Task Initialize(Window window)
        {
            Process = Process.GetProcessById(window.Properties.ProcessId);
            InitializeInternal(Process);
            return Task.CompletedTask;
        }

        private void InitializeInternal(Process process, bool hasCustomTitle = false)
        {
            _interface = new MpvHcInterface(process);

            TitleChanged = hasCustomTitle
                ? Observable.Return(_customTitle!)
                : _interface.TitleChanged;

            DurationChanged = _interface.DurationChanged;
            PositionChanged = _interface.PositionChanged;
        }

        public void Dispose()
        {
            _interface.Dispose();
        }
    }
}
