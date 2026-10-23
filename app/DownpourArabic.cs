// Silent Hill: Downpour (DownpourRecomp PC port) - Arabic translation tool. One window: install, uninstall.
// Same logic as the release's Install.ps1 / Uninstall.ps1 (GAMES/SilentHillDownpour/TOOLS/make_release.py); written
// after the PowerShell scripts were found to be refused on a default Windows (RemoteSigned + downloaded file).
// Build (C# 5, .NET Framework 4 that ships with Windows): build.cmd. No game file is inside the program.
// Headless test mode: DownpourArabic.exe --install <port> | --uninstall <port>; writes DownpourArabic.log next to the
// exe and exits 0 on success.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Silent Hill: Downpour Arabic")]
[assembly: AssemblyProduct("Silent Hill: Downpour Arabic Translation")]
[assembly: AssemblyCompany("Osama")]
[assembly: AssemblyDescription("Installs the Arabic translation of Silent Hill: Downpour for the DownpourRecomp PC port")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace DownpourArabic
{
    class Failure : Exception { public Failure(string ar, string en) : base(ar + "\r\n" + en) { } }

    static class Core
    {
        public static Action<string> Log = delegate { };
        public static readonly string Here = AppDomain.CurrentDomain.BaseDirectory;
        const string BackupName = "DownpourArabic-backup";

        public static bool IsPort(string dir)
        {
            // the port folder: downpour.exe next to the installed game (a bare copy of the game files is not a port)
            return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "downpour.exe"))
                && File.Exists(Path.Combine(dir, "assets\\SHGame\\Xbox360TOC.txt"));
        }

        static void CheckGameClosed()
        {
            if (Process.GetProcessesByName("downpour").Length > 0 || Process.GetProcessesByName("PlayDownpour").Length > 0)
                throw new Failure("سكّر اللعبة والبورت أول وبعدين حاول مرة ثانية.", "Close the game and the port first, then try again.");
        }

        static string Sha(string path)
        {
            using (var s = File.OpenRead(path))
            using (var h = SHA256.Create())
                return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "");
        }

        static ArrayList ManifestFiles()
        {
            string p = Path.Combine(Here, "patch\\manifest.json");
            if (!File.Exists(p) || !File.Exists(Path.Combine(Here, "patch\\data.bin")))
                throw new Failure("مجلد patch ناقص. فك ضغط ملف التعريب كامل وشغّل البرنامج من داخله.",
                                  "The patch folder is missing. Extract the whole translation zip and run the program from inside it.");
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var m = (Dictionary<string, object>)js.DeserializeObject(File.ReadAllText(p, Encoding.UTF8));
            return new ArrayList((object[])m["files"]);
        }

        static void CopyRange(Stream from, Stream to, long off, long n, byte[] buf)
        {
            from.Seek(off, SeekOrigin.Begin);
            while (n > 0)
            {
                int k = from.Read(buf, 0, (int)Math.Min(buf.Length, n));
                if (k <= 0) throw new IOException("short read");
                to.Write(buf, 0, k); n -= k;
            }
        }

        public static void Install(string port, Action<int> progress)
        {
            CheckGameClosed();
            if (!IsPort(port))
                throw new Failure("هذا مو مجلد البورت، أو اللعبة ما تثبتت فيه بعد (شغّل PlayDownpour.exe وخل البورت يثبّت اللعبة أول).",
                                  "This is not the port folder, or the game is not installed in it yet (run PlayDownpour.exe and let the port install the game first).");
            string assets = Path.Combine(port, "assets");
            var todo = new List<Dictionary<string, object>>();
            var files = ManifestFiles();
            int checkedCount = 0;
            foreach (Dictionary<string, object> f in files)
            {
                string dst = Path.Combine(assets, (string)f["path"]);
                if (!File.Exists(dst))
                    throw new Failure("الملف " + f["path"] + " مو موجود. ثبّت اللعبة في البورت أول.", f["path"] + " is missing. Install the game in the port first.");
                string h = Sha(dst);
                progress(++checkedCount * 30 / files.Count);
                if (h == (string)f["patched_sha256"]) continue;
                if (h != (string)f["stock_sha256"])
                    throw new Failure("الملف " + f["path"] + " ما يطابق النسخة الأمريكية الأصلية. ثبّت اللعبة في البورت من جديد ثم ثبّت التعريب.",
                                      f["path"] + " does not match the original US game. Install the game in the port again, then install the translation.");
                todo.Add(f);
            }
            Log("فحص " + files.Count + " ملف / Checked " + files.Count + " files");
            if (todo.Count == 0) { Log("التعريب مثبّت من قبل / The Arabic translation is already installed."); return; }
            string bak = Path.Combine(port, BackupName);
            foreach (var f in todo)
            {
                string b = Path.Combine(bak, (string)f["path"]);
                if (File.Exists(b)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(b));
                File.Copy(Path.Combine(assets, (string)f["path"]), b);
            }
            Log("نسخة احتياطية / Backed up " + todo.Count + " files -> " + BackupName);
            var buf = new byte[1 << 20];
            var zero = new byte[1 << 20];
            using (var data = File.OpenRead(Path.Combine(Here, "patch\\data.bin")))
            {
                int done = 0;
                foreach (var f in todo)
                {
                    string dst = Path.Combine(assets, (string)f["path"]);
                    string tmp = dst + ".arabic-tmp";
                    using (var src = File.OpenRead(Path.Combine(bak, (string)f["path"])))
                    using (var output = File.Create(tmp))
                    {
                        foreach (object[] op in (object[])f["ops"])
                        {
                            int kind = Convert.ToInt32(op[0]);
                            long off = Convert.ToInt64(op[1]), n = Convert.ToInt64(op[2]);
                            if (kind == 0) CopyRange(src, output, off, n, buf);
                            else if (kind == 1) CopyRange(data, output, off, n, buf);
                            else while (n > 0) { int k = (int)Math.Min(zero.Length, n); output.Write(zero, 0, k); n -= k; }
                        }
                    }
                    if (Sha(tmp) != (string)f["patched_sha256"])
                    {
                        File.Delete(tmp);
                        throw new Failure("فشل بناء الملف " + f["path"] + " وما تغيّر. شغّل الإزالة.",
                                          "Building " + f["path"] + " failed; that file was not changed. Run Uninstall.");
                    }
                    File.SetAttributes(dst, FileAttributes.Normal);
                    File.Delete(dst);
                    File.Move(tmp, dst);
                    progress(30 + ++done * 70 / todo.Count);
                }
            }
            Log("تركيب / Wrote " + todo.Count + " files");
            File.WriteAllText(Path.Combine(Here, "game_folder.txt"), port, Encoding.UTF8);
        }

        public static void Uninstall(string port)
        {
            CheckGameClosed();
            if (!IsPort(port))
                throw new Failure("هذا مو مجلد البورت.", "This is not the port folder.");
            string bak = Path.Combine(port, BackupName);
            if (!Directory.Exists(bak))
                throw new Failure("ما لقيت النسخة الاحتياطية (DownpourArabic-backup). لإرجاع الملفات الأصلية ثبّت اللعبة في البورت من جديد.",
                                  "No backup found (DownpourArabic-backup). To get the original files back, install the game in the port again.");
            int n = 0;
            foreach (Dictionary<string, object> f in ManifestFiles())
            {
                string b = Path.Combine(bak, (string)f["path"]), dst = Path.Combine(port, "assets", (string)f["path"]);
                if (!File.Exists(b)) continue;
                if (File.Exists(dst)) File.SetAttributes(dst, FileAttributes.Normal);
                File.Copy(b, dst, true);
                n++;
            }
            Log("رجّعت / Restored " + n + " files");
            foreach (string b in Directory.GetFiles(bak, "*", SearchOption.AllDirectories))
                File.SetAttributes(b, FileAttributes.Normal);         // backups of read-only originals are read-only too
            Directory.Delete(bak, true);
        }

        public static string SavedPort()
        {
            string p = Path.Combine(Here, "game_folder.txt");
            if (File.Exists(p)) return File.ReadAllText(p, Encoding.UTF8).Trim();
            string parent = Path.GetDirectoryName(Here.TrimEnd('\\'));    // the zip extracted inside the port folder
            return IsPort(parent) ? parent : "";
        }
    }

    class MainForm : Form
    {
        readonly TextBox portDir = new TextBox();
        readonly TextBox log = new TextBox();
        readonly ProgressBar bar = new ProgressBar();
        readonly List<Button> actions = new List<Button>();
        static readonly Font Body = new Font("Segoe UI", 9.5f), Head = new Font("Segoe UI", 11f, FontStyle.Bold);

        public MainForm()
        {
            Text = "Silent Hill: Downpour — التعريب العربي / Arabic Translation";
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            Font = Body; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 560); MinimumSize = new Size(700, 520);
            var col = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
            Controls.Add(col);

            col.Controls.Add(Title("قبل التثبيت", "Before installing"));
            col.Controls.Add(Note("ثبّت البورت DownpourRecomp 1.1.8، وافتح PlayDownpour.exe وخله يثبّت اللعبة من ملف الـ ISO حقك وينزّل التحديث TU1. خل اللعبة توصل للقائمة الرئيسية وبعدين سكّرها.",
                                  "Install the DownpourRecomp 1.1.8 port, open PlayDownpour.exe and let it install the game from your ISO and download TU1. Let the game reach the main menu, then close it."));
            col.Controls.Add(Links());

            col.Controls.Add(Title("١. ثبّت التعريب", "1. Install the translation"));
            col.Controls.Add(Row("مجلد البورت\r\nPort folder (with downpour.exe)", portDir, () => PickFolder(portDir)));
            col.Controls.Add(AddAction("ثبّت التعريب — Install", OnInstall));

            col.Controls.Add(Title("٢. إزالة التعريب", "2. Uninstall"));
            col.Controls.Add(AddAction("أزل التعريب — Uninstall", OnUninstall));

            bar.Width = 720; bar.Height = 18; bar.Margin = new Padding(0, 14, 0, 6);
            col.Controls.Add(bar);
            log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.Width = 720; log.Height = 120;
            log.RightToLeft = RightToLeft.No;
            col.Controls.Add(log);

            portDir.Text = Core.SavedPort();
            Core.Log = s => BeginInvoke((Action)(() => log.AppendText(s + "\r\n")));
        }

        static Control Title(string ar, string en)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(0, 16, 0, 4) };
            p.Controls.Add(new Label { Text = ar, Font = Head, AutoSize = true, Margin = new Padding(0) });
            p.Controls.Add(new Label { Text = en, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0), RightToLeft = RightToLeft.No });
            return p;
        }

        static Control Note(string ar, string en)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
            p.Controls.Add(new Label { Text = ar, AutoSize = true, MaximumSize = new Size(720, 0), ForeColor = Color.DimGray, Margin = new Padding(0) });
            p.Controls.Add(new Label { Text = en, AutoSize = true, MaximumSize = new Size(720, 0), ForeColor = Color.DimGray, Margin = new Padding(0), RightToLeft = RightToLeft.No });
            return p;
        }

        static Control Links()
        {
            var p = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 2, 0, 4) };
            p.Controls.Add(Link("DownpourRecomp 1.1.8", "https://github.com/LittleBitUA/DownpourRecomp/releases"));
            p.Controls.Add(Link("Visual C++ Redistributable (x64)", "https://aka.ms/vs/17/release/vc_redist.x64.exe"));
            return p;
        }

        static LinkLabel Link(string text, string url)
        {
            var l = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 0, 16, 0) };
            l.LinkClicked += (s, e) => Process.Start(url);
            return l;
        }

        static Control Row(string label, TextBox box, Action browse)
        {
            var p = new TableLayoutPanel { ColumnCount = 3, Width = 720, Height = 44, Margin = new Padding(0, 2, 0, 2) };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Right | AnchorStyles.Left, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right; box.RightToLeft = RightToLeft.No;
            p.Controls.Add(box, 1, 0);
            var b = new Button { Text = "اختيار...", Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 28 };
            b.Click += (s, e) => browse();
            p.Controls.Add(b, 2, 0);
            return p;
        }

        Button AddAction(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 6, 0, 2) };
            b.Click += click;
            actions.Add(b);
            return b;
        }

        static void PickFolder(TextBox box)
        {
            using (var d = new FolderBrowserDialog { SelectedPath = box.Text })
                if (d.ShowDialog() == DialogResult.OK) box.Text = d.SelectedPath;
        }

        void Run(ThreadStart work)
        {
            foreach (var b in actions) b.Enabled = false;
            bar.Value = 0;
            new Thread(() =>
            {
                try { work(); }
                catch (Failure f) { Done(f.Message, MessageBoxIcon.Error); }
                catch (Exception e) { Done("صار خطأ:\r\nError: " + e.Message, MessageBoxIcon.Error); }
            }) { IsBackground = true }.Start();
        }

        void Done(string message, MessageBoxIcon icon)
        {
            BeginInvoke((Action)(() =>
            {
                foreach (var b in actions) b.Enabled = true;
                bar.Value = icon == MessageBoxIcon.Error ? 0 : 100;
                log.AppendText(message + "\r\n");
                MessageBox.Show(this, message, "Silent Hill: Downpour", MessageBoxButtons.OK, icon,
                                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            }));
        }

        void OnInstall(object sender, EventArgs e)
        {
            string port = portDir.Text.Trim();
            Run(() =>
            {
                Core.Install(port, p => BeginInvoke((Action)(() => bar.Value = Math.Min(100, p))));
                Done("تم تثبيت التعريب.\r\nThe Arabic translation is installed.\r\n\r\n" +
                     "شغّل اللعبة وفعّل الترجمة النصية: «خيارات» ← «الترجمة النصية» ← «نعم».\r\n" +
                     "Start the game and turn on subtitles: Options > Subtitles > Yes.", MessageBoxIcon.Information);
            });
        }

        void OnUninstall(object sender, EventArgs e)
        {
            string port = portDir.Text.Trim();
            Run(() =>
            {
                Core.Uninstall(port);
                Done("تمت إزالة التعريب وعادت ملفات اللعبة الأصلية.\r\nThe Arabic translation was removed and the original game files are back.", MessageBoxIcon.Information);
            });
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0) return Headless(args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        static int Headless(string[] a)
        {
            var log = new StringBuilder();
            Core.Log = s => { lock (log) log.AppendLine(s); };
            int rc = 0;
            try
            {
                if (a[0] == "--install" && a.Length == 2) Core.Install(a[1], p => { });
                else if (a[0] == "--uninstall" && a.Length == 2) Core.Uninstall(a[1]);
                else { Core.Log("usage: --install <port> | --uninstall <port>"); rc = 2; }
            }
            catch (Exception e) { Core.Log("ERROR " + e.Message); rc = 1; }
            File.WriteAllText(Path.Combine(Core.Here, "DownpourArabic.log"), log.ToString(), Encoding.UTF8);
            return rc;
        }
    }
}
