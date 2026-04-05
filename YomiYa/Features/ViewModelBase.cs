using System;
using CommunityToolkit.Mvvm.ComponentModel;
using YomiYa.Core.Localization;

namespace YomiYa.Features;

public abstract class ViewModelBase : ObservableObject
{
    protected ViewModelBase()
    {
        LanguageHelper.LanguageChanged += LanguageHelper_LanguageChanged;
    }

    private void LanguageHelper_LanguageChanged(object? sender, EventArgs e)
    {
        UpdateLocalizedTexts();
    }

    ~ViewModelBase()
    {
        LanguageHelper.LanguageChanged -= LanguageHelper_LanguageChanged;
    }

    protected abstract void UpdateLocalizedTexts();
}