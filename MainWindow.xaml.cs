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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly OpenAIClient _client;
        private readonly ChatClient _chatMini;
        private readonly ImageClient _imageClient;
        private readonly List<ChatMessage> _messages = [];
        private readonly ObservableCollection<ChatMessageModel> _chatItems = [];

        public MainWindow()
        {
            InitializeComponent();

            // ★ APIキーを設定（環境変数がオススメ）
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            _client = new OpenAIClient(apiKey);
            _chatMini = _client.GetChatClient("gpt-4.1-mini");
            _imageClient = _client.GetImageClient("gpt-image-1");
            ChatList.ItemsSource = _chatItems;

            // ユーザー情報をファイルからロード
            string userInfoPath = "../../../UserInfo.txt";
            if (File.Exists(userInfoPath))
            {
                var userInfo = File.ReadAllText(userInfoPath, Encoding.UTF8);
                _messages.Add(ChatMessage.CreateSystemMessage(userInfo));
            }
        }
        public class MessageItem
        {
            public string Text { get; set; }
            public Brush Background { get; set; }
        }
       
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {

            string userMessage = InputTextBox.Text;

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                MessageBox.Show("メッセージを入力してください");
                return;
            }
            InputTextBox.Clear();
            InputTextBox.Focus();

            //ResponseTextBox.Text = "GPT に問い合わせ中…";

            // 1. 画像リクエスト判定（安いモデル）
            var judge = await _chatMini.CompleteChatAsync(
                $"次の文章は画像生成の依頼ですか？ yes/no で答えてください。\n\n「{userMessage}」"
            );

            // ユーザーのメッセージを表示＆履歴追加
            AddMessage(userMessage, isUser: true);
            _messages.Add(ChatMessage.CreateUserMessage(userMessage));

            bool isImageRequest = judge.Value.Content[0].Text.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
            if (isImageRequest)
            {
                try
                {
                    GeneratedImage.Source = null;
                    var bitmap = await GenerateImageUrlAsync(userMessage);

                    GeneratedImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("画像生成中にエラー: " + ex.Message);
                }
            }
            else
            {
                try
                {
                    var chat = _client.GetChatClient("gpt-5.1");

                    var response = await chat.CompleteChatAsync(_messages);

                    string assistantText = response.Value.Content[0].Text;

                    // ユーザーのメッセージを表示＆履歴追加
                    // ここを変更
                    AddParsedAssistantResponse(assistantText);

                    // 履歴にも保存
                    _messages.Add(ChatMessage.CreateAssistantMessage(assistantText));
                }
                catch (Exception ex)
                {
                }
            }
        }

        /// <summary>
        /// プロンプトから画像URLを1枚生成して返す「自作API」。
        /// </summary>
        public async Task<BitmapImage> GenerateImageUrlAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("プロンプトが空です。", nameof(prompt));

            var options = new ImageGenerationOptions
            {
                Size = GeneratedImageSize.W512xH512,
            };

            // 画像生成API呼び出し
            var response = await _imageClient.GenerateImageAsync(prompt, options);

            // ★ バイナリデータを直接扱う
            var bytes = response?.Value?.ImageBytes;
            if (bytes == null || bytes.IsEmpty)
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

        private void AddMessage(string text, bool isUser)
        {
            _chatItems.Add(new ChatMessageModel
            {
                Text = text,
                Type = "text",
                Background = isUser ? Brushes.LightBlue : Brushes.LightGray
            });

            // Dispatcherを使ってUI描画が完了してからスクロール
            Dispatcher.InvokeAsync(() =>
            {
                if (VisualTreeHelper.GetChild(ChatList, 0) is Border border &&
                    border.Child is ScrollViewer scroll)
                {
                    scroll.ScrollToEnd();
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }


        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ChatMessageModel msg)
                Clipboard.SetText(msg.Text);
        }

        private void AddParsedAssistantResponse(string rawText)
        {
            // コードブロックを抽出する正規表現
            // ```lang\n ... \n``` の形式を想定
            var pattern = @"```(.*?)\n(.*?)```";
            var matches = System.Text.RegularExpressions.Regex.Matches(
                rawText, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);

            if (matches.Count == 0)
            {
                // コードブロックがない：通常テキストとして追加
                AddMessage(rawText, isUser: false);
                return;
            }

            int lastIndex = 0;
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // コードブロックの前の通常文
                if (match.Index > lastIndex)
                {
                    var beforeText = rawText.Substring(lastIndex, match.Index - lastIndex);
                    if (!string.IsNullOrWhiteSpace(beforeText))
                        AddMessage(beforeText.Trim(), isUser: false);
                }

                // コード部分
                string language = match.Groups[1].Value.Trim();
                string code = match.Groups[2].Value.Trim();

                _chatItems.Add(new ChatMessageModel
                {
                    Text = code,
                    Type = "code",
                    Language = language,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) // 黒系
                });

                lastIndex = match.Index + match.Length;
            }

            // 最後の部分（コード後のテキスト）
            if (lastIndex < rawText.Length)
            {
                var tailText = rawText.Substring(lastIndex);
                if (!string.IsNullOrWhiteSpace(tailText))
                    AddMessage(tailText.Trim(), isUser: false);
            }
        }

        private void CodeEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ICSharpCode.AvalonEdit.TextEditor editor &&
                editor.DataContext is ChatMessageModel msg)
            {
                // 内容セット
                editor.Text = msg.Text;

                // 背景色・文字色の調整
                editor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)); // ダーク背景
                editor.Foreground = Brushes.Gainsboro;

                // 行番号をON（任意）
                editor.ShowLineNumbers = true;

                // キャレット（カーソル）と選択色
                editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(80, 0, 120, 215));
                editor.TextArea.SelectionBorder = null;
                editor.TextArea.Caret.CaretBrush = Brushes.White;
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
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.None)
            {
                // 改行を許可
            }
        }


    }
}