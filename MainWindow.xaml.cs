using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace wpf
{
    public partial class MainWindow : Window
    {
        // ===== OpenAI 関連 =====
        private readonly OpenAIClient _client;
        private readonly ChatClient _chat51;
        private readonly ChatClient _chatMini;
        private readonly ImageClient _imageClient;

        // ===== チャット状態 =====
        private readonly List<ChatMessage> _messages = [];
        private readonly ObservableCollection<ChatMessageModel> _chatItems = [];

        // ===== 定数 =====
        private const string UserInfoPath = "../../../UserInfo.txt";
        private const string MainModelName = "gpt-5.1";
        private const string MiniModelName = "gpt-4.1-mini";
        private const string ImageModelName = "gpt-image-1";

        public MainWindow()
        {
            InitializeComponent();

            _client = CreateOpenAIClient();
            _chat51 = _client.GetChatClient(MainModelName);
            _chatMini = _client.GetChatClient(MiniModelName);
            _imageClient = _client.GetImageClient(ImageModelName);

            ChatList.ItemsSource = _chatItems;

            LoadUserInfoIfExists(UserInfoPath);
        }

        #region 初期化系

        private static OpenAIClient CreateOpenAIClient()
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("環境変数 OPENAI_API_KEY が設定されていません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InvalidOperationException("OPENAI_API_KEY is not set.");
            }

            return new OpenAIClient(apiKey);
        }

        private void LoadUserInfoIfExists(string path)
        {
            if (!File.Exists(path))
                return;

            try
            {
                var userInfo = File.ReadAllText(path, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(userInfo))
                {
                    _messages.Add(ChatMessage.CreateSystemMessage(userInfo));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ユーザー情報の読み込みに失敗しました: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region イベントハンドラ

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var userMessage = InputTextBox.Text;

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                MessageBox.Show("メッセージを入力してください");
                return;
            }

            InputTextBox.Clear();
            InputTextBox.Focus();

            // UI 表示 & 履歴追加
            AddMessage(userMessage, isUser: true);
            _messages.Add(ChatMessage.CreateUserMessage(userMessage));

            // TODO: 画像判定を行いたい場合は、ここを true/判定ロジックに差し替え
            bool isImageRequest = false;

            if (isImageRequest)
            {
                await HandleImageRequestAsync(userMessage);
            }
            else
            {
                await HandleChatRequestAsync();
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Enter → 送信
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
            // Enter単体 → 改行（デフォルト動作）
        }

        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not ChatMessageModel msg)
                return;

            Clipboard.SetText(msg.Text ?? string.Empty);
        }

        private void CodeEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ICSharpCode.AvalonEdit.TextEditor editor ||
                editor.DataContext is not ChatMessageModel msg)
                return;

            editor.Text = msg.Text;

            editor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            editor.Foreground = Brushes.Gainsboro;
            editor.ShowLineNumbers = true;

            editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(80, 0, 120, 215));
            editor.TextArea.SelectionBorder = null;
            editor.TextArea.Caret.CaretBrush = Brushes.White;
        }

        private void Image_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Image img || img.Source is not BitmapImage bmp)
                return;

            var window = new Window
            {
                Title = "画像プレビュー",
                Width = bmp.PixelWidth,
                Height = bmp.PixelHeight,
                Content = new Image { Source = bmp, Stretch = Stretch.Uniform },
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            window.ShowDialog();
        }

        #endregion

        #region チャット処理

        private async Task HandleChatRequestAsync()
        {
            try
            {
                var response = await _chat51.CompleteChatAsync(_messages);
                var content = response.Value.Content;

                if (content is null || content.Count == 0)
                {
                    AddMessage("応答が空でした。", isUser: false);
                    return;
                }

                var assistantText = content[0].Text ?? string.Empty;

                AddParsedAssistantResponse(assistantText);
                _messages.Add(ChatMessage.CreateAssistantMessage(assistantText));
            }
            catch (Exception ex)
            {
                AddMessage($"エラーが発生しました: {ex.Message}", isUser: false);
            }
        }

        private async Task HandleImageRequestAsync(string prompt)
        {
            try
            {
                var bitmap = await GenerateImageAsync(prompt);

                _chatItems.Add(new ChatMessageModel
                {
                    Type = "image",
                    Image = bitmap,
                    Background = Brushes.Transparent,
                    Role = ChatRole.Assistant
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("画像生成中にエラー: " + ex.Message);
            }
        }

        #endregion

        #region 画像生成

        public async Task<BitmapImage> GenerateImageAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("プロンプトが空です。", nameof(prompt));

            var options = new ImageGenerationOptions
            {
                Size = GeneratedImageSize.W1024xH1024,
            };

            var response = await _imageClient.GenerateImageAsync(prompt, options);
            var bytes = response?.Value?.ImageBytes;

            if (bytes is null || bytes.IsEmpty)
                throw new Exception("画像データが空です。");

            using var ms = new MemoryStream(bytes.ToArray());

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();

            return image;
        }

        #endregion

        #region 表示系

        private void AddMessage(string text, bool isUser)
        {
            _chatItems.Add(new ChatMessageModel
            {
                Text = text,
                Type = "text",
                Background = isUser ? Brushes.LightBlue : Brushes.LightGray,
                Role = isUser ? ChatRole.User : ChatRole.Assistant
            });

            // Dispatcherを使ってUI描画が完了してからスクロール
            Dispatcher.InvokeAsync(
                ScrollChatToEnd,
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ScrollChatToEnd()
        {
            if (VisualTreeHelper.GetChild(ChatList, 0) is not Border border)
                return;

            if (border.Child is not ScrollViewer scroll)
                return;

            scroll.ScrollToEnd();
        }

        private void AddParsedAssistantResponse(string rawText)
        {
            // ```lang\n ... \n``` の形式を検出
            const string pattern = "```(.*?)\\n(.*?)```";
            var matches = System.Text.RegularExpressions.Regex.Matches(
                rawText,
                pattern,
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (matches.Count == 0)
            {
                AddMessage(rawText, isUser: false);
                return;
            }

            var lastIndex = 0;

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // コードブロックの前の通常テキスト
                if (match.Index > lastIndex)
                {
                    var beforeText = rawText.Substring(lastIndex, match.Index - lastIndex);
                    if (!string.IsNullOrWhiteSpace(beforeText))
                    {
                        AddMessage(beforeText.Trim(), isUser: false);
                    }
                }

                // コードブロック
                var language = match.Groups[1].Value.Trim();
                var code = match.Groups[2].Value.Trim();

                _chatItems.Add(new ChatMessageModel
                {
                    Text = code,
                    Type = "code",
                    Language = language,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Role = ChatRole.Assistant
                });

                lastIndex = match.Index + match.Length;
            }

            // 最後の通常テキスト
            if (lastIndex < rawText.Length)
            {
                var tailText = rawText.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(tailText))
                {
                    AddMessage(tailText.Trim(), isUser: false);
                }
            }
        }

        #endregion
    }
}