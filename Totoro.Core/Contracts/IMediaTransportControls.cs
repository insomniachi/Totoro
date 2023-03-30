﻿namespace Totoro.Core.Contracts;

public interface IMediaTransportControls
{
    IObservable<Unit> OnNextTrack { get; }
    IObservable<Unit> OnPrevTrack { get; }
    IObservable<Unit> OnStaticSkip { get; }
    IObservable<Unit> OnDynamicSkip { get; }
    IObservable<Unit> OnAddCc { get; }
    IObservable<string> OnQualityChanged { get; }
    IObservable<Unit> OnSubmitTimeStamp { get; }
    bool IsSkipButtonVisible { get; set; }
    bool IsNextTrackButtonVisible { get; set; }
    bool IsPreviousTrackButtonVisible { get; set; }
}
