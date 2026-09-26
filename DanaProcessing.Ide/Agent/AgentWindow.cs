using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
            Width = 480;
            Height = 640;
            MinWidth = 360;
            MinHeight = 420;
            CanResize = true;
            Background = ClayTheme.Base;
            Styles.AddRange(ClayTheme.ButtonEffectStyles());

            var header = new StackPanel
            {
                Margin = new Thickness(20, 18, 20, 10),
                Children =
                {
                    new TextBlock
                    {
                        Text = Loc.Tr("Asistente de DanaProcessing", "DanaProcessing Assistant"),
                        Foreground = ClayTheme.TextPrimary,
                        FontFamily = ClayTheme.FontDisplay,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 16,
                    },
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(settings.ApiKey)
                            ? Loc.Tr("No hay API key configurada -- abrí Settings para cargar una.", "No API key configured -- open Settings to add one.")
                            : Loc.Tr($"Proveedor: {_session.ProviderName}", $"Provider: {_session.ProviderName}"),
                        Foreground = ClayTheme.TextMuted,
                        FontFamily = ClayTheme.FontBody,
                        FontSize = 12,
                        Margin = new Thickness(0, 3, 0, 0),
                    },
                },
            };

            _messageList = new StackPanel { Spacing = 10, Margin = new Thickness(4) };
            _messageScroll = new ScrollViewer { Content = _messageList };
            var messagesCard = new Border
            {
                Background = ClayTheme.Surface,
                CornerRadius = ClayTheme.RadiusMedium,
                Padding = new Thickness(14, 12),
                Child = _messageScroll,
            };

            _statusText = new TextBlock
            {
                Foreground = ClayTheme.TextMuted,
                FontFamily = ClayTheme.FontBody,
                FontSize = 12,
                FontStyle = FontStyle.Italic,
                Margin = new Thickness(4, 6, 4, 0),
                IsVisible = false,
            };

            _inputBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 64,
                FontFamily = ClayTheme.FontBody,
                FontSize = 13,
                PlaceholderText = Loc.Tr("Pedile algo al asistente... (Enter para enviar, Shift+Enter para salto de línea)", "Ask the assistant something... (Enter to send, Shift+Enter for a new line)"),
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
                Content = Loc.Tr("Enviar", "Send"),
                Background = ClayTheme.Accent,
                Foreground = ClayTheme.OnAccent,
                CornerRadius = ClayTheme.RadiusButton,
                Padding = new Thickness(18, 8),
                FontFamily = ClayTheme.FontBody,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            _sendButton.Click += (_, _) => SendCurrentInput();

            var inputRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetColumn(_inputBox, 0);
            Grid.SetColumn(_sendButton, 1);
            var sendButtonWrap = new Border { Child = _sendButton, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            inputRow.Children.Add(_inputBox);
            inputRow.Children.Add(sendButtonWrap);

            var body = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto,Auto"),
                Margin = new Thickness(20, 0, 20, 16),
            };
            Grid.SetRow(messagesCard, 0);
            Grid.SetRow(_statusText, 1);
            Grid.SetRow(inputRow, 2);
            body.Children.Add(messagesCard);
            body.Children.Add(_statusText);
            body.Children.Add(inputRow);

            Content = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    header,
                    body,
                },
            };
            DockPanel.SetDock(header, Dock.Top);

            RefreshMessages();
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
            foreach (var message in _session.Messages)
            {
                var bubble = BuildBubble(message);
                if (bubble != null)
                    _messageList.Children.Add(bubble);
            }
            _messageScroll.ScrollToEnd();
        }

        /// <summary>Renders a User/Assistant text bubble, plus a compact
        /// "🔧 tool_name" line under an Assistant message that called tools --
        /// ToolResult messages carry no user-facing text of their own (their
        /// content already gets summarized by whatever the assistant says
        /// next), so they render as null (skipped).</summary>
        private Control? BuildBubble(AgentMessage message)
        {
            switch (message.Role)
            {
                case AgentRole.User:
                    return new Border
                    {
                        Background = ClayTheme.AccentGlow,
                        CornerRadius = ClayTheme.RadiusSmall,
                        Padding = new Thickness(12, 8),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        MaxWidth = 340,
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
                    var stack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Left, MaxWidth = 340 };
                    if (!string.IsNullOrEmpty(message.Text))
                    {
                        stack.Children.Add(new Border
                        {
                            Background = ClayTheme.SurfaceHigher,
                            CornerRadius = ClayTheme.RadiusSmall,
                            Padding = new Thickness(12, 8),
                            Child = new TextBlock
                            {
                                Text = message.Text,
                                Foreground = ClayTheme.TextPrimary,
                                FontFamily = ClayTheme.FontBody,
                                FontSize = 13,
                                TextWrapping = TextWrapping.Wrap,
                            },
                        });
                    }
                    if (message.ToolCalls is { Count: > 0 })
                    {
                        foreach (var call in message.ToolCalls)
                        {
                            stack.Children.Add(new TextBlock
                            {
                                Text = $"🔧 {call.Name}",
                                Foreground = ClayTheme.TextMuted,
                                FontFamily = ClayTheme.FontMono,
                                FontSize = 11,
                            });
                        }
                    }
                    return stack.Children.Count > 0 ? stack : null;

                default:
                    return null;
            }
        }
    }
}
