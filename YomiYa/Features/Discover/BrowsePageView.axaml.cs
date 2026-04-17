using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using YomiYa.Core.Interfaces;
using YomiYa.Core.Localization;
using YomiYa.Helper.Input.Classes;
using YomiYa.Helper.Input.Interfaces;

namespace YomiYa.Features.Discover;

public partial class BrowsePageView : UserControl
{
    private SearchBoxKeyboardHelper? _searchHelper;

    public BrowsePageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _searchHelper?.Dispose();
        if (DataContext is ISearchableByKeyboard searchableViewModel)
            _searchHelper = new SearchBoxKeyboardHelper(SearchTextBox, searchableViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _searchHelper?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    // NUEVO: Manejador para el botón de Ajustes
    private async void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is IConfigurableSource configurablePlugin)
        {
            // Deshabilitar temporalmente el botón para evitar múltiples clics rápidos
            btn.IsEnabled = false;

            try
            {
                // 1. Obtener la configuración actual del plugin
                var config = await configurablePlugin.GetConfigurationAsync();

                // 2. Crear el contenedor del Flyout
                var stackPanel = new StackPanel
                {
                    Spacing = 8,
                    MinWidth = 150,
                    Margin = new Thickness(10)
                };

                // Título del menú flotante
                stackPanel.Children.Add(new TextBlock
                {
                    Text = LanguageHelper.GetText("SelectLanguages") ?? "Idiomas:",
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                // 3. Generar un CheckBox por cada idioma/opción
                foreach (var kvp in config)
                {
                    var checkBox = new CheckBox
                    {
                        Content = kvp.Key,
                        IsChecked = kvp.Value
                    };

                    // Guardado automático al marcar/desmarcar
                    checkBox.IsCheckedChanged += async (s, args) =>
                    {
                        config[kvp.Key] = checkBox.IsChecked ?? false;
                        await configurablePlugin.SetConfigurationAsync(config);
                    };

                    stackPanel.Children.Add(checkBox);
                }

                // 4. Crear y mostrar el Flyout anclado al botón
                var flyout = new Flyout
                {
                    Content = stackPanel,
                    Placement = PlacementMode.BottomEdgeAlignedRight // Despliega hacia abajo
                };

                flyout.ShowAt(btn);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}