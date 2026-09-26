using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DanaProcessing.Ide.Localization;
using DanaProcessing.Ide.Theme;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// Agent mode's chat window -- a real, tool-using assistant with access
    /// to the current sketch and DanaProcessing's own API reference (see
    /// AgentSession/AgentTools). Non-modal (.Show(), not .ShowDialog()) --
    /// unlike Settings/NuGet, the whole point is to keep this open while
    /// working in the editor, so it doesn't block the owning MainWindow the
    /// way those dialogs deliberately do.
    ///
    /// Laid out to read like the Claude Code panel in VS Code rather than a
    /// generic chat app: messages flow directly against the panel
    /// background (no boxed-in scroll "card"), assistant replies are plain
    /// text with no bubble around them, tool calls render as small pill-
    /// shaped status chips with a friendly Spanish label instead of a raw
    /// snake_case tool name, and the composer is a single rounded box with
    /// the send affordance living inside it, bottom-right -- not a separate
    /// full-height button floating outside the input.
    /// </summary>
    public class AgentWindow : Window
    {
        private readonly MainWindow _owner;
        private readonly AgentSession _session;
        private readonly StackPanel _messageList;
        private readonly ScrollViewer _messageScroll;
        private readonly TextBlock _statusText;
        private readonly TextBox _inputBox;
        private readonly Button _sendButton;
        private CancellationTokenSource _cts = new();

        public AgentWindow(MainWindow owner)
        {
            _owner = owner;

            var settings = AgentSettingsStore.Load();
            _session = new AgentSession(owner, settings);
            _session.MessagesChanged += () => Dispatcher.UIThread.Post(RefreshMessages);
            _session.StatusChanged += status => Dispatcher.UIThread.Post(() => SetStatus(status));

            Title = Loc.Tr("Asistente — DanaProcessing IDE", "Assistant — DanaProcessing IDE");
            Width = 420;
            Height = 640;
            MinWidth = 320;
            MinHeight = 380;
            CanResize = true;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            // Custom chrome, matching MainWindow's own look -- see
            // MainWindow's remark on WindowDecorations.None for why this
            // trades away the OS's native title bar AND its edge-drag
            // resize in one move; BuildResizeOverlay() below reimplements
            // the resize edges by hand for the same reason MainWindow does.
            WindowDecorations = WindowDecorations.None;

            var titleBar = BuildTitleBar();

            var header = new Border
            {
                Padding = new Thickness(16, 10),
                BorderBrush = ClayTheme.SurfaceHover,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = Loc.Tr("Asistente de DanaProcessing", "DanaProcessing Assistant"),
                            Foreground = ClayTheme.TextPrimary,
                            FontFamily = ClayTheme.FontDisplay,
                            FontWeight = FontWeight.SemiBold,
                            FontSize = 13,
                        },
                        new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(settings.ApiKey)
                                ? Loc.Tr("No hay API key configurada -- abrí Settings para cargar una.", "No API key configured -- open Settings to add one.")
                                : Loc.Tr($"Proveedor: {_session.ProviderName}", $"Provider: {_session.ProviderName}"),
                            Foreground = ClayTheme.TextMuted,
                            FontFamily = ClayTheme.FontBody,
                            FontSize = 11,
                        },
                    },
                },
            };

            _messageList = new StackPanel { Spacing = 14, Margin = new Thickness(16, 12, 16, 6) };
            _messageScroll = new ScrollViewer { Content = _messageList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            _statusText = new TextBlock
            {
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 11,
                FontStyle = FontStyle.Italic,
                Margin = new Thickness(18, 0, 18, 4),
                IsVisible = false,
            };

            _inputBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 20,
                MaxHeight = 140,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                PlaceholderText = Loc.Tr("Pedile algo al asistente...", "Ask the assistant something..."),
            };
            _inputBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && e.KeyModifiers != KeyModifiers.Shift)
                {
                    e.Handled = true;
                    SendCurrentInput();
                }
            };

            _sendButton = new Button
            {
                Content = "↑",
                Classes = { "clay-icon" },
                Width = 26,
                Height = 26,
                FontSize = 14,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            ToolTip.SetTip(_sendButton, Loc.Tr("Enviar (Enter)", "Send (Enter)"));
            _sendButton.Click += (_, _) => SendCurrentInput();

            var composer = new Border
            {
                Background = ClayTheme.Surface,
                BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#E8E2DA")),
                BorderThickness = new Thickness(1),
                CornerRadius = ClayTheme.RadiusMedium,
                Padding = new Thickness(12, 10, 8, 8),
                Margin = new Thickness(16, 0, 16, 14),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children = { _inputBox, _sendButton },
                },
            };

            var body = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto") };
            Grid.SetRow(_messageScroll, 0);
            Grid.SetRow(_statusText, 1);
            Grid.SetRow(composer, 2);
            body.Children.Add(_messageScroll);
            body.Children.Add(_statusText);
            body.Children.Add(composer);

            var root = new DockPanel
            {
                LastChildFill = true,
                Children = { titleBar, header, body },
            };
            DockPanel.SetDock(titleBar, Dock.Top);
            DockPanel.SetDock(header, Dock.Top);

            Content = BuildResizeOverlay(root);

            RefreshMessages();
        }

        /// <summary>Slim title bar matching MainWindow's own custom chrome --
        /// just a wordmark on the left and minimize/maximize/close on the
        /// right, no run/hot-reload/settings/file-menu buttons (this window
        /// has none of those concerns).</summary>
        private Border BuildTitleBar()
        {
            var logoDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = ClayTheme.Accent,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var wordmark = new TextBlock
            {
                Text = Loc.Tr("Asistente", "Assistant"),
                Foreground = ClayTheme.TextPrimary,
                FontFamily = ClayTheme.FontDisplay,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var brand = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Children = { logoDot, wordmark },
            };

            var minButton = new Button { Content = "—", Classes = { "clay-chrome" } };
            minButton.Click += (_, _) => WindowState = WindowState.Minimized;

            var maxButton = new Button { Content = "▢", Classes = { "clay-chrome" } };
            maxButton.Click += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

            var closeButton = new Button { Content = "✕", Classes = { "clay-chrome-close" } };
            closeButton.Click += (_, _) => Close();

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Children = { minButton, maxButton, closeButton },
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Height = 40 };
            Grid.SetColumn(brand, 0);
            Grid.SetColumn(controls, 1);
            grid.Children.Add(brand);
            grid.Children.Add(controls);

            var root = new Border { Background = ClayTheme.TitleBarBackground, Child = grid };
            root.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };
            root.DoubleTapped += (_, _) =>
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

            return root;
        }

        /// <summary>Same edge/corner drag-to-resize reimplementation
        /// MainWindow uses for the same reason -- WindowDecorations.None
        /// throws away the OS's own resize border, so this lays invisible
        /// grips on top of the real content, each forwarding its
        /// PointerPressed into BeginResizeDrag for the matching edge.</summary>
        private Grid BuildResizeOverlay(Control content)
        {
            const double edgeThickness = 6;
            const double cornerSize = 12;

            var root = new Grid();
            root.Children.Add(content);

            void AddGrip(WindowEdge edge, HorizontalAlignment h, VerticalAlignment v, double? width, double? height, StandardCursorType cursorType)
            {
                var grip = new Border
                {
                    Background = Brushes.Transparent,
                    HorizontalAlignment = h,
                    VerticalAlignment = v,
                    Cursor = new Cursor(cursorType),
                };
                if (width.HasValue)
                    grip.Width = width.Value;
                if (height.HasValue)
                    grip.Height = height.Value;

                grip.PointerPressed += (_, e) =>
                {
                    if (e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
                        BeginResizeDrag(edge, e);
                };
                root.Children.Add(grip);
            }

            AddGrip(WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, null, edgeThickness, StandardCursorType.TopSide);
            AddGrip(WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, null, edgeThickness, StandardCursorType.BottomSide);
            AddGrip(WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, edgeThickness, null, StandardCursorType.LeftSide);
            AddGrip(WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, edgeThickness, null, StandardCursorType.RightSide);

            AddGrip(WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, cornerSize, cornerSize, StandardCursorType.TopLeftCorner);
            AddGrip(WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, cornerSize, cornerSize, StandardCursorType.TopRightCorner);
            AddGrip(WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, cornerSize, cornerSize, StandardCursorType.BottomLeftCorner);
            AddGrip(WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, cornerSize, cornerSize, StandardCursorType.BottomRightCorner);

            return root;
        }

        private void SendCurrentInput()
        {
            var text = _inputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            _inputBox.Text = "";
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            _ = _session.SendUserMessageAsync(text, _cts.Token);
        }

        private void SetStatus(string? status)
        {
            _statusText.Text = status ?? "";
            _statusText.IsVisible = status != null;
        }

        private void RefreshMessages()
        {
            _messageList.Children.Clear();

            if (_session.Messages.Count == 0)
            {
                _messageList.Children.Add(new TextBlock
                {
                    Text = Loc.Tr(
                        "Preguntale algo sobre tu sketch, pedile que lo edite, que lo corra, o que busque algo en la documentación de la API.",
                        "Ask it something about your sketch, ask it to edit it, run it, or search the API docs."),
                    Foreground = ClayTheme.TextMuted,
                    FontFamily = ClayTheme.FontBody,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(12, 24, 12, 0),
                });
            }

            foreach (var message in _session.Messages)
            {
                var bubble = BuildBubble(message);
                if (bubble != null)
                    _messageList.Children.Add(bubble);
            }
            _messageScroll.ScrollToEnd();
        }

        /// <summary>Renders a User bubble, a plain (unboxed) Assistant reply
        /// plus a compact status chip per tool call it made -- ToolResult
        /// messages carry no user-facing text of their own (their content
        /// already gets summarized by whatever the assistant says next), so
        /// they render as null (skipped).</summary>
        private Control? BuildBubble(AgentMessage message)
        {
            switch (message.Role)
            {
                case AgentRole.User:
                    return new Border
                    {
                        Background = ClayTheme.AccentGlow,
                        CornerRadius = ClayTheme.RadiusSmall,
                        Padding = new Thickness(11, 7),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        MaxWidth = 280,
                        Child = new TextBlock
                        {
                            Text = message.Text,
                            Foreground = ClayTheme.TextPrimary,
                            FontFamily = ClayTheme.FontBody,
                            FontSize = 13,
                            TextWrapping = TextWrapping.Wrap,
                        },
                    };

                case AgentRole.Assistant:
                    var stack = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
                    if (message.ToolCalls is { Count: > 0 })
                    {
                        foreach (var call in message.ToolCalls)
                            stack.Children.Add(BuildToolChip(call));
                    }
                    if (!string.IsNullOrEmpty(message.Text))
                    {
                        stack.Children.Add(new TextBlock
                        {
                            Text = message.Text,
                            Foreground = ClayTheme.TextPrimary,
                            FontFamily = ClayTheme.FontBody,
                            FontSize = 13,
                            TextWrapping = TextWrapping.Wrap,
                        });
                    }
                    return stack.Children.Count > 0 ? stack : null;

                default:
                    return null;
            }
        }

        private static Control BuildToolChip(AgentToolCall call)
        {
            var (icon, label) = DescribeToolCall(call);
            return new Border
            {
                Background = ClayTheme.SurfaceHigher,
                CornerRadius = ClayTheme.RadiusSmall,
                Padding = new Thickness(9, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = icon, FontSize = 11 },
                        new TextBlock
                        {
                            Text = label,
                            Foreground = ClayTheme.TextSecondary,
                            FontFamily = ClayTheme.FontMono,
                            FontSize = 11,
                        },
                    },
                },
            };
        }

        /// <summary>Maps a raw tool call (snake_case name + JSON args) to a
        /// short, human-facing icon+label -- e.g. "🔎 Buscando en la
        /// documentación: 'PVector'" instead of "🔧 search_docs", matching
        /// how the Claude Code panel describes a tool call in plain language
        /// rather than showing its literal identifier.</summary>
        private static (string Icon, string Label) DescribeToolCall(AgentToolCall call)
        {
            switch (call.Name)
            {
                case "read_sketch":
                    return ("📄", Loc.Tr("Leyendo el sketch", "Reading the sketch"));
                case "edit_sketch":
                    return ("✏️", Loc.Tr("Editando el sketch", "Editing the sketch"));
                case "run_sketch":
                    return ("▶", Loc.Tr("Ejecutando el sketch", "Running the sketch"));
                case "search_docs":
                    var query = TryExtractStringArg(call.ArgumentsJson, "query");
                    return ("🔎", query != null
                        ? Loc.Tr($"Buscando en la documentación: \"{query}\"", $"Searching the docs: \"{query}\"")
                        : Loc.Tr("Buscando en la documentación", "Searching the docs"));
                default:
                    return ("🔧", call.Name);
            }
        }

        private static string? TryExtractStringArg(string argumentsJson, string propertyName)
        {
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(argumentsJson);
                return node?[propertyName]?.GetValue<string>();
            }
            catch
            {
                return null;
            }
        }
    }
}
