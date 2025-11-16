using OpenAI;
using OpenAI.Chat;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenAI.Images;

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

        public MainWindow()
        {
            InitializeComponent();

            // ★ APIキーを設定（環境変数がオススメ）
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            _client = new OpenAIClient(apiKey);
            _chatMini = _client.GetChatClient("gpt-4.1-mini");
            _imageClient = _client.GetImageClient("gpt-image-1");
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string userMessage = InputTextBox.Text;

            if (string.IsNullOrWhiteSpace(userMessage))
            {
                MessageBox.Show("メッセージを入力してください");
                return;
            }

            ResponseTextBox.Text = "GPT に問い合わせ中…";

            // 1. 画像リクエスト判定（安いモデル）
            var judge = await _chatMini.CompleteChatAsync(
                $"次の文章は画像生成の依頼ですか？ yes/no で答えてください。\n\n「{userMessage}」"
            );

            bool isImageRequest = judge.Value.Content[0].Text.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
            if(isImageRequest)
            {
                try
                {
                    GeneratedImage.Source = null;
                    var imageUrl = await GenerateImageUrlAsync(userMessage);
                    
                    // ★ URL から BitmapImage を作って表示
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = imageUrl;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    GeneratedImage.Source = bitmap;
                }
                catch(Exception ex)
                {
                    MessageBox.Show("画像生成中にエラー: " + ex.Message);
                }
            }
            else
            {
                try
                {
                    var chat = _client.GetChatClient("gpt-5.1");

                    var response = await chat.CompleteChatAsync(userMessage);

                    string assistantText = response.Value.Content[0].Text;

                    ResponseTextBox.Text = assistantText;
                }
                catch (Exception ex)
                {
                    ResponseTextBox.Text = $"エラー: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// プロンプトから画像URLを1枚生成して返す「自作API」。
        /// </summary>
        public async Task<Uri> GenerateImageUrlAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("プロンプトが空です。", nameof(prompt));

            var options = new ImageGenerationOptions
            {
                // 画像サイズ（お好みで 512x512 なども可）
                Size = GeneratedImageSize.W512xH512,
                // URL で返してもらう
                ResponseFormat = GeneratedImageFormat.Uri
            };

            // 画像生成API呼び出し
            var response = await _imageClient.GenerateImageAsync(prompt);

            // 1枚だけ生成する前提
            return response.Value.ImageUri;
        }
    }
}