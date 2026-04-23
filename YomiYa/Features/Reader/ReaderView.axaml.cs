using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using YomiYa.Domain.Enums;
using YomiYa.Helper.Input.Classes;
using YomiYa.Helper.Input.Interfaces;

namespace YomiYa.Features.Reader;

public partial class ReaderView : Window
{
    private readonly DispatcherTimer _scrollDebounceTimer;
    private DispatcherTimer? _idleTimer;
    private bool _isProgrammaticScroll;
    private ReaderKeyboardHelper? _keyboardHelper;

    [Obsolete("Obsolete")]
    public ReaderView()
    {
        InitializeComponent();
        SetupIdleTimer();
        DataContextChanged += OnDataContextChanged;

        var cascadeScrollViewer = this.FindControl<ScrollViewer>("CascadeScrollViewer");
        if (cascadeScrollViewer != null) cascadeScrollViewer.ScrollChanged += CascadeScrollViewer_ScrollChanged;

        _scrollDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _scrollDebounceTimer.Tick += ScrollDebounceTimer_Tick;
    }

    [Obsolete("Obsolete")]
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ReaderViewModel oldVm) oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        _keyboardHelper?.Dispose();

        if (DataContext is IKeyboardNavigable navigableViewModel)
        {
            _keyboardHelper = new ReaderKeyboardHelper(this, navigableViewModel);
            if (DataContext is ReaderViewModel newVm) newVm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void CascadeScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isProgrammaticScroll) return;
        _scrollDebounceTimer.Stop();
        _scrollDebounceTimer.Start();
    }

    [Obsolete("Obsolete")]
    private void ScrollDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _scrollDebounceTimer.Stop();
        if (DataContext is not ReaderViewModel vm) return;

        UpdateCurrentPageFromScrollPosition(vm);
    }

    [Obsolete("Obsolete")]
    private void UpdateCurrentPageFromScrollPosition(ReaderViewModel vm)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("CascadeScrollViewer");
        if (scrollViewer is null) return;

        var viewportBounds = new Rect(scrollViewer.Bounds.Size);
        Control? containerWithMaxVisibility = null;
        double maxVisibleArea = 0;

        foreach (var container in CascadeItemsControl.GetRealizedContainers())
        {
            var transform = container.TransformToVisual(scrollViewer);
            if (transform == null) continue;

            var containerBounds = new Rect(container.Bounds.Size).TransformToAABB(transform.Value);
            var intersection = viewportBounds.Intersect(containerBounds);

            if (!(intersection.Width > 0) || !(intersection.Height > 0)) continue;
            var visibleArea = intersection.Width * intersection.Height;

            if (!(visibleArea > maxVisibleArea)) continue;
            maxVisibleArea = visibleArea;
            containerWithMaxVisibility = container;
        }

        if (containerWithMaxVisibility == null) return;

        var newIndex = CascadeItemsControl.IndexFromContainer(containerWithMaxVisibility);

        if (newIndex != -1 && vm.CurrentPageIndex != newIndex) vm.CurrentPageIndex = newIndex;
    }

    [Obsolete("Obsolete")]
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not ReaderViewModel vm) return;

        if ((e.PropertyName == nameof(ReaderViewModel.ReadingMode) ||
             e.PropertyName == nameof(ReaderViewModel.ReadingWidth)) && vm.ReadingMode == ReadingMode.Cascade)
            Dispatcher.UIThread.Post(() =>
            {
                if (CascadeItemsControl.ContainerFromIndex(vm.CurrentPageIndex) is { } container)
                {
                    _isProgrammaticScroll = true;
                    container.BringIntoView();
                    Dispatcher.UIThread.Post(() => { _isProgrammaticScroll = false; }, DispatcherPriority.Loaded);
                }
            });
    }

    private void SetupIdleTimer()
    {
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _idleTimer.Tick += (s, e) =>
        {
            ReaderContainerGrid.Classes.Remove("controls-visible");
            _idleTimer.Stop();
        };
        ReaderContainerGrid.PointerMoved += (s, e) =>
        {
            ReaderContainerGrid.Classes.Add("controls-visible");
            _idleTimer.Stop();
            _idleTimer.Start();
        };
        ReaderContainerGrid.PointerExited += (s, e) =>
        {
            ReaderContainerGrid.Classes.Remove("controls-visible");
            _idleTimer.Stop();
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _keyboardHelper?.Dispose();
        if (DataContext is IDisposable disposable) disposable.Dispose();
        base.OnClosed(e);
    }
}