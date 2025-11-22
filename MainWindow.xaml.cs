using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
        private readonly ObservableCollection<MessageItem> _chatItems = [];
        private readonly List<ChatMessage> _messages = [];

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
                    AddMessage(assistantText, isUser: false);
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
            _chatItems.Add(new MessageItem
            {
                Text = text,
                Background = isUser ? Brushes.LightBlue : Brushes.LightGray
            });

            // スクロールを一番下に
            ChatList.UpdateLayout();
            if (VisualTreeHelper.GetChild(ChatList, 0) is Border border &&
                border.Child is ScrollViewer scroll)
            {
                scroll.ScrollToEnd();
            }
        }
    }
}