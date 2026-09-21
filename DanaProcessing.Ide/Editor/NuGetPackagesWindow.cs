using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DanaProcessing.Ide.Compilation.PackageManagement;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Editor
{
    /// <summary>
    /// Manages the active sketch's `// nuget:` package directives with a
    /// Visual-Studio-style "Buscar"/"Instalados" split view: a search box and
    /// result list on the left, a details pane (description, version picker,
    /// Instalar/Actualizar/Quitar) on the right. Edits only the directive
    /// comments in the source text (via SketchEditorView.ApplyPackageDirectives)
    /// on Guardar; it never touches the network itself for anything beyond
    /// searching and listing versions — actual package resolution/download
    /// happens on the next Run, same as before this UI existed.
    /// </summary>
    public class NuGetPackagesWindow : Window
    {
        private const string LatestLabel = "(ultima estable)";

        private enum Tab { Browse, Installed }

        private sealed class SelectedPackage
        {
            public required string Id;
            public string? Title;
            public string? Description;
            public string? Authors;
            public long? Downloads;
            public List<string>? Versions; // null while still loading
            public string? PickedVersion; // null = "latest" (LatestLabel)
        }

        private readonly List<PackageDirective> _working;
        private readonly Action<IReadOnlyList<PackageDirective>> _onSave;

        private Tab _activeTab = Tab.Browse;
        private readonly Button _browseTabButton;
        private readonly Button _installedTabButton;

        private readonly TextBox _searchBox;
        private readonly StackPanel _searchResultsList;
        private readonly TextBlock _searchStatusText;
        private readonly Control _browsePanel;

        private readonly StackPanel _installedList;
        private readonly Control _installedPanel;

        private readonly StackPanel _detailsContent;

        private readonly DispatcherTimer _searchDebounce;
        private int _searchGeneration;
        private int _versionsGeneration;

        private SelectedPackage? _selected;

        /// <param name="current">The active sketch's directives right now, as parsed by PackageDirectiveParser.Parse.</param>
        /// <param name="onSave">Called with the final directive set if the user presses Guardar. Not called on Cancelar.</param>
        public NuGetPackagesWindow(IReadOnlyList<PackageDirective> current, Action<IReadOnlyList<PackageDirective>> onSave)
        {
            _working = current.ToList();
            _onSave = onSave;

            Title = "Paquetes NuGet — DanaProcessing IDE";
            Width = 860;
            Height = 640;
            MinWidth = 680;
            MinHeight = 480;
            CanResize = true;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var header = new StackPanel
            {
                Margin = new Thickness(24, 20, 24, 0),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Paquetes NuGet del sketch",
                        Foreground = ClayTheme.TextPrimary,
                        FontFamily = ClayTheme.FontDisplay,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 17,
                    },
                    new TextBlock
                    {
                        Text = "Buscá en NuGet.org o gestioná lo que ya está instalado. Los cambios se aplican al guardar.",
                        Foreground = ClayTheme.TextMuted,
                        FontFamily = ClayTheme.FontBody,
                        FontSize = 12,
                        Margin = new Thickness(0, 3, 0, 0),
                    },
                }
            };

            (_browseTabButton, _installedTabButton) = BuildTabStrip();
            var tabRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(24, 14, 24, 0),
                Children = { _browseTabButton, _installedTabButton },
            };

            // --- "Buscar" pane: search box + result rows ---
            _searchBox = new TextBox
            {
                PlaceholderText = "Buscar paquetes en NuGet.org...",
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8),
            };
            _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _searchDebounce.Tick += (_, _) =>
            {
                _searchDebounce.Stop();
                _ = RunSearchAsync(_searchBox.Text);
            };
            _searchBox.TextChanged += (_, _) => { _searchDebounce.Stop(); _searchDebounce.Start(); };
            _searchBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    _searchDebounce.Stop();
                    _ = RunSearchAsync(_searchBox.Text);
                }
            };

            _searchStatusText = new TextBlock
            {
                Text = "Escribi para buscar paquetes en NuGet.org.",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 8, 4, 4),
            };
            _searchResultsList = new StackPanel { Spacing = 4 };
            var browseListScroll = new ScrollViewer
            {
                Content = new StackPanel { Children = { _searchStatusText, _searchResultsList } },
            };

            var browseDock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_searchBox, Dock.Top);
            browseDock.Children.Add(_searchBox);
            browseDock.Children.Add(browseListScroll);
            _browsePanel = browseDock;

            // --- "Instalados" pane: current directives ---
            _installedList = new StackPanel { Spacing = 4 };
            _installedPanel = new ScrollViewer { Content = _installedList };

            var leftStack = new Panel { Children = { _browsePanel, _installedPanel } };

            // --- Details pane (right) ---
            _detailsContent = new StackPanel { Spacing = 4 };
            var detailsScroll = new ScrollViewer { Content = _detailsContent };
            var detailsCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusMedium,
                Padding = new Thickness(18, 16),
                Child = detailsScroll,
            };

            var bodyGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("300,16,*"),
                Margin = new Thickness(24, 12, 24, 0),
            };
            Grid.SetColumn(leftStack, 0);
            Grid.SetColumn(detailsCard, 2);
            bodyGrid.Children.Add(leftStack);
            bodyGrid.Children.Add(detailsCard);

            var footer = BuildButtonRow();
            footer.Margin = new Thickness(24, 12, 24, 20);

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            rootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
            rootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(header, 0);
            Grid.SetRow(tabRow, 1);
            Grid.SetRow(bodyGrid, 2);
            Grid.SetRow(footer, 3);
            rootGrid.Children.Add(header);
            rootGrid.Children.Add(tabRow);
            rootGrid.Children.Add(bodyGrid);
            rootGrid.Children.Add(footer);

            Content = rootGrid;

            RefreshInstalledList();
            ShowEmptyDetails();
            SetTab(Tab.Browse);
        }

        private (Button browse, Button installed) BuildTabStrip()
        {
            var browse = new Button
            {
                Content = "Buscar",
                Classes = { "clay-toggle" },
                Padding = new Thickness(16, 7),
                FontSize = 12.5,
            };
            var installed = new Button
            {
                Content = "Instalados",
                Classes = { "clay-toggle" },
                Padding = new Thickness(16, 7),
                FontSize = 12.5,
            };
            browse.Click += (_, _) => SetTab(Tab.Browse);
            installed.Click += (_, _) => SetTab(Tab.Installed);
            return (browse, installed);
        }

        private void SetTab(Tab tab)
        {
            _activeTab = tab;
            _browseTabButton.Classes.Set("active", tab == Tab.Browse);
            _installedTabButton.Classes.Set("active", tab == Tab.Installed);
            _browsePanel.IsVisible = tab == Tab.Browse;
            _installedPanel.IsVisible = tab == Tab.Installed;
        }

        private async Task RunSearchAsync(string? query)
        {
            var generation = ++_searchGeneration;
            query = query?.Trim() ?? "";
            _searchResultsList.Children.Clear();

            if (query.Length == 0)
            {
                _searchStatusText.Text = "Escribi para buscar paquetes en NuGet.org.";
                _searchStatusText.IsVisible = true;
                return;
            }

            _searchStatusText.Text = "Buscando...";
            _searchStatusText.IsVisible = true;

            IReadOnlyList<PackageSearchResult> results;
            try
            {
                results = await NuGetPackageResolver.SearchAsync(query);
            }
            catch (Exception ex)
            {
                if (generation != _searchGeneration)
                    return;
                _searchStatusText.Text = $"Error buscando en NuGet.org: {ex.Message}";
                return;
            }

            // A newer keystroke (or Enter) started another search while this
            // one was in flight — that result is what the user is waiting on,
            // this one is stale and would just flicker the list backwards.
            if (generation != _searchGeneration)
                return;

            if (results.Count == 0)
            {
                _searchStatusText.Text = "Sin resultados.";
                return;
            }

            _searchStatusText.IsVisible = false;
            foreach (var r in results)
                _searchResultsList.Children.Add(BuildSearchResultRow(r));
        }

        private Control BuildSearchResultRow(PackageSearchResult r)
        {
            var titleText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(r.Title) ? r.Id : r.Title,
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13.5,
            };
            var idText = new TextBlock
            {
                Text = r.Id,
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontMono,
                FontSize = 11,
                Margin = new Thickness(0, 1, 0, 0),
            };

            var content = new StackPanel { Children = { titleText, idText } };

            if (!string.IsNullOrWhiteSpace(r.Description))
            {
                content.Children.Add(new TextBlock
                {
                    Text = r.Description,
                    Foreground = ClayTheme.TextSecondary,
                    FontFamily = ClayTheme.FontBody,
                    FontSize = 11.5,
                    TextWrapping = TextWrapping.Wrap,
                    MaxLines = 2,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 4, 0, 0),
                });
            }

            var metaParts = new List<string>();
            if (r.DownloadCount.HasValue) metaParts.Add($"{FormatCount(r.DownloadCount.Value)} descargas");
            if (!string.IsNullOrWhiteSpace(r.Authors)) metaParts.Add(r.Authors!);
            if (metaParts.Count > 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = string.Join(" · ", metaParts),
                    Foreground = ClayTheme.TextMuted,
                    FontFamily = ClayTheme.FontBody,
                    FontSize = 10.5,
                    Margin = new Thickness(0, 4, 0, 0),
                });
            }

            if (_working.Any(d => d.Id.Equals(r.Id, StringComparison.OrdinalIgnoreCase)))
            {
                content.Children.Add(new TextBlock
                {
                    Text = "✓ Instalado",
                    Foreground = ClayTheme.Success,
                    FontFamily = ClayTheme.FontBody,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 10.5,
                    Margin = new Thickness(0, 4, 0, 0),
                });
            }

            var button = new Button
            {
                Content = content,
                Classes = { "clay-menu-item" },
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 10),
            };
            button.Click += (_, _) => SelectSearchResult(r);
            return button;
        }

        private void SelectSearchResult(PackageSearchResult r)
        {
            var existing = _working.FirstOrDefault(d => d.Id.Equals(r.Id, StringComparison.OrdinalIgnoreCase));
            var selected = new SelectedPackage
            {
                Id = r.Id,
                Title = r.Title,
                Description = r.Description,
                Authors = r.Authors,
                Downloads = r.DownloadCount,
                PickedVersion = existing?.Version,
            };
            _selected = selected;
            RenderDetails();
            _ = LoadVersionsAsync(selected);
        }

        private void SelectInstalled(PackageDirective directive)
        {
            var selected = new SelectedPackage { Id = directive.Id, PickedVersion = directive.Version };
            _selected = selected;
            RenderDetails();
            _ = LoadVersionsAsync(selected);
        }

        private async Task LoadVersionsAsync(SelectedPackage target)
        {
            var generation = ++_versionsGeneration;
            List<string> versions;
            try
            {
                versions = (await NuGetPackageResolver.GetVersionsAsync(target.Id)).ToList();
            }
            catch
            {
                versions = new List<string>();
            }

            // The user may have selected a different package while this was
            // in flight — that selection owns the details pane now.
            if (_selected != target || generation != _versionsGeneration)
                return;

            target.Versions = versions;
            RenderDetails();
        }

        private void ShowEmptyDetails()
        {
            _selected = null;
            _detailsContent.Children.Clear();
            _detailsContent.Children.Add(new TextBlock
            {
                Text = "Selecciona un paquete de la lista para ver mas detalles.",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        private void RenderDetails()
        {
            var s = _selected;
            if (s is null)
            {
                ShowEmptyDetails();
                return;
            }

            _detailsContent.Children.Clear();

            _detailsContent.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(s.Title) ? s.Id : s.Title,
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 17,
                TextWrapping = TextWrapping.Wrap,
            });
            _detailsContent.Children.Add(new TextBlock
            {
                Text = s.Id,
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontMono,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
            });

            var metaParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.Authors)) metaParts.Add(s.Authors!);
            if (s.Downloads.HasValue) metaParts.Add($"{FormatCount(s.Downloads.Value)} descargas");
            if (metaParts.Count > 0)
            {
                _detailsContent.Children.Add(new TextBlock
                {
                    Text = string.Join(" · ", metaParts),
                    Foreground = ClayTheme.TextSecondary,
                    FontFamily = ClayTheme.FontBody,
                    FontSize = 11.5,
                    Margin = new Thickness(0, 8, 0, 0),
                });
            }

            if (!string.IsNullOrWhiteSpace(s.Description))
            {
                _detailsContent.Children.Add(new TextBlock
                {
                    Text = s.Description,
                    Foreground = ClayTheme.TextSecondary,
                    FontFamily = ClayTheme.FontBody,
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0),
                });
            }

            _detailsContent.Children.Add(new TextBlock
            {
                Text = "Version",
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12,
                Margin = new Thickness(0, 16, 0, 4),
            });

            var comboItems = new List<string> { LatestLabel };
            if (s.Versions != null)
                comboItems.AddRange(s.Versions);

            var combo = new ComboBox
            {
                ItemsSource = comboItems,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontFamily = ClayTheme.FontMono,
                FontSize = 12,
            };
            combo.SelectedItem = s.PickedVersion ?? LatestLabel;
            combo.SelectionChanged += (_, _) =>
            {
                var picked = combo.SelectedItem as string;
                s.PickedVersion = picked == LatestLabel ? null : picked;
            };
            _detailsContent.Children.Add(combo);

            if (s.Versions == null)
            {
                _detailsContent.Children.Add(new TextBlock
                {
                    Text = "Cargando versiones disponibles...",
                    Foreground = ClayTheme.TextMuted,
                    FontFamily = ClayTheme.FontBody,
                    FontSize = 11,
                    Margin = new Thickness(0, 4, 0, 0),
                });
            }

            var isInstalled = _working.Any(d => d.Id.Equals(s.Id, StringComparison.OrdinalIgnoreCase));
            var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };

            if (isInstalled)
            {
                var updateButton = new Button
                {
                    Content = "Actualizar version",
                    Classes = { "clay-run" },
                    Padding = new Thickness(14, 8),
                    CornerRadius = ClayTheme.RadiusButton,
                    FontSize = 12.5,
                };
                updateButton.Click += (_, _) => ApplyInstall(s);

                var removeButton = new Button
                {
                    Content = "Quitar",
                    Classes = { "clay-secondary" },
                    Padding = new Thickness(14, 8),
                    FontSize = 12.5,
                };
                removeButton.Click += (_, _) => RemoveInstalled(s.Id);

                actionsRow.Children.Add(updateButton);
                actionsRow.Children.Add(removeButton);
            }
            else
            {
                var installButton = new Button
                {
                    Content = "+ Instalar",
                    Classes = { "clay-run" },
                    Padding = new Thickness(14, 8),
                    CornerRadius = ClayTheme.RadiusButton,
                    FontSize = 12.5,
                };
                installButton.Click += (_, _) => ApplyInstall(s);
                actionsRow.Children.Add(installButton);
            }

            _detailsContent.Children.Add(actionsRow);
        }

        private void ApplyInstall(SelectedPackage s)
        {
            _working.RemoveAll(d => d.Id.Equals(s.Id, StringComparison.OrdinalIgnoreCase));
            _working.Add(new PackageDirective(s.Id, s.PickedVersion));
            RefreshInstalledList();
            RenderDetails();
        }

        private void RemoveInstalled(string id)
        {
            _working.RemoveAll(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            RefreshInstalledList();
            if (_selected != null && _selected.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                RenderDetails();
        }

        private void RefreshInstalledList()
        {
            _installedList.Children.Clear();
            _installedTabButton.Content = _working.Count == 0 ? "Instalados" : $"Instalados ({_working.Count})";

            if (_working.Count == 0)
            {
                _installedList.Children.Add(new TextBlock
                {
                    Text = "Este sketch todavia no usa ningun paquete NuGet.",
                    Foreground = ClayTheme.TextMuted,
                    FontFamily = ClayTheme.FontBody,
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 8, 4, 4),
                });
                return;
            }

            foreach (var directive in _working.OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase))
                _installedList.Children.Add(BuildInstalledRow(directive));
        }

        private Control BuildInstalledRow(PackageDirective directive)
        {
            var idText = new TextBlock
            {
                Text = directive.Id,
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13.5,
            };
            var versionText = new TextBlock
            {
                Text = directive.Version ?? "ultima version estable",
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontMono,
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 0, 0),
            };

            var content = new StackPanel { Children = { idText, versionText } };
            var button = new Button
            {
                Content = content,
                Classes = { "clay-menu-item" },
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 10),
            };
            button.Click += (_, _) => SelectInstalled(directive);
            return button;
        }

        private Control BuildButtonRow()
        {
            var cancelButton = new Button
            {
                Content = "Cancelar",
                Classes = { "clay-secondary" },
                Padding = new Thickness(14, 8),
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 8, 0),
            };
            cancelButton.Click += (_, _) => Close();

            var saveButton = new Button
            {
                Content = "Guardar",
                Classes = { "clay-run" },
                Padding = new Thickness(16, 8),
                CornerRadius = ClayTheme.RadiusButton,
                FontSize = 12.5,
            };
            saveButton.Click += (_, _) =>
            {
                _onSave(_working);
                Close();
            };

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { cancelButton, saveButton },
            };
        }

        private static string FormatCount(long n)
        {
            if (n >= 1_000_000_000) return $"{n / 1_000_000_000.0:0.#}B";
            if (n >= 1_000_000) return $"{n / 1_000_000.0:0.#}M";
            if (n >= 1_000) return $"{n / 1_000.0:0.#}K";
            return n.ToString();
        }
    }
}
