using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using NovaIsland.Application.Modules;
using NovaIsland.Domain.Ai;

namespace NovaIsland.Panels;

public sealed partial class AiPanel : UserControl, IDisposable
{
    private readonly IAiProvider? _aiProvider;
        private CancellationTokenSource? _cts;

        public AiPanel()
        {
            this.InitializeComponent();
        }

        public AiPanel(IAiProvider aiProvider) : this()
        {
            _aiProvider = aiProvider;
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SubmitMessageAsync();
        }

        private async void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await SubmitMessageAsync();
            }
        }

        private async Task SubmitMessageAsync()
        {
            if (_aiProvider == null || string.IsNullOrWhiteSpace(InputTextBox.Text)) return;

            var prompt = InputTextBox.Text;
            InputTextBox.Text = string.Empty;
            OutputTextBlock.Text += $"\nUser: {prompt}\nAI: ";

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var messages = new List<AiMessage> { new AiMessage("user", prompt) };

            try
            {
                await foreach (var token in _aiProvider.GetResponseStreamAsync(messages, _cts.Token))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        OutputTextBlock.Text += token;
                    });
                }
                OutputTextBlock.Text += "\n";
            }
            catch (OperationCanceledException)
            {
                // Normal
            }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                OutputTextBlock.Text += $"\n[Error: {ex.Message}]";
            });
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
