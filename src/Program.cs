using System;
using Raylib_cs;
using Jint;
using System.Net.Http;
using HtmlAgilityPack;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Numerics;

namespace Browser
{
    class Program
    {
        static HttpClient http = new HttpClient();

        static Stack<string> history = new Stack<string>();
        static HtmlDocument currentPage = null;
        static Dictionary<string, Texture2D> images = new Dictionary<string, Texture2D>();

        static Camera2D camera;
        static int y = 0;

        static void LoadPage(string url)
        {
            if (url.StartsWith("/"))
            {
                url = history.Peek() + url;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("/"))
            {
                url = history.Peek() + "/" + url;
            }

            Console.WriteLine("Loading " + url + "...");

            var htmlTask = RequestHtml(url);

            Raylib.SetWindowTitle("Browser - Loading");

            while (!htmlTask.IsCompleted) { }

            var html = htmlTask.Result;

            var title = html.DocumentNode.SelectSingleNode("//title").InnerText;

            Raylib.SetWindowTitle("Browser - " + title);

            currentPage = html;
            history.Push(url);
        }

        static void Render(HtmlNode node)
        {
            if (node.ChildNodes.Count == 0)
            {
                if (node.Name == "img")
                {
                    var src = node.Attributes["src"].Value;

                    if (!src.StartsWith("http://") && !src.StartsWith("https://") && !src.StartsWith("/"))
                    {
                        src = history.Peek() + "/" + src;
                    }

                    if (images.ContainsKey(src))
                    {
                        Raylib.DrawTexture(images[src], 0, y, Color.WHITE);

                        y += images[src].height;

                        return;
                    }

                    var imageTask = RequestImage(src);

                    while (!imageTask.IsCompleted) { }

                    var image = imageTask.Result;

                    var texture = Raylib.LoadTextureFromImage(image.Value);

                    images.Add(src, texture);

                    Raylib.DrawTexture(texture, 0, y, Color.WHITE);

                    y += texture.height;
                }

                if (node.ParentNode.Name == "p")
                {
                    if (node.InnerText != "\n")
                    {
                        Raylib.DrawText(node.ParentNode.Name + " -> " + node.InnerText.Replace("\n", " "), 0, y, 20, Color.BLACK);
                        y += 20;
                    }
                }
                else if (node.ParentNode.Name == "a")
                {
                    if (Raylib.CheckCollisionPointRec(Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), camera), new Rectangle(0, y, Raylib.MeasureText(node.ParentNode.Name + " -> " + node.InnerText.Replace("\n", " "), 20), 20)))
                    {
                        Raylib.DrawText(node.ParentNode.Name + " -> " + node.InnerText.Replace("\n", " "), 0, y, 20, Color.SKYBLUE);
                        y += 20;

                        if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT))
                        {
                            LoadPage(node.ParentNode.Attributes["href"].Value);
                        }
                    }
                    else
                    {
                        Raylib.DrawText(node.ParentNode.Name + " -> " + node.InnerText.Replace("\n", " "), 0, y, 20, Color.BLUE);
                        y += 20;
                    }
                }
            }
            else
            {
                foreach (var child in node.ChildNodes)
                {
                    Render(child);
                }
            }
        }

        static void Main(string[] args)
        {
            Raylib.SetConfigFlags(ConfigFlags.FLAG_WINDOW_RESIZABLE);
            Raylib.InitWindow(800, 600, "Browser");
            Raylib.SetTargetFPS(60);

            camera = new Camera2D();
            camera.target = new Vector2(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2);
            camera.offset = new Vector2(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2);
            camera.rotation = 0.0f;
            camera.zoom = 1.0f;

            var engine = new Engine();
            engine.SetValue("log", new Action<object>(Console.WriteLine));

            engine.Execute("log('Hello World!');");

            LoadPage("http://www.toad.com");

            while (!Raylib.WindowShouldClose())
            {
                if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_RIGHT))
                {
                    if (history.Count > 1)
                    {
                        history.Pop();

                        LoadPage(history.Peek());
                    }
                }

                if (Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_CONTROL) && Raylib.GetMouseWheelMove() != 0)
                {
                    camera.zoom += Raylib.GetMouseWheelMove() / 10.0f;
                }

                if (Raylib.GetMouseWheelMove() != 0)
                {
                    camera.target = new Vector2(camera.target.X, camera.target.Y + Raylib.GetMouseWheelMove() * -20);
                }

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.WHITE);

                Raylib.BeginMode2D(camera);

                if (currentPage !=  null)
                {
                    y = 0;

                    var body = currentPage.DocumentNode.SelectSingleNode("/html/body");

                    Render(body);
                }

                Raylib.EndMode2D();

                Raylib.EndDrawing();
            }

            Raylib.CloseWindow();
        }

        static async Task<string> Request(string url)
        {
            try
            {
                var response = await http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                return content;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.Message);

                return null;
            }
        }

        static unsafe Image LoadImageFromMemory(string fileType, byte[] fileData)
        {
            using var fileTypeNative = fileType.ToUTF8Buffer();

            byte* fileDataNative = (byte*)Raylib.MemAlloc(fileData.Length);
            Marshal.Copy(fileData, 0, (IntPtr)fileDataNative, fileData.Length);

            Image image = Raylib.LoadImageFromMemory(fileTypeNative.AsPointer(), fileDataNative, fileData.Length);

            return image;
        }

        static async Task<Image?> RequestImage(string url)
        {
            Console.WriteLine("Requesting image: " + url);

            var content = await http.GetByteArrayAsync(url);

            if (content == null)
            {
                return null;
            }

            var image = LoadImageFromMemory(".png", content);

            return image;
        }

        static async Task<HtmlDocument> RequestHtml(string url)
        {
            var content = await Request(url);

            if (content == null)
            {
                return null;
            }

            var html = new HtmlDocument();
            html.LoadHtml(content);

            return html;
        }
    }
}