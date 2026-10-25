// CPU Logic Pet - тамагочи для рабочего стола: Лоджи и Ева.
// Клик — сердечки, звуки и ответы; сами говорят фразы, замечают, когда ты отошёл (АФК), моргают.
// Если на экране обе, а ты АФК — разговаривают друг с другом.
// Два вида у каждой: в окне или без фона (только персонаж).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WF = System.Windows.Forms;

namespace CPULogicPet
{
    public static class Program
    {
        public static readonly string DataDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CPU-Logic");
        public static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;

        [STAThread]
        public static void Main()
        {
            bool created;
            var mutex = new Mutex(true, "CPULogicPet_SingleInstance", out created);
            if (!created) return;
            Directory.CreateDirectory(DataDir);

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += (s, e) =>
            {
                File.WriteAllText(System.IO.Path.Combine(DataDir, "pet-error.log"), e.Exception.ToString(), Encoding.UTF8);
                MessageBox.Show("Что-то сломалось:\n" + e.Exception.Message, "CPU Logic Pet");
                e.Handled = true;
            };
            var director = new Director();
            director.Start();
            app.Run();
            GC.KeepAlive(mutex);
        }

        // Достаёт встроенный файл рядом с программой, если его там ещё нет (чтобы его можно было редактировать)
        public static string ExtractIfMissing(string name)
        {
            var path = System.IO.Path.Combine(AppDir, name);
            if (!File.Exists(path))
            {
                using (var res = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                using (var file = File.Create(path)) res.CopyTo(file);
            }
            return path;
        }

        // Дописывает в файл рядом с программой разделы [..], которые появились во встроенной версии,
        // не трогая то, что пользователь уже поправил. Строки до первого заголовка считаются разделом defaultSection.
        public static string PrepareFile(string name, string defaultSection)
        {
            var path = ExtractIfMissing(name);
            var have = new HashSet<string>();
            string cur = defaultSection;
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { cur = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant(); have.Add(cur); continue; }
                if (cur != null) have.Add(cur);
            }

            string embedded;
            using (var res = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var reader = new StreamReader(res, Encoding.UTF8)) embedded = reader.ReadToEnd();
            var add = new StringBuilder();
            string sec = null;
            foreach (var raw in embedded.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    sec = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    if (!have.Contains(sec)) add.Append("\r\n\r\n[" + sec + "]\r\n");
                    continue;
                }
                if (sec != null && !have.Contains(sec)) add.Append(line + "\r\n");
            }
            if (add.Length > 0) File.AppendAllText(path, add.ToString(), new UTF8Encoding(false));
            return path;
        }

        public static void After(int ms, Action action)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (s, e) => { t.Stop(); action(); };
            t.Start();
        }
    }

    // ---------- описание персонажа ----------

    public class CharacterDef
    {
        public string Id, Name, Title, PhrasesFile, VoicePitch, BackgroundTip;
        public string ImgOpen, ImgClosed, CutOpen, CutClosed;
        public double ImageWidth, ImageHeight, CutWidth, CutHeight, DefaultScale, DefaultRightOffset;
        public Color Accent, BubbleBack, BubbleText, TitleText, CounterText;
        public string[] HeartGlyphs, HeartColors;
        public string CounterGlyph;
        public double[][] ClickChirps;
        public double[] TalkChirp;
        public double ChirpPitch;

        public static CharacterDef Loji()
        {
            return new CharacterDef
            {
                Id = "loji", Name = "Лоджи", Title = "中央处理器>/逻辑", PhrasesFile = "phrases-loji.txt",
                ImgOpen = "tama-open.png", ImgClosed = "tama-closed.png", CutOpen = "cut-open.png", CutClosed = "cut-closed.png",
                ImageWidth = 712, ImageHeight = 526, CutWidth = 712, CutHeight = 526, DefaultScale = 0.5, DefaultRightOffset = 420,
                Accent = Color.FromRgb(205, 0, 0), BubbleBack = Color.FromArgb(242, 18, 0, 0),
                BubbleText = Color.FromRgb(255, 217, 217), TitleText = Color.FromRgb(230, 30, 30), CounterText = Color.FromRgb(255, 107, 133),
                HeartGlyphs = new[] { "❤", "♥" }, HeartColors = new[] { "#FF3B5C", "#FF7A9A", "#E00000", "#FFB3C6", "#FF1E1E" },
                CounterGlyph = "♥", VoicePitch = "+40%", ChirpPitch = 1.0,
                BackgroundTip = "Теперь я без рамки! Правый клик по мне — меню ❤"
            };
        }

        public static CharacterDef Eva()
        {
            return new CharacterDef
            {
                Id = "eva", Name = "Ева", Title = "ЕВРАЗИЯ>/ПОРЯДОК", PhrasesFile = "phrases-eva.txt",
                ImgOpen = "eva-open.png", ImgClosed = "eva-closed.png", CutOpen = "eva-cut-open.png", CutClosed = "eva-cut-closed.png",
                ImageWidth = 763, ImageHeight = 821, CutWidth = 730, CutHeight = 895, DefaultScale = 0.4, DefaultRightOffset = 820,
                Accent = Color.FromRgb(222, 176, 24), BubbleBack = Color.FromArgb(242, 10, 12, 14),
                BubbleText = Color.FromRgb(250, 238, 200), TitleText = Color.FromRgb(232, 186, 30), CounterText = Color.FromRgb(255, 214, 90),
                HeartGlyphs = new[] { "✷", "❤", "★" }, HeartColors = new[] { "#E8B820", "#FFD966", "#FFF4C2", "#4DA3FF", "#FF5A7A" },
                CounterGlyph = "✷", VoicePitch = "-5%", ChirpPitch = 0.75,
                BackgroundTip = "Рамка снята. Меню — правым кликом по мне."
            };
        }
    }

    // ---------- настройки персонажа ----------

    public class Settings
    {
        public double Left = double.NaN, Top = double.NaN, Scale = double.NaN;
        public bool Sound = true, Voice = false, Topmost = true, NoBackground = false, Visible = true;
        public int Love = 0;
        readonly string path;

        Settings(string path) { this.path = path; }

        public static Settings Load(string id)
        {
            var path = System.IO.Path.Combine(Program.DataDir, "pet-" + id + ".ini");
            // Лоджи раньше хранила настройки в pet.ini
            var legacy = System.IO.Path.Combine(Program.DataDir, "pet.ini");
            if (id == "loji" && !File.Exists(path) && File.Exists(legacy)) File.Copy(legacy, path);

            var s = new Settings(path);
            if (!File.Exists(path)) return s;
            var inv = CultureInfo.InvariantCulture;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                double d; bool b; int n;
                switch (k)
                {
                    case "Left": if (double.TryParse(v, NumberStyles.Float, inv, out d)) s.Left = d; break;
                    case "Top": if (double.TryParse(v, NumberStyles.Float, inv, out d)) s.Top = d; break;
                    case "Scale": if (double.TryParse(v, NumberStyles.Float, inv, out d)) s.Scale = d; break;
                    case "Sound": if (bool.TryParse(v, out b)) s.Sound = b; break;
                    case "Voice": if (bool.TryParse(v, out b)) s.Voice = b; break;
                    case "Topmost": if (bool.TryParse(v, out b)) s.Topmost = b; break;
                    case "NoBackground": if (bool.TryParse(v, out b)) s.NoBackground = b; break;
                    case "Visible": if (bool.TryParse(v, out b)) s.Visible = b; break;
                    case "Love": if (int.TryParse(v, out n)) s.Love = n; break;
                }
            }
            return s;
        }

        public void Save()
        {
            var inv = CultureInfo.InvariantCulture;
            var lines = new[]
            {
                "Left=" + Left.ToString(inv), "Top=" + Top.ToString(inv), "Scale=" + Scale.ToString(inv),
                "Sound=" + Sound, "Voice=" + Voice, "Topmost=" + Topmost, "NoBackground=" + NoBackground,
                "Visible=" + Visible, "Love=" + Love
            };
            try { File.WriteAllLines(path, lines, Encoding.UTF8); } catch { }
        }
    }

    // ---------- фразы ----------

    public class Phrases
    {
        static readonly string[] Special = { "afk", "back", "click", "spam", "night", "jealous", "smug" };
        readonly Dictionary<string, List<string>> sections = new Dictionary<string, List<string>>();
        readonly Random rnd;
        public readonly string FilePath;

        public Phrases(string fileName, Random rnd)
        {
            this.rnd = rnd;
            FilePath = Program.PrepareFile(fileName, null);
            string current = null;
            foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    current = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    if (!sections.ContainsKey(current)) sections[current] = new List<string>();
                    continue;
                }
                if (current != null) sections[current].Add(line);
            }
        }

        public string Pick(string section)
        {
            List<string> list;
            if (!sections.TryGetValue(section, out list) || list.Count == 0) return null;
            return list[rnd.Next(list.Count)];
        }

        // Половина — про любовь, остальное поровну между тематическими разделами
        public string PickRandom()
        {
            if (DateTime.Now.Hour < 5 && rnd.NextDouble() < 0.35)
            {
                var night = Pick("night");
                if (night != null) return night;
            }
            var themes = sections.Keys.Where(k => k != "love" && !Special.Contains(k) && sections[k].Count > 0).ToList();
            if (themes.Count == 0 || rnd.NextDouble() < 0.5) return Pick("love");
            return Pick(themes[rnd.Next(themes.Count)]);
        }
    }

    // ---------- диалоги ----------

    public class DialogLine { public string Speaker, Text; }

    // Разделы: [afk], [active], [eva_joins], [loji_joins]. Диалоги без заголовка (старый формат) — это [afk].
    public static class Dialogs
    {
        public static Dictionary<string, List<List<DialogLine>>> Load(out string path)
        {
            path = Program.PrepareFile("dialogs.txt", "afk");
            var all = new Dictionary<string, List<List<DialogLine>>>();
            string section = "afk";
            var cur = new List<DialogLine>();
            Action flush = () =>
            {
                if (cur.Count == 0) return;
                if (!all.ContainsKey(section)) all[section] = new List<List<DialogLine>>();
                all[section].Add(cur);
                cur = new List<DialogLine>();
            };
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.StartsWith("#")) continue;
                if (line.Length == 0) { flush(); continue; }
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    flush();
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    continue;
                }
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                cur.Add(new DialogLine { Speaker = line.Substring(0, colon).Trim(), Text = line.Substring(colon + 1).Trim() });
            }
            flush();
            return all;
        }
    }

    // ---------- звуки ----------

    public static class Sounds
    {
        public static SoundPlayer Chirp(double[] freqs, int ms, double slide)
        {
            const int rate = 22050;
            var pcm = new MemoryStream();
            var data = new BinaryWriter(pcm);
            foreach (var f in freqs)
            {
                int n = rate * ms / 1000;
                double phase = 0;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / n;
                    phase += 2 * Math.PI * f * (1 + slide * t) / rate;
                    double env = Math.Min(1.0, i / (rate * 0.004)) * Math.Pow(1 - t, 1.6);
                    double v = (Math.Sin(phase) + 0.25 * Math.Sin(2 * phase)) * env * 0.33;
                    data.Write((short)(v * 32767));
                }
                for (int i = 0; i < rate * 15 / 1000; i++) data.Write((short)0);
            }
            data.Flush();
            var bytes = pcm.ToArray();
            var wav = new MemoryStream();
            var w = new BinaryWriter(wav);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes.Length);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes.Length); w.Write(bytes);
            w.Flush();
            wav.Position = 0;
            var player = new SoundPlayer(wav);
            player.Load();
            return player;
        }

        public static double[] Scale(double[] freqs, double k)
        {
            return freqs.Select(f => f * k).ToArray();
        }
    }

    // ---------- режиссёр: трей, АФК, очередь реплик, диалоги ----------

    public class Director
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        static uint IdleMs()
        {
            var info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(info);
            if (!GetLastInputInfo(ref info)) return 0;
            return (uint)Environment.TickCount - info.dwTime;
        }

        const double AfkAfterMs = 3 * 60 * 1000;
        public readonly Random Rnd = new Random();
        public readonly List<PetWindow> Pets = new List<PetWindow>();
        Dictionary<string, List<List<DialogLine>>> dialogs;
        public string DialogsPath;
        bool started;
        DateTime jealousCooldown = DateTime.MinValue;
        readonly Dictionary<PetWindow, Queue<DateTime>> clicksOn = new Dictionary<PetWindow, Queue<DateTime>>();

        WF.NotifyIcon tray;
        readonly Dictionary<PetWindow, WF.MenuItem> trayItems = new Dictionary<PetWindow, WF.MenuItem>();
        DispatcherTimer talkTimer, idleTimer, dialogTimer;
        bool afk;
        DateTime nextAfkTalk = DateTime.MaxValue;
        List<DialogLine> dialog;
        int dialogPos;

        public bool DialogRunning { get { return dialog != null; } }

        public void Start()
        {
            // старый файл фраз Лоджи переименовываем, чтобы правки не потерялись
            var oldLoji = System.IO.Path.Combine(Program.AppDir, "phrases.txt");
            var newLoji = System.IO.Path.Combine(Program.AppDir, "phrases-loji.txt");
            if (File.Exists(oldLoji) && !File.Exists(newLoji)) File.Move(oldLoji, newLoji);
            dialogs = Dialogs.Load(out DialogsPath);

            Pets.Add(new PetWindow(CharacterDef.Loji(), this));
            Pets.Add(new PetWindow(CharacterDef.Eva(), this));
            BuildTray();
            foreach (var p in Pets) if (p.Cfg.Visible) p.Show();
            if (!Pets.Any(p => p.IsVisible))
                tray.ShowBalloonTip(3000, "CPU Logic Pet", "Мы в трее. Нажми на значок, чтобы нас вернуть ❤", WF.ToolTipIcon.None);

            // пока ты за компьютером: обычно одна фраза, а если на экране обе — иногда разговор между ними
            talkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            talkTimer.Tick += (s, e) =>
            {
                talkTimer.Interval = TimeSpan.FromSeconds(Rnd.Next(25, 100));
                if (afk || DialogRunning) return;
                var vis = Visible();
                if (vis.Count >= 2 && Rnd.NextDouble() < 0.3 && StartDialog("active")) return;
                if (vis.Count > 0) { var p = vis[Rnd.Next(vis.Count)]; p.Say(p.Phrases.PickRandom()); }
            };
            talkTimer.Start();

            idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            idleTimer.Tick += (s, e) => CheckIdle();
            idleTimer.Start();

            dialogTimer = new DispatcherTimer();
            dialogTimer.Tick += (s, e) => { dialogTimer.Stop(); NextDialogLine(); };

            started = true;

            // для проверки: --dialog-now сразу проигрывает диалог; --selftest прогоняет появление Евы и ревность
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--dialog-now")) Program.After(2500, () => StartDialog("active"));
            if (args.Contains("--selftest")) SelfTest();
        }

        void SelfTest()
        {
            var loji = Pets[0];
            var eva = Pets[1];
            Program.After(1500, () => { loji.ShowCharacter(); eva.HideTemporarily(); });
            Program.After(3000, () => eva.ShowCharacter());
            Program.After(14000, () => { StopDialog(); for (int i = 0; i < 6; i++) loji.TestPet(); });
        }

        // Вызывается, когда персонаж появился на экране
        public void OnJoined(PetWindow pet)
        {
            if (!started) return;
            if (!Visible().Any(p => p != pet)) return;
            StopDialog();
            Program.After(700, () => StartDialog(pet.Def.Id + "_joins"));
        }

        // Ревность: гладишь одну — другая может обидеться; если гладить одну подряд — обидится точно
        public void OnPetted(PetWindow pet)
        {
            var other = Visible().FirstOrDefault(p => p != pet);
            if (other == null) return;
            var now = DateTime.Now;
            Queue<DateTime> q;
            if (!clicksOn.TryGetValue(pet, out q)) clicksOn[pet] = q = new Queue<DateTime>();
            q.Enqueue(now);
            while (q.Count > 0 && (now - q.Peek()).TotalSeconds > 15) q.Dequeue();
            Queue<DateTime> oq;
            if (clicksOn.TryGetValue(other, out oq)) oq.Clear();

            bool strong = q.Count >= 6;
            if (now < jealousCooldown || !(strong || Rnd.NextDouble() < 0.12)) return;
            jealousCooldown = now.AddSeconds(12);
            q.Clear();
            Program.After(500, other.BeJealous);
            if (Rnd.NextDouble() < 0.5) Program.After(3200, () => pet.Say(pet.Phrases.Pick("smug")));
        }

        public List<PetWindow> Visible()
        {
            return Pets.Where(p => p.IsVisible && p.WindowState != WindowState.Minimized).ToList();
        }

        void CheckIdle()
        {
            double idle = IdleMs();
            if (!afk && idle > AfkAfterMs)
            {
                afk = true;
                nextAfkTalk = DateTime.Now.AddSeconds(15);
            }
            else if (afk && idle < 3000)
            {
                afk = false;
                nextAfkTalk = DateTime.MaxValue;
                StopDialog();
                var vis = Visible();
                for (int i = 0; i < vis.Count; i++)
                {
                    var p = vis[i];
                    Program.After(i * 1600 + 1, () => p.Say(p.Phrases.Pick("back")));
                }
            }
            else if (afk && !DialogRunning && DateTime.Now >= nextAfkTalk)
            {
                var vis = Visible();
                // разговаривают между собой, только если на экране обе
                if (!(vis.Count >= 2 && Rnd.NextDouble() < 0.7 && StartDialog("afk")))
                {
                    nextAfkTalk = DateTime.Now.AddSeconds(Rnd.Next(60, 150));
                    if (vis.Count > 0) { var p = vis[Rnd.Next(vis.Count)]; p.Say(p.Phrases.Pick("afk")); }
                }
            }
        }

        bool StartDialog(string section)
        {
            List<List<DialogLine>> list;
            if (Visible().Count < 2 || !dialogs.TryGetValue(section, out list) || list.Count == 0) return false;
            StopDialog();
            dialog = list[Rnd.Next(list.Count)];
            dialogPos = 0;
            NextDialogLine();
            return true;
        }

        void NextDialogLine()
        {
            if (dialog == null) return;
            if (dialogPos >= dialog.Count || Visible().Count < 2) { EndDialog(); return; }
            var line = dialog[dialogPos++];
            var speaker = Pets.FirstOrDefault(p => p.Def.Name.Equals(line.Speaker, StringComparison.OrdinalIgnoreCase));
            if (speaker != null) speaker.Say(line.Text);
            dialogTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(2800, line.Text.Length * 38 + 2000));
            dialogTimer.Start();
        }

        void EndDialog()
        {
            dialog = null;
            nextAfkTalk = afk ? DateTime.Now.AddSeconds(Rnd.Next(45, 120)) : DateTime.MaxValue;
        }

        void StopDialog()
        {
            if (dialogTimer != null) dialogTimer.Stop();
            dialog = null;
        }

        // ---------- трей ----------

        void BuildTray()
        {
            var items = new List<WF.MenuItem>();
            foreach (var p in Pets)
            {
                var pet = p;
                var mi = new WF.MenuItem(pet.Def.Name, (s, e) => { if (pet.IsVisible) pet.CloseCharacter(); else pet.ShowCharacter(); });
                mi.Checked = pet.Cfg.Visible;
                trayItems[pet] = mi;
                items.Add(mi);
            }
            items.Add(new WF.MenuItem("-"));
            items.Add(new WF.MenuItem("Показать всех", (s, e) => { foreach (var p in Pets) p.ShowCharacter(); }));
            items.Add(new WF.MenuItem("Спрятать всех", (s, e) => { foreach (var p in Pets) p.HideTemporarily(); }));
            items.Add(new WF.MenuItem("Редактировать диалоги...", (s, e) => Process.Start("notepad.exe", "\"" + DialogsPath + "\"")));
            items.Add(new WF.MenuItem("-"));
            items.Add(new WF.MenuItem("Выход", (s, e) => Exit()));

            tray = new WF.NotifyIcon
            {
                Text = "CPU Logic Pet — Лоджи и Ева", ContextMenu = new WF.ContextMenu(items.ToArray()),
                Visible = true, Icon = MakeTrayIcon()
            };
            tray.MouseClick += (s, e) =>
            {
                if (e.Button != WF.MouseButtons.Left) return;
                if (Pets.Any(p => p.IsVisible)) foreach (var p in Pets) p.HideTemporarily();
                else foreach (var p in Pets.Where(p => p.Cfg.Visible).DefaultIfEmpty(Pets[0])) p.ShowCharacter();
            };
        }

        static System.Drawing.Icon MakeTrayIcon()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("tray.png"))
            using (var bmp = new System.Drawing.Bitmap(stream))
                return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        public void UpdateTray()
        {
            foreach (var kv in trayItems) kv.Value.Checked = kv.Key.IsVisible;
        }

        public void Balloon(string text)
        {
            tray.ShowBalloonTip(2000, "CPU Logic Pet", text, WF.ToolTipIcon.None);
        }

        public void Exit()
        {
            foreach (var p in Pets) p.SaveNow();
            tray.Visible = false;
            tray.Dispose();
            Application.Current.Shutdown();
        }
    }

    // ---------- окно персонажа ----------

    public class PetWindow : Window
    {
        public readonly CharacterDef Def;
        public readonly Settings Cfg;
        public readonly Phrases Phrases;
        readonly Director director;
        readonly Random rnd;
        readonly SolidColorBrush accent;
        double[] sizes;

        BitmapImage imgOpen, imgClosed, cutOpen, cutClosed;
        SoundPlayer[] clickSounds;
        SoundPlayer talkSound;
        System.Speech.Synthesis.SpeechSynthesizer synth;
        bool noRussianVoice;

        Grid root;
        ScaleTransform rootScale;
        StackPanel bubble;
        TextBlock bubbleText, loveText;
        Grid frame;
        Border petBox;
        Image pet, cutPet;
        Grid cutBox;
        Canvas sketch, heartLayer;
        ScaleTransform petScale, cutScale;
        TranslateTransform shakeTransform;
        Canvas glyphBg, glyphMin, glyphMax, glyphClose;
        MenuItem menuNoBg;

        DispatcherTimer blinkTimer, blinkOpenTimer, happyTimer, typeTimer, hideTimer, saveTimer;
        bool eyesHeld, doubleBlink;
        string typeText = "";
        int typePos;
        readonly Queue<DateTime> recentClicks = new Queue<DateTime>();
        DateTime spamCooldown = DateTime.MinValue;
        Point? downPt;

        public PetWindow(CharacterDef def, Director director)
        {
            Def = def;
            this.director = director;
            rnd = director.Rnd;
            Cfg = Settings.Load(def.Id);
            if (double.IsNaN(Cfg.Scale)) Cfg.Scale = def.DefaultScale;
            sizes = new[] { def.DefaultScale * 0.7, def.DefaultScale, def.DefaultScale * 1.4 };
            accent = new SolidColorBrush(def.Accent);
            accent.Freeze();
            Phrases = new Phrases(def.PhrasesFile, rnd);

            imgOpen = LoadImage(def.ImgOpen);
            imgClosed = LoadImage(def.ImgClosed);
            cutOpen = LoadImage(def.CutOpen);
            cutClosed = LoadImage(def.CutClosed);

            double k = def.ChirpPitch;
            clickSounds = new[]
            {
                Sounds.Chirp(Sounds.Scale(new double[] { 880, 1320 }, k), 70, 0.3),
                Sounds.Chirp(Sounds.Scale(new double[] { 1046, 1318 }, k), 110, -0.2),
                Sounds.Chirp(Sounds.Scale(new double[] { 1568, 1760, 1976 }, k), 50, 0.1),
                Sounds.Chirp(Sounds.Scale(new double[] { 660 }, k), 120, 0.6),
                Sounds.Chirp(Sounds.Scale(new double[] { 1318, 988, 1318 }, k), 60, 0.15),
            };
            talkSound = Sounds.Chirp(Sounds.Scale(new double[] { 1200, 1500 }, k), 45, 0.15);

            Title = "CPU Logic Pet — " + def.Name;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Topmost = Cfg.Topmost;

            BuildUi();
            BuildMenu();
            SetupTimers();

            var wa = SystemParameters.WorkArea;
            if (double.IsNaN(Cfg.Left) || Cfg.Left < SystemParameters.VirtualScreenLeft ||
                Cfg.Left > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100)
                Cfg.Left = wa.Right - def.DefaultRightOffset;
            if (double.IsNaN(Cfg.Top) || Cfg.Top < SystemParameters.VirtualScreenTop ||
                Cfg.Top > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100)
                Cfg.Top = wa.Bottom - (def.ImageHeight + 330) * Cfg.Scale;
            Left = Cfg.Left;
            Top = Cfg.Top;

            SetSize(Cfg.Scale);
            ApplyMode();
            UpdateLove();

            Loaded += (s, e) =>
            {
                DrawGlyph(glyphBg, "bg", 3);
                DrawGlyph(glyphMin, "min", 11);
                DrawGlyph(glyphMax, "max", 23);
                DrawGlyph(glyphClose, "close", 37);
                RequestRedraw();
            };
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible) blinkTimer.Start(); else blinkTimer.Stop();
                director.UpdateTray();
            };
            LocationChanged += (s, e) => { saveTimer.Stop(); saveTimer.Start(); };
        }

        static BitmapImage LoadImage(string name)
        {
            var bmp = new BitmapImage();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
            }
            bmp.Freeze();
            return bmp;
        }

        DropShadowEffect Glow(double blur, double opacity)
        {
            return new DropShadowEffect { Color = Def.Accent, BlurRadius = blur, ShadowDepth = 0, Opacity = opacity };
        }

        // ---------- интерфейс ----------

        void BuildUi()
        {
            double rootWidth = Math.Max(Def.ImageWidth, Def.CutWidth) + 48;
            root = new Grid { Width = rootWidth };
            rootScale = new ScaleTransform(0.5, 0.5);
            root.LayoutTransform = rootScale;
            shakeTransform = new TranslateTransform();
            root.RenderTransform = shakeTransform;
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(200) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // облачко с репликой
            bubble = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, -4), Visibility = Visibility.Collapsed, Opacity = 0, IsHitTestVisible = false
            };
            bubbleText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Def.BubbleText),
                FontSize = 30, FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"), FontWeight = FontWeights.SemiBold
            };
            bubble.Children.Add(new Border
            {
                Background = new SolidColorBrush(Def.BubbleBack), BorderBrush = accent, BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(16), Padding = new Thickness(24, 14, 24, 14), MaxWidth = Math.Min(660, rootWidth - 40),
                Effect = Glow(16, 0.8), Child = bubbleText
            });
            bubble.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,0 L 26,0 L 4,28 Z"), Fill = accent,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(-90, -2, 0, 0)
            });
            root.Children.Add(bubble);

            // вид "в окне"
            frame = new Grid { Margin = new Thickness(10) };
            Grid.SetRow(frame, 1);
            frame.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(245, 0, 0, 0)) });
            var inner = new Grid();
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBar = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
            titleBar.MouseLeftButtonDown += (s, e) => DragMove();
            var titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 4, 0, 0), Effect = Glow(10, 0.6)
            };
            titleStack.Children.Add(new TextBlock
            {
                Text = Def.Title, Foreground = new SolidColorBrush(Def.TitleText),
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 28
            });
            loveText = new TextBlock
            {
                Foreground = new SolidColorBrush(Def.CounterText), FontFamily = new FontFamily("Segoe UI Symbol, Segoe UI"),
                FontWeight = FontWeights.Bold, FontSize = 22, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 2, 0, 0), ToolTip = "Сколько раз ты погладил " + (Def.Id == "eva" ? "Еву" : "Лоджи")
            };
            titleStack.Children.Add(loveText);
            titleBar.Children.Add(titleStack);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 12, 0), Effect = Glow(8, 0.7)
            };
            buttons.Children.Add(MakeButton(out glyphBg, "Убрать фон", (s, e) => ToggleBackground()));
            buttons.Children.Add(MakeButton(out glyphMin, "Спрятать в трей", (s, e) => HideTemporarily()));
            buttons.Children.Add(MakeButton(out glyphMax, "Сменить размер", (s, e) => CycleSize()));
            buttons.Children.Add(MakeButton(out glyphClose, "Закрыть " + (Def.Id == "eva" ? "Еву" : "Лоджи"), (s, e) => CloseCharacter()));
            titleBar.Children.Add(buttons);
            inner.Children.Add(titleBar);

            petScale = new ScaleTransform(1, 1);
            pet = new Image { Width = Def.ImageWidth, Height = Def.ImageHeight, Source = imgOpen };
            RenderOptions.SetBitmapScalingMode(pet, BitmapScalingMode.HighQuality);
            petBox = new Border
            {
                Margin = new Thickness(14, 2, 14, 14), ClipToBounds = true, Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.6), RenderTransform = petScale, Child = pet
            };
            Grid.SetRow(petBox, 1);
            HookPetting(petBox);
            inner.Children.Add(petBox);
            frame.Children.Add(inner);

            sketch = new Canvas { IsHitTestVisible = false, Effect = Glow(8, 0.7) };
            sketch.SizeChanged += (s, e) => RedrawSketch();
            frame.Children.Add(sketch);
            root.Children.Add(frame);

            // вид "без фона": только персонаж, низ мягко растворяется
            cutScale = new ScaleTransform(1, 1);
            cutPet = new Image { Width = Def.CutWidth, Height = Def.CutHeight, Source = cutOpen, Cursor = Cursors.Hand };
            RenderOptions.SetBitmapScalingMode(cutPet, BitmapScalingMode.HighQuality);
            var fade = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            fade.GradientStops.Add(new GradientStop(Colors.Black, 0.0));
            fade.GradientStops.Add(new GradientStop(Colors.Black, 0.86));
            fade.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
            cutPet.OpacityMask = fade;
            cutPet.Effect = Glow(18, 0.55);
            cutBox = new Grid
            {
                Margin = new Thickness(24, 10, 24, 24), RenderTransformOrigin = new Point(0.5, 0.6), RenderTransform = cutScale,
                Visibility = Visibility.Collapsed
            };
            cutBox.Children.Add(cutPet);
            Grid.SetRow(cutBox, 1);
            HookPetting(cutPet);
            root.Children.Add(cutBox);

            heartLayer = new Canvas { IsHitTestVisible = false };
            Grid.SetRowSpan(heartLayer, 2);
            root.Children.Add(heartLayer);

            Content = root;
        }

        Button MakeButton(out Canvas glyph, string tip, RoutedEventHandler onClick)
        {
            var template = new ControlTemplate(typeof(Button));
            var bg = new FrameworkElementFactory(typeof(Border), "bg");
            bg.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)));
            bg.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            template.VisualTree = bg;
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(102, (byte)(Def.Accent.R / 4), (byte)(Def.Accent.G / 4), (byte)(Def.Accent.B / 4))), "bg"));
            template.Triggers.Add(hover);

            glyph = new Canvas { Width = 42, Height = 32 };
            var b = new Button
            {
                Template = template, Content = glyph, Cursor = Cursors.Hand, Focusable = false,
                Margin = new Thickness(3, 0, 3, 0), ToolTip = tip
            };
            b.Click += onClick;
            return b;
        }

        // ---------- "рисованные от руки" рамки ----------

        void AddJitterLine(Canvas c, double x1, double y1, double x2, double y2, Random r, double thick, double opacity)
        {
            double dx = x2 - x1, dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            double nx = -dy / len, ny = dx / len;
            int n = Math.Max(2, (int)(len / 22));
            double bow = (r.NextDouble() - 0.5) * Math.Min(3, len / 30);
            var pl = new Polyline
            {
                Stroke = accent, StrokeThickness = thick, Opacity = opacity, StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;
                double off = (r.NextDouble() - 0.5) * 1.8 + bow * Math.Sin(Math.PI * t);
                pl.Points.Add(new Point(x1 + dx * t + nx * off, y1 + dy * t + ny * off));
            }
            c.Children.Add(pl);
        }

        void AddStroke(Canvas c, double x1, double y1, double x2, double y2, Random r)
        {
            AddJitterLine(c, x1, y1, x2, y2, r, 2.6, 1.0);
            AddJitterLine(c, x1, y1, x2, y2, r, 1.2, 0.55);
        }

        void AddSketchRect(Canvas c, double x, double y, double w, double h, Random r)
        {
            double e = Math.Min(3, w * 0.06);
            AddStroke(c, x - e, y, x + w + e, y, r);
            AddStroke(c, x + w, y - e, x + w, y + h + e, r);
            AddStroke(c, x + w + e, y + h, x - e, y + h, r);
            AddStroke(c, x, y + h + e, x, y - e, r);
        }

        void DrawGlyph(Canvas c, string kind, int seed)
        {
            var r = new Random(seed);
            c.Children.Clear();
            AddSketchRect(c, 2, 2, 38, 28, r);
            switch (kind)
            {
                case "bg": AddSketchRect(c, 12, 9, 18, 14, r); AddStroke(c, 10, 26, 32, 6, r); break;
                case "min": AddStroke(c, 12, 22, 30, 22, r); break;
                case "max": AddSketchRect(c, 13, 9, 16, 14, r); break;
                case "close": AddStroke(c, 14, 8, 28, 24, r); AddStroke(c, 28, 8, 14, 24, r); break;
            }
        }

        void RedrawSketch()
        {
            sketch.Children.Clear();
            double w = sketch.ActualWidth, h = sketch.ActualHeight;
            if (w < 10 || h < 10) return;
            var r = new Random(Def.Id == "eva" ? 19 : 7);
            AddSketchRect(sketch, 3, 3, w - 6, h - 6, r);
            if (petBox.ActualWidth > 0)
            {
                var p = petBox.TranslatePoint(new Point(0, 0), sketch);
                AddSketchRect(sketch, p.X, p.Y, petBox.ActualWidth, petBox.ActualHeight, r);
            }
        }

        void RequestRedraw()
        {
            Dispatcher.BeginInvoke(new Action(RedrawSketch), DispatcherPriority.Loaded);
        }

        // ---------- меню ----------

        MenuItem Item(string header, Action onClick, bool checkable = false, bool isChecked = false)
        {
            var mi = new MenuItem { Header = header, IsCheckable = checkable, IsChecked = isChecked };
            mi.Click += (s, e) => onClick();
            return mi;
        }

        void BuildMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(Item("Скажи что-нибудь", () => Say(Phrases.PickRandom())));
            menuNoBg = Item("Без фона", ToggleBackground, true, Cfg.NoBackground);
            menu.Items.Add(menuNoBg);
            var size = new MenuItem { Header = "Размер" };
            size.Items.Add(Item("Маленький", () => { SetSize(sizes[0]); SaveNow(); }));
            size.Items.Add(Item("Средний", () => { SetSize(sizes[1]); SaveNow(); }));
            size.Items.Add(Item("Большой", () => { SetSize(sizes[2]); SaveNow(); }));
            menu.Items.Add(size);
            menu.Items.Add(new Separator());

            MenuItem sound = null, voice = null, top = null, auto = null;
            sound = Item("Звуки", () => { Cfg.Sound = sound.IsChecked; SaveNow(); }, true, Cfg.Sound);
            voice = Item("Говорить вслух", () => { Cfg.Voice = voice.IsChecked; SaveNow(); }, true, Cfg.Voice);
            top = Item("Поверх всех окон", () => { Cfg.Topmost = top.IsChecked; Topmost = top.IsChecked; SaveNow(); }, true, Cfg.Topmost);
            auto = Item("Запускать вместе с Windows", () => SetAutostart(auto.IsChecked), true, IsAutostart());
            menu.Items.Add(sound);
            menu.Items.Add(voice);
            menu.Items.Add(top);
            menu.Items.Add(auto);
            menu.Items.Add(Item("Редактировать фразы...", () => Process.Start("notepad.exe", "\"" + Phrases.FilePath + "\"")));
            menu.Items.Add(Item("Редактировать диалоги...", () => Process.Start("notepad.exe", "\"" + director.DialogsPath + "\"")));
            menu.Items.Add(new Separator());
            foreach (var other in director.Pets)
                if (other != this) menu.Items.Add(Item("Позвать " + (other.Def.Id == "eva" ? "Еву" : "Лоджи"), other.ShowCharacter));
            menu.Items.Add(Item("Спрятать в трей", HideTemporarily));
            menu.Items.Add(Item("Закрыть " + (Def.Id == "eva" ? "Еву" : "Лоджи"), CloseCharacter));
            menu.Items.Add(Item("Выход (обе)", director.Exit));
            menu.Opened += (s, e) =>
            {
                // пункты "Позвать" показываем только для тех, кого сейчас нет на экране
                foreach (var obj in menu.Items)
                {
                    var mi = obj as MenuItem;
                    if (mi == null || !(mi.Header is string) || !((string)mi.Header).StartsWith("Позвать")) continue;
                    var name = ((string)mi.Header).Substring(8);
                    var target = director.Pets.FirstOrDefault(p => p != this && (p.Def.Id == "eva" ? "Еву" : "Лоджи") == name);
                    mi.Visibility = target != null && !target.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            root.ContextMenu = menu;
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static bool IsAutostart()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("CPULogicPet") != null;
        }

        static void SetAutostart(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue("CPULogicPet", "\"" + Assembly.GetExecutingAssembly().Location + "\"");
                else k.DeleteValue("CPULogicPet", false);
            }
        }

        // ---------- показ, скрытие, режимы ----------

        public void ShowCharacter()
        {
            bool wasVisible = IsVisible;
            Cfg.Visible = true;
            Show();
            Activate();
            SaveNow();
            if (!wasVisible) director.OnJoined(this);
        }

        public void HideTemporarily()
        {
            if (!IsVisible) return;
            SaveNow();
            Hide();
            director.Balloon((Def.Id == "eva" ? "Ева" : "Лоджи") + " в трее. Нажми на значок, чтобы вернуть.");
        }

        public void CloseCharacter()
        {
            SaveNow();
            Cfg.Visible = false;
            Cfg.Save();
            Hide();
            if (!director.Pets.Any(p => p.IsVisible)) director.Balloon("Все в трее. Нажми на значок, чтобы нас вернуть ❤");
        }

        void ApplyMode()
        {
            frame.Visibility = Cfg.NoBackground ? Visibility.Collapsed : Visibility.Visible;
            cutBox.Visibility = Cfg.NoBackground ? Visibility.Visible : Visibility.Collapsed;
            if (menuNoBg != null) menuNoBg.IsChecked = Cfg.NoBackground;
            RequestRedraw();
        }

        void ToggleBackground()
        {
            Cfg.NoBackground = !Cfg.NoBackground;
            ApplyMode();
            SaveNow();
            if (Cfg.NoBackground) Say(Def.BackgroundTip);
        }

        void SetSize(double s)
        {
            Cfg.Scale = s;
            rootScale.ScaleX = s;
            rootScale.ScaleY = s;
            // текст в облачке одного размера на экране, как бы ни было уменьшено окно
            bubbleText.FontSize = Math.Max(22, 15 / s);
            RequestRedraw();
        }

        void CycleSize()
        {
            int i = 0;
            for (int j = 0; j < sizes.Length; j++) if (Math.Abs(sizes[j] - Cfg.Scale) < 0.01) i = j;
            SetSize(sizes[(i + 1) % sizes.Length]);
            SaveNow();
        }

        public void SaveNow()
        {
            if (IsVisible) { Cfg.Left = Left; Cfg.Top = Top; }
            Cfg.Save();
        }

        void UpdateLove() { loveText.Text = Def.CounterGlyph + " " + Cfg.Love; }

        // ---------- глаза и таймеры ----------

        void SetEyes(bool closed)
        {
            pet.Source = closed ? imgClosed : imgOpen;
            cutPet.Source = closed ? cutClosed : cutOpen;
        }

        void SetupTimers()
        {
            blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500 + rnd.Next(1500)) };
            blinkOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(130) };
            blinkTimer.Tick += (s, e) =>
            {
                blinkTimer.Interval = TimeSpan.FromMilliseconds(rnd.Next(2500, 6500));
                if (eyesHeld) return;
                SetEyes(true);
                doubleBlink = rnd.NextDouble() < 0.2;
                blinkOpenTimer.Start();
            };
            blinkOpenTimer.Tick += (s, e) =>
            {
                blinkOpenTimer.Stop();
                if (eyesHeld) return;
                SetEyes(false);
                if (doubleBlink)
                {
                    doubleBlink = false;
                    blinkTimer.Stop();
                    blinkTimer.Interval = TimeSpan.FromMilliseconds(180);
                    blinkTimer.Start();
                }
            };

            happyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            happyTimer.Tick += (s, e) => { happyTimer.Stop(); eyesHeld = false; SetEyes(false); };

            typeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(38) };
            typeTimer.Tick += (s, e) =>
            {
                typePos++;
                bubbleText.Text = typeText.Substring(0, Math.Min(typePos, typeText.Length));
                if (typePos % 6 == 1) PlaySound(talkSound);
                if (typePos >= typeText.Length) typeTimer.Stop();
            };
            hideTimer = new DispatcherTimer();
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400));
                fadeOut.Completed += (s2, e2) => { if (bubble.Opacity < 0.05) bubble.Visibility = Visibility.Collapsed; };
                bubble.BeginAnimation(OpacityProperty, fadeOut);
            };

            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); SaveNow(); };
        }

        // ---------- звук и голос ----------

        void PlaySound(SoundPlayer p)
        {
            if (!Cfg.Sound) return;
            try { p.Play(); } catch { }
        }

        void Speak(string text)
        {
            if (!Cfg.Voice || noRussianVoice) return;
            try
            {
                if (synth == null)
                {
                    synth = new System.Speech.Synthesis.SpeechSynthesizer();
                    var ru = synth.GetInstalledVoices().FirstOrDefault(v => v.VoiceInfo.Culture.Name.StartsWith("ru"));
                    if (ru == null)
                    {
                        noRussianVoice = true;
                        MessageBox.Show("В Windows не установлен русский голос, поэтому говорить вслух не получится.\n" +
                                        "Его можно добавить: Параметры → Время и язык → Речь.", "CPU Logic Pet");
                        return;
                    }
                    synth.SelectVoice(ru.VoiceInfo.Name);
                    synth.Rate = 1;
                }
                string clean = Regex.Replace(text, @"[^\p{IsCyrillic}A-Za-z0-9\s\.,!\?\-—«»…]", "").Trim();
                if (clean.Length == 0) return;
                synth.SpeakAsyncCancelAll();
                var ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='ru-RU'>" +
                           "<prosody pitch='" + Def.VoicePitch + "'>" + System.Security.SecurityElement.Escape(clean) + "</prosody></speak>";
                synth.SpeakSsmlAsync(ssml);
            }
            catch { }
        }

        // ---------- реплики ----------

        public void Say(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsVisible) return;
            typeTimer.Stop();
            hideTimer.Stop();
            typeText = text;
            typePos = 0;
            bubbleText.Text = "";
            bubble.Visibility = Visibility.Visible;
            bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
            typeTimer.Start();
            hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(4500, text.Length * 38 + 3500));
            hideTimer.Start();
            Speak(text);
        }

        // ---------- поглаживание ----------

        void HookPetting(FrameworkElement el)
        {
            // короткий клик — погладить, потянуть — перетащить окно
            el.MouseLeftButtonDown += (s, e) => { downPt = e.GetPosition(this); };
            el.MouseMove += (s, e) =>
            {
                if (downPt == null || e.LeftButton != MouseButtonState.Pressed) return;
                var p = e.GetPosition(this);
                if (Math.Abs(p.X - downPt.Value.X) + Math.Abs(p.Y - downPt.Value.Y) > 6)
                {
                    downPt = null;
                    DragMove();
                }
            };
            el.MouseLeftButtonUp += (s, e) =>
            {
                if (downPt != null) PetHer(e.GetPosition(heartLayer));
                downPt = null;
            };
        }

        void PetHer(Point at)
        {
            Cfg.Love++;
            UpdateLove();
            int hearts = rnd.Next(3, 7);
            for (int i = 0; i < hearts; i++) SpawnHeart(at.X + rnd.Next(-30, 30), at.Y + rnd.Next(-20, 20));
            PlaySound(clickSounds[rnd.Next(clickSounds.Length)]);
            Bounce();
            eyesHeld = true;
            SetEyes(true);
            happyTimer.Stop();
            happyTimer.Start();

            var now = DateTime.Now;
            recentClicks.Enqueue(now);
            while (recentClicks.Count > 0 && (now - recentClicks.Peek()).TotalSeconds > 2) recentClicks.Dequeue();

            if (Cfg.Love % 50 == 0)
                Say(Def.Id == "eva" ? "Поглаживаний: " + Cfg.Love + ". Записала в личное дело. Ты молодец." : "Уже " + Cfg.Love + " раз! Я тебя обожаю ❤");
            else if (recentClicks.Count >= 6 && now > spamCooldown) { spamCooldown = now.AddSeconds(5); Say(Phrases.Pick("spam")); }
            else if (rnd.NextDouble() < 0.35 && now > spamCooldown) Say(Phrases.Pick("click"));
            if (Cfg.Love % 10 == 0) Cfg.Save();
            director.OnPetted(this);
        }

        // для --selftest: поглаживание в центр персонажа
        public void TestPet()
        {
            PetHer(new Point(root.Width / 2, 200 + Def.ImageHeight * 0.35));
        }

        // Ревность: обиженная реплика, знаки злости над головой и окно вздрагивает
        public void BeJealous()
        {
            if (!IsVisible) return;
            Say(Phrases.Pick("jealous"));
            var anchor = Cfg.NoBackground ? (FrameworkElement)cutPet : petBox;
            var p = anchor.TranslatePoint(new Point(anchor.ActualWidth * 0.68, anchor.ActualHeight * 0.12), heartLayer);
            for (int i = 0; i < 3; i++)
                SpawnFloat(p.X + rnd.Next(-50, 50), p.Y + rnd.Next(-20, 30), i == 0 ? "💔" : "💢", i == 0 ? "#B0B0B0" : "#FF2B2B");

            var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(450) };
            double[] xs = { 0, -9, 9, -7, 7, -4, 4, 0 };
            for (int i = 0; i < xs.Length; i++)
                shake.KeyFrames.Add(new LinearDoubleKeyFrame(xs[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 60))));
            shakeTransform.BeginAnimation(TranslateTransform.XProperty, shake);
        }

        void Bounce()
        {
            var a = new DoubleAnimation(1, 1.05, TimeSpan.FromMilliseconds(110))
            {
                AutoReverse = true, EasingFunction = new QuadraticEase()
            };
            foreach (var st in new[] { petScale, cutScale })
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
        }

        void SpawnHeart(double x, double y)
        {
            SpawnFloat(x, y, Def.HeartGlyphs[rnd.Next(Def.HeartGlyphs.Length)], Def.HeartColors[rnd.Next(Def.HeartColors.Length)]);
        }

        void SpawnFloat(double x, double y, string glyph, string color)
        {
            var tb = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = rnd.Next(26, 56),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                Effect = new DropShadowEffect { Color = Def.Accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.8 },
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Canvas.SetLeft(tb, x - tb.FontSize / 2);
            Canvas.SetTop(tb, y - tb.FontSize / 2);
            var scale = new ScaleTransform(0.3, 0.3);
            var move = new TranslateTransform();
            var tg = new TransformGroup();
            tg.Children.Add(scale);
            tg.Children.Add(move);
            tb.RenderTransform = tg;
            heartLayer.Children.Add(tb);

            var dur = TimeSpan.FromMilliseconds(rnd.Next(1100, 1900));
            var pop = new DoubleAnimation(0.3, 0.9 + rnd.NextDouble() * 0.4, TimeSpan.FromMilliseconds(220));
            var up = new DoubleAnimation(0, -rnd.Next(140, 300), dur) { EasingFunction = new QuadraticEase() };
            var side = new DoubleAnimation(0, rnd.Next(-70, 70), dur) { EasingFunction = new SineEase() };
            var fadeOut = new DoubleAnimation(1, 0, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            fadeOut.Completed += (s, e) => heartLayer.Children.Remove(tb);

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            move.BeginAnimation(TranslateTransform.YProperty, up);
            move.BeginAnimation(TranslateTransform.XProperty, side);
            tb.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
