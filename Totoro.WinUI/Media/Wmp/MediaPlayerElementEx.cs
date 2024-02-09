using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using ReactiveMarbles.ObservableEvents;
using Totoro.WinUI.Contracts;

namespace Totoro.WinUI.Media.Wmp;

public class MediaPlayerElementEx : MediaPlayerElement
{
    public MediaPlayerElementEx()
    {
        //ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        //var windowService = App.GetService<IWindowService>(); 

        //this.Events()
        //    .PointerMoved
        //    .Subscribe(_ => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow));

        //this.Events()
        //    .PointerMoved
        //    .Throttle(TimeSpan.FromSeconds(3))
        //    .ObserveOn(RxApp.MainThreadScheduler)
        //    .Subscribe(_ =>
        //    {
        //        if (windowService.IsFullWindow)
        //        {
        //            ProtectedCursor.Dispose();
        //        }

        //    }, RxApp.DefaultExceptionHandler.OnError);
    }
}
