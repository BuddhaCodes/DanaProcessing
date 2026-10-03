using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DanaProcessing.Ide.Editor;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide
{
    /// <summary>
    /// Browser for SketchSamples.All: a search box (accent- and case-
    /// insensitive, every word must match, also searches inside the code so
    /// "Sphere" or "PVector" finds the samples that use them), tag chips to
    /// narrow by topic, a list on the left and a details pane with the full
    /// description and a code preview on the right.
    ///
    /// Keyboard-first: the search box has focus on open; Up/Down move through
    /// the results, Enter opens the selected sample, Esc clears the search
    /// (or closes the window when there's nothing to clear), Ctrl+F jumps
    /// back to the search box. Double-click a card to open it directly.
    ///
    /// Uses the IDE's own chrome (ClayChrome), not the OS title bar.
    ///
    /// Modal, like SettingsWindow -- picking a sample (or closing) is a
    /// one-shot action, not something you leave open while working.
    /// </summary>
    public class SamplesWindow : Window
    {
        private static readonly IBrush Hairline = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA"));

        private sealed class Entry
        {
            public required SketchSample Sample { get; init; }
            public required int Index { get; init; }
            public required string Name { get; init; }
            public required string Description { get; init; }
            public required string Tags { get; init; }
            public required string Code { get; init; }
            public required int Lines { get; init; }
            public Border Card { get; set; } = null!;
        }

        private readonly Action<string> _onLoad;
        private readonly List<Entry> _entries;
        private readonly List<Entry> _visible = new();
        private readonly HashSet<SampleTag> _activeTags = new();
        private readonly Dictionary<SampleTag, Button> _tagButtons = new();
        private Entry? _selected;

        private readonly TextBox _search;
        private readonly TextBlock _countText;
        private readonly Button _allTagsButton;
        private readonly StackPanel _list;
        private readonly ScrollViewer _listScroll;
        private readonly StackPanel _emptyState;
        private readonly TextBlock _emptyText;

        private readonly Control _details;
        private readonly TextBlock _detailsPlaceholder;
        private readonly TextBlock _detailName;
        private readonly WrapPanel _detailTags;
        private readonly TextBlock _detailDescription;
        private readonly TextBlock _codeHeader;
        private readonly SelectableTextBlock _code;
        private readonly ScrollViewer _codeScroll;

        /// <param name="onLoad">Called with the chosen sample's source once the user picks one. The window closes itself right after.</param>
        public SamplesWindow(Action<string> onLoad)
        {
            _onLoad = onLoad;

            Title = Loc.Tr("Ejemplos", "Examples");
            Width = 1040;
            Height = 680;
            MinWidth = 760;
            MinHeight = 460;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            _entries = SketchSamples.All.Select((s, i) => new Entry
            {
                Sample = s,
                Index = i,
                Name = Normalize(s.Name),
                Description = Normalize(s.Description),
                Tags = Normalize(string.Join(" ", s.Tags.Select(SampleTags.SearchText))),
                Code = Normalize(s.Source),
                Lines = s.Source.Split('\n').Length,
            }).ToList();

            // --- Result count: shown next to the title in the IDE-style title bar ---
            _countText = new TextBlock
            {
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12.5,
                Margin = new Thickness(2, 1, 0, 0),
            };

            // --- Search ---
            _search = new TextBox
            {
                PlaceholderText = Loc.Tr(
                    "Buscá por nombre, tema o función del código  (ej. audio, cámara, PVector, json)",
                    "Search by name, topic or a function in the code  (e.g. audio, camera, PVector, json)"),
                FontFamily = ClayTheme.FontBody,
                FontSize = 13.5,
                Padding = new Thickness(12, 9),
                Margin = new Thickness(0, 0, 0, 10),
            };
            _search.TextChanged += (_, _) => ApplyFilter();

            // --- Tag chips (only tags some sample actually uses, with counts) ---
            var tagBar = new WrapPanel { Orientation = Orientation.Horizontal };
            _allTagsButton = BuildChip(Loc.Tr($"Todos  {_entries.Count}", $"All  {_entries.Count}"));
            _allTagsButton.Click += (_, _) =>
            {
                _activeTags.Clear();
                ApplyFilter();
            };
            tagBar.Children.Add(_allTagsButton);

            foreach (var tag in Enum.GetValues<SampleTag>())
            {
                int count = _entries.Count(e => e.Sample.Tags.Contains(tag));
                if (count == 0)
                    continue;
                var chip = BuildChip($"{SampleTags.Label(tag)}  {count}");
                chip.Click += (_, _) => ToggleTag(tag);
                _tagButtons[tag] = chip;
                tagBar.Children.Add(chip);
            }

            // --- Result list (left) ---
            _list = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 10, 0) };
            foreach (var entry in _entries)
                entry.Card = BuildCard(entry);
            _listScroll = new ScrollViewer { Content = _list };

            _emptyText = new TextBlock
            {
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
            };
            var clearButton = new Button
            {
                Content = Loc.Tr("Limpiar búsqueda y filtros", "Clear search and filters"),
                Classes = { "clay-secondary" },
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            clearButton.Click += (_, _) => ClearAll();
            _emptyState = new StackPanel
            {
                Spacing = 12,
                Margin = new Thickness(16, 40, 26, 0),
                IsVisible = false,
                Children = { _emptyText, clearButton },
            };
            var left = new Panel { Children = { _listScroll, _emptyState } };

            // --- Details (right) ---
            _detailName = new TextBlock
            {
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 17,
                TextWrapping = TextWrapping.Wrap,
            };
            _detailTags = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            _detailDescription = new TextBlock
            {
                Foreground = ClayTheme.TextSecondary,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                LineHeight = 19,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0),
            };
            _codeHeader = new TextBlock
            {
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                Margin = new Thickness(0, 16, 0, 6),
            };
            var detailsTop = new StackPanel { Children = { _detailName, _detailTags, _detailDescription, _codeHeader } };

            _code = new SelectableTextBlock
            {
                FontFamily = ClayTheme.FontMono,
                FontSize = 12,
                Foreground = ClayTheme.TextPrimary,
                Margin = new Thickness(14, 12),
            };
            _codeScroll = new ScrollViewer
            {
                Content = _code,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            };
            var codeBox = new Border
            {
                Background = ClayTheme.SurfaceHigher,
                BorderBrush = Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = ClayTheme.RadiusSmall,
                ClipToBounds = true,
                Child = _codeScroll,
            };

            var openButton = new Button
            {
                Content = Loc.Tr("Abrir en una pestaña nueva", "Open in a new tab"),
                Classes = { "clay-run" },
            };
            openButton.Click += (_, _) =>
            {
                if (_selected != null)
                    Load(_selected);
            };
            var hint = new TextBlock
            {
                Text = Loc.Tr("↑ ↓ para moverte · Enter abre · Esc limpia/cierra · doble click abre",
                              "↑ ↓ to move · Enter opens · Esc clears/closes · double-click opens"),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 14, 0, 0) };
            Grid.SetColumn(hint, 0);
            Grid.SetColumn(openButton, 1);
            actions.Children.Add(hint);
            actions.Children.Add(openButton);

            var detailsDock = new DockPanel();
            DockPanel.SetDock(detailsTop, Dock.Top);
            DockPanel.SetDock(actions, Dock.Bottom);
            detailsDock.Children.Add(detailsTop);
            detailsDock.Children.Add(actions);
            detailsDock.Children.Add(codeBox);
            _details = detailsDock;

            _detailsPlaceholder = new TextBlock
            {
                Text = Loc.Tr("Elegí un ejemplo de la lista para ver de qué trata y su código.",
                              "Pick an example from the list to see what it's about and its code."),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var detailsCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusMedium,
                Padding = new Thickness(20, 18),
                Child = new Panel { Children = { _details, _detailsPlaceholder } },
            };

            var body = new Grid { ColumnDefinitions = new ColumnDefinitions("380,8,*"), Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetColumn(left, 0);
            Grid.SetColumn(detailsCard, 2);
            body.Children.Add(left);
            body.Children.Add(detailsCard);

            var root = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,*"),
                Margin = new Thickness(24, 16, 24, 20),
            };
            Grid.SetRow(_search, 0);
            Grid.SetRow(tagBar, 1);
            Grid.SetRow(body, 2);
            root.Children.Add(_search);
            root.Children.Add(tagBar);
            root.Children.Add(body);

            // Same title bar as the IDE (— ▢ ✕, drag, double-click to
            // maximize, edge resize) instead of the OS's own.
            ClayChrome.Apply(this, root, _countText);

            AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
            Opened += (_, _) => _search.Focus();

            ApplyFilter();
        }

        private static Button BuildChip(string text) => new()
        {
            Content = text,
            Classes = { "clay-chip" },
            Margin = new Thickness(0, 0, 6, 6),
        };

        private Border BuildCard(Entry entry)
        {
            var name = new TextBlock
            {
                Text = entry.Sample.Name,
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13.5,
                TextWrapping = TextWrapping.Wrap,
            };

            var tags = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            foreach (var tag in entry.Sample.Tags)
                tags.Children.Add(BuildTagPill(tag));

            var description = new TextBlock
            {
                Text = entry.Sample.Description,
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 4, 0, 0),
            };

            var card = new Border
            {
                Background = ClayTheme.Surface,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1.5),
                CornerRadius = ClayTheme.RadiusMedium,
                Padding = new Thickness(14, 11),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel { Children = { name, tags, description } },
            };

            card.PointerEntered += (_, _) =>
            {
                if (_selected != entry)
                    card.Background = ClayTheme.SurfaceHigher;
            };
            card.PointerExited += (_, _) =>
            {
                if (_selected != entry)
                    card.Background = ClayTheme.Surface;
            };
            card.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
                    return;
                Select(entry, scrollIntoView: false);
                if (e.ClickCount >= 2)
                    Load(entry);
            };

            return card;
        }

        /// <summary>Small read-only tag label used on the cards. Highlighted when that tag is an active filter.</summary>
        private Border BuildTagPill(SampleTag tag) => new()
        {
            Tag = tag,
            Background = ClayTheme.Base,
            CornerRadius = ClayTheme.RadiusPill,
            Padding = new Thickness(7, 1),
            Margin = new Thickness(0, 0, 4, 3),
            Child = new TextBlock
            {
                Text = SampleTags.Label(tag),
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 10.5,
            },
        };

        private void ToggleTag(SampleTag tag)
        {
            if (!_activeTags.Remove(tag))
                _activeTags.Add(tag);
            ApplyFilter();
        }

        private void ClearAll()
        {
            _activeTags.Clear();
            _search.Text = "";   // TextChanged -> ApplyFilter
            ApplyFilter();
            _search.Focus();
        }

        /// <summary>
        /// Re-filters and re-ranks the list. Every search word must appear
        /// somewhere (name, tags, description or code); matches in the name
        /// rank above tags, tags above description, description above code.
        /// With no search text the catalog keeps its own order.
        /// </summary>
        private void ApplyFilter()
        {
            var terms = Normalize(_search.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var ranked = new List<(Entry Entry, int Score)>();
            foreach (var entry in _entries)
            {
                if (_activeTags.Count > 0 && !_activeTags.All(t => entry.Sample.Tags.Contains(t)))
                    continue;

                int score = Score(entry, terms);
                if (terms.Length > 0 && score == 0)
                    continue;
                ranked.Add((entry, score));
            }

            _visible.Clear();
            _visible.AddRange(ranked
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Entry.Index)
                .Select(r => r.Entry));

            _list.Children.Clear();
            foreach (var entry in _visible)
                _list.Children.Add(entry.Card);

            // Chips + card pills reflect the active filters.
            _allTagsButton.Classes.Set("active", _activeTags.Count == 0);
            foreach (var (tag, button) in _tagButtons)
                button.Classes.Set("active", _activeTags.Contains(tag));
            foreach (var entry in _entries)
                RefreshPills(entry.Card);

            _countText.Text = _visible.Count == _entries.Count
                ? Loc.Tr($"{_entries.Count} ejemplos", $"{_entries.Count} examples")
                : Loc.Tr($"{_visible.Count} de {_entries.Count}", $"{_visible.Count} of {_entries.Count}");

            bool empty = _visible.Count == 0;
            _emptyState.IsVisible = empty;
            _listScroll.IsVisible = !empty;
            if (empty)
            {
                var what = string.IsNullOrWhiteSpace(_search.Text) ? "" : $" «{_search.Text!.Trim()}»";
                _emptyText.Text = Loc.Tr(
                    $"Ningún ejemplo coincide con{what}{(_activeTags.Count > 0 ? " y los tags elegidos" : "")}.",
                    $"No example matches{what}{(_activeTags.Count > 0 ? " with the selected tags" : "")}.");
            }

            // Keep the selection if it survived the filter; otherwise take the best match.
            Select(_selected != null && _visible.Contains(_selected) ? _selected : _visible.FirstOrDefault(), scrollIntoView: true);
        }

        private static int Score(Entry entry, string[] terms)
        {
            int total = 0;
            foreach (var term in terms)
            {
                int s =
                    entry.Name.StartsWith(term, StringComparison.Ordinal) ? 14 :
                    entry.Name.Contains(term, StringComparison.Ordinal) ? 10 :
                    entry.Tags.Contains(term, StringComparison.Ordinal) ? 6 :
                    entry.Description.Contains(term, StringComparison.Ordinal) ? 3 :
                    entry.Code.Contains(term, StringComparison.Ordinal) ? 1 : 0;
                if (s == 0)
                    return 0;
                total += s;
            }
            return total;
        }

        private void RefreshPills(Border card)
        {
            if (card.Child is not StackPanel { Children: [_, WrapPanel pills, ..] })
                return;
            foreach (var child in pills.Children)
            {
                if (child is Border { Tag: SampleTag tag } pill && pill.Child is TextBlock text)
                {
                    bool on = _activeTags.Contains(tag);
                    pill.Background = on ? ClayTheme.AccentGlow : ClayTheme.Base;
                    text.Foreground = on ? ClayTheme.OnAccent : ClayTheme.TextMuted;
                }
            }
        }

        private void Select(Entry? entry, bool scrollIntoView)
        {
            if (_selected != null && _selected != entry)
            {
                _selected.Card.Background = ClayTheme.Surface;
                _selected.Card.BorderBrush = Brushes.Transparent;
            }

            _selected = entry;
            _details.IsVisible = entry != null;
            _detailsPlaceholder.IsVisible = entry == null;
            if (entry == null)
                return;

            entry.Card.Background = ClayTheme.SurfaceHigher;
            entry.Card.BorderBrush = ClayTheme.Accent;
            if (scrollIntoView)
                entry.Card.BringIntoView();

            var sample = entry.Sample;
            _detailName.Text = sample.Name;
            _detailDescription.Text = sample.Description;

            // Clickable chips here: a quick way to say "more like this".
            _detailTags.Children.Clear();
            foreach (var tag in sample.Tags)
            {
                var chip = BuildChip(SampleTags.Label(tag));
                chip.Classes.Set("active", _activeTags.Contains(tag));
                ToolTip.SetTip(chip, Loc.Tr("Filtrar por este tag", "Filter by this tag"));
                chip.Click += (_, _) => ToggleTag(tag);
                _detailTags.Children.Add(chip);
            }

            _codeHeader.Text = Loc.Tr($"CÓDIGO · {entry.Lines} líneas", $"CODE · {entry.Lines} lines");
            _code.Text = sample.Source.Replace("\r\n", "\n");
            _codeScroll.Offset = default;
        }

        private void Load(Entry entry)
        {
            _onLoad(entry.Sample.Source);
            Close();
        }

        private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
            {
                _search.Focus();
                _search.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.KeyModifiers != KeyModifiers.None)
                return;

            switch (e.Key)
            {
                case Key.Down:
                case Key.Up:
                    if (_visible.Count == 0)
                        break;
                    int i = _selected == null ? -1 : _visible.IndexOf(_selected);
                    i = e.Key == Key.Down ? Math.Min(i + 1, _visible.Count - 1) : Math.Max(i - 1, 0);
                    Select(_visible[i], scrollIntoView: true);
                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (_selected != null)
                    {
                        Load(_selected);
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    if (!string.IsNullOrEmpty(_search.Text) || _activeTags.Count > 0)
                        ClearAll();
                    else
                        Close();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>Lowercase, accents stripped ("Cámara" -> "camara"), so typing without tildes still matches.</summary>
        private static string Normalize(string text)
        {
            var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
