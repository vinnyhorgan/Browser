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

        static Font font;
        static Font h1Font;

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

            camera.target = new Vector2(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2);
            camera.zoom = 1.0f;
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

                    Texture2D texture;

                    if (!image.HasValue)
                    {
                        texture = Raylib.LoadTexture("assets/error.jpg");
                    }
                    else
                    {
                        texture = Raylib.LoadTextureFromImage(image.Value);
                    }

                    images.Add(src, texture);
                }

                if (node.ParentNode.Name == "h1")
                {
                    if (node.InnerText != "\n")
                    {
                        Raylib.DrawTextEx(h1Font, node.InnerText.Replace("\n", " "), new Vector2(0, y), 42, 0, Color.BLACK);
                        y += 40;
                    }
                }
                else if (node.ParentNode.Name == "li")
                {
                    if (node.InnerText != "\n")
                    {
                        Raylib.DrawTextEx(font, node.InnerText.Replace("\n", " "), new Vector2(0, y), 22, 0, Color.BLACK);

                        y += 22;
                    }
                }
                else if (node.ParentNode.Name == "p")
                {
                    if (node.InnerText != "\n")
                    {
                        Raylib.DrawTextEx(font, node.InnerText.Replace("\n", " "), new Vector2(0, y), 22, 0, Color.BLACK);
                        y += 22;
                    }
                }
                else if (node.ParentNode.Name == "a")
                {
                    if (Raylib.CheckCollisionPointRec(Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), camera), new Rectangle(0, y, Raylib.MeasureText(node.InnerText.Replace("\n", " "), 22), 22)))
                    {
                        Raylib.DrawTextEx(font, node.InnerText.Replace("\n", " "), new Vector2(0, y), 22, 0, Color.SKYBLUE);

                        y += 22;

                        if (Raylib.IsMouseButtonPressed(MouseButton.MOUSE_BUTTON_LEFT))
                        {
                            LoadPage(node.ParentNode.Attributes["href"].Value);
                        }
                    }
                    else
                    {
                        Raylib.DrawTextEx(font, node.InnerText.Replace("\n", " "), new Vector2(0, y), 22, 0, Color.BLUE);
                        y += 22;
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

            font = Raylib.LoadFontEx("assets/times_new_roman.ttf", 22, null, 250);
            h1Font = Raylib.LoadFontEx("assets/times_new_roman.ttf", 42, null, 250);

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
                    camera.target = new Vector2(camera.target.X, camera.target.Y + Raylib.GetMouseWheelMove() * -22);
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
                var content = await http.GetStringAsync(url);

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

            try
            {
                var content = await http.GetByteArrayAsync(url);

                var image = LoadImageFromMemory(".png", content);

                return image;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.Message);

                return null;
            }
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