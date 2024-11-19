using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;

namespace ImageScaner
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            string url = textBox1.Text;
            listBox1.Items.Clear();
            richTextBox1.Clear();
            label5.Text = "Початок аналізу...";
            List<string> imageUrls = await GetImageUrlsFromWebsite(url);

            foreach (var imageUrl in imageUrls)
            {
                listBox1.Items.Add(imageUrl);
                Bitmap bitmap = await DownloadImage(imageUrl);
                if (bitmap != null)
                {
                    pictureBox1.Image = bitmap;
                    bool hiddenData = AnalyzeImageForHiddenFiles(bitmap);
                    string hiddenText = ExtractHiddenText(bitmap);
                    richTextBox1.AppendText($"Image URL: {imageUrl}\nHidden data: {hiddenData}\nHidden text: {hiddenText}\n");
                    label5.Text = $"Аналіз зображення: {imageUrl}";
                }
            }

            label5.Text = "Аналіз завершено!";
        }

        private async Task<List<string>> GetImageUrlsFromWebsite(string url)
        {
            List<string> imageUrls = new List<string>();
            using (HttpClient client = new HttpClient())
            {
                string html = await client.GetStringAsync(url);
                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);
                foreach (var node in doc.DocumentNode.SelectNodes("//img[@src]"))
                {
                    string src = node.GetAttributeValue("src", null);
                    if (!string.IsNullOrEmpty(src))
                    {
                        imageUrls.Add(src);
                    }
                }
            }
            return imageUrls;
        }

        private async Task<Bitmap> DownloadImage(string imageUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    byte[] imageBytes = await client.GetByteArrayAsync(imageUrl);
                    using (MemoryStream ms = new MemoryStream(imageBytes))
                    {
                        return new Bitmap(ms);
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        private bool CheckForHiddenFiles(byte[] imageData)
        {
            string[] fileSignatures = { "504B0304", "52617221", "25504446" }; // Zip, Rar, PDF
            string hexString = BitConverter.ToString(imageData).Replace("-", "");

            foreach (string signature in fileSignatures)
            {
                if (hexString.Contains(signature))
                {
                    return true; 
                }
            }

            return false;
        }

        private bool AnalyzeImageForHiddenFiles(Bitmap bitmap)
        {
            byte[] imageData = ImageToByteArray(bitmap);
            return CheckForHiddenFiles(imageData);
        }

        private byte[] ImageToByteArray(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // Ensure the bitmap is in a compatible format
                Bitmap clone = new Bitmap(bitmap);
                clone.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }

        private string ExtractHiddenText(Bitmap bitmap)
        {
            List<byte> extractedBits = new List<byte>();
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    extractedBits.Add((byte)(pixel.R & 1)); // Extract LSB from Red channel
                    extractedBits.Add((byte)(pixel.G & 1)); // Extract LSB from Green channel
                    extractedBits.Add((byte)(pixel.B & 1)); // Extract LSB from Blue channel
                }
            }

            List<byte> byteList = new List<byte>();
            for (int i = 0; i < extractedBits.Count; i += 8)
            {
                byte b = 0;
                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    if (i + bitIndex < extractedBits.Count)
                    {
                        b = (byte)(b | (extractedBits[i + bitIndex] << (7 - bitIndex)));
                    }
                }
                byteList.Add(b);
            }

            string hiddenText = Encoding.UTF8.GetString(byteList.ToArray());

            int nullIndex = hiddenText.IndexOf('\0');
            if (nullIndex >= 0)
            {
                hiddenText = hiddenText.Substring(0, nullIndex);
            }

            return hiddenText;
        }
    }
}
