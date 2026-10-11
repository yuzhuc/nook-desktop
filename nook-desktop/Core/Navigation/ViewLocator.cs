using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using nook_desktop.Core.Mvvm;

namespace nook_desktop.Core.Navigation;

/// <summary>
/// ViewModel 到 View 的约定式映射：Foo.BarViewModel -> Foo.BarView。
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
        {
            return new TextBlock { Text = "(null)" };
        }

        var viewModelName = data.GetType().FullName!;
        var viewName = viewModelName
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        var viewType = Type.GetType(viewName);
        if (viewType is null)
        {
            return new TextBlock { Text = $"View not found: {viewName}" };
        }

        return (Control)Activator.CreateInstance(viewType)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
